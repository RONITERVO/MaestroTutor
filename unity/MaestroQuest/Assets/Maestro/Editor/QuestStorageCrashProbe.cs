// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    /// <summary>Disposable batch-Editor crash target. Never included in a player.</summary>
    public static class QuestStorageCrashProbe
    {
        const string Program="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Cell="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        static readonly string[] Stages={"before-journal","prepared","room","memory","committed","room-backup","memory-backup"};
        static readonly UTF8Encoding Utf8=new(false,true);
        static string directory,workspace,id,stage;
        static bool first;
        static JObject owner;
        static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        static RoomSnapshotTransaction.Snapshot Pair(int n)=>RoomSnapshotTransaction.FromDocuments(
            new RoomDocument{objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(n,1,0)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}},
            ProgramMemoryDocument.Empty().WithValues(Program,new Dictionary<string,ProgramMemoryDocument.Cell>{{Cell,new("count",new ProgramValue(n))}}));
        static void Publish(string name,JObject value)
        {
            string target=Path.Combine(directory,name),pending=target+".pending";
            var bytes=Utf8.GetBytes(value.ToString(Newtonsoft.Json.Formatting.None));
            using(var file=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            File.Move(pending,target);
        }
        static string Wire(byte[] bytes)=>bytes==null?null:Convert.ToBase64String(bytes);
        static JToken JsonWire(byte[] bytes)=>bytes==null?JValue.CreateNull():new JValue(Wire(bytes));
        static JObject Files(RoomSnapshotTransaction.Snapshot pair)=>new(){["room"]=JsonWire(pair.Room),["memory"]=JsonWire(pair.Memory),["identity"]=pair.Identity};
        static string Read(string name){string path=Path.Combine(workspace,name);return File.Exists(path)?Wire(File.ReadAllBytes(path)):null;}
        static void WaitForTermination(string marker,string point)
        {
            using var process=Process.GetCurrentProcess();
            Publish(marker,new JObject{["version"]=1,["id"]=id,["stage"]=point,["pid"]=process.Id,["startedUtcTicks"]=process.StartTime.ToUniversalTime().Ticks,
                ["primaryRoom"]=Read(RoomStorage.FileName),["primaryMemory"]=Read(ProgramMemoryStore.FileName),["journal"]=Read(RoomSnapshotTransaction.FileName)});
            // The orchestrator kills this exact process. No exception unwinding,
            // editor shutdown, transaction finally or graceful save can run.
            Thread.Sleep(Timeout.Infinite);
            throw new InvalidOperationException("The interrupted writer must never return.");
        }
        public static void Run()
        {
            try {
                Need(Application.isBatchMode&&!EditorApplication.isPlaying,"Use a dedicated batch Editor mirror.");
                directory=Environment.GetEnvironmentVariable("MAESTRO_STORAGE_PROBE_DIRECTORY");
                Need(!string.IsNullOrWhiteSpace(directory)&&Path.IsPathRooted(directory),"Supply an absolute owned probe directory.");
                directory=Path.GetFullPath(directory);workspace=Path.Combine(directory,"workspace");
                owner=JObject.Parse(File.ReadAllText(Path.Combine(directory,"owner.json"),Utf8));id=(string)owner["id"];stage=(string)owner["stage"];first=(bool?)owner["firstSave"]==true;
                Need((int?)owner["version"]==1&&Guid.TryParseExact(id,"N",out _)&&Path.GetFileName(directory)==id&&Stages.Contains(stage),"Invalid crash-probe owner.");
                Need((string)owner["mirror"]==Path.GetFullPath(Path.Combine(Application.dataPath,"..")),"Wrong probe mirror.");
                Need((string)owner["recoveryStop"] is null or "recovered-room","Invalid recovery stop.");
                string mode=Environment.GetEnvironmentVariable("MAESTRO_STORAGE_PROBE_MODE");
                if(mode=="write")Write();
                else if(mode=="recoverStop"){
                    Need((string)owner["recoveryStop"]=="recovered-room"&&File.Exists(Path.Combine(directory,"writer-stopped.json")),"Writer termination was not recorded.");
                    RoomSnapshotTransaction.Capture(workspace,out _,point=>{if(point=="recovered-room")WaitForTermination("recovery-ready.json",point);});
                    throw new InvalidOperationException("The recovery stop was not reached.");
                }else if(mode=="verify")Verify();
                else throw new InvalidDataException("Unknown crash-probe mode.");
                EditorApplication.Exit(0);
            }catch(Exception error){UnityEngine.Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static void Write()
        {
            Need(!Directory.Exists(workspace)&&!File.Exists(Path.Combine(directory,"expected.json")),"Never reuse a writer directory.");
            var earlier=RoomSnapshotTransaction.Capture(workspace);
            if(!first){RoomSnapshotTransaction.Publish(workspace,earlier,Pair(1));earlier=RoomSnapshotTransaction.Capture(workspace);RoomSnapshotTransaction.Publish(workspace,earlier,Pair(2));}
            var before=RoomSnapshotTransaction.Capture(workspace);var after=Pair(3);
            bool committed=stage is "committed" or "room-backup" or "memory-backup";
            Publish("expected.json",new JObject{["version"]=1,["id"]=id,["committed"]=committed,["before"]=Files(before),["after"]=Files(after),
                ["expected"]=Files(committed?after:before),["backups"]=Files(first?earlier:committed?before:earlier)});
            RoomSnapshotTransaction.Publish(workspace,before,after,point=>{if(point==stage)WaitForTermination("writer-ready.json",point);});
            throw new InvalidOperationException("The publication stop was not reached.");
        }
        static void Verify()
        {
            Need(File.Exists(Path.Combine(directory,"writer-stopped.json")),"Writer termination was not recorded.");
            if((string)owner["recoveryStop"]!=null)Need(File.Exists(Path.Combine(directory,"recovery-stopped.json")),"Recovery termination was not recorded.");
            var expected=JObject.Parse(File.ReadAllText(Path.Combine(directory,"expected.json"),Utf8));Need((string)expected["id"]==id,"Wrong expected snapshot.");
            var actual=RoomSnapshotTransaction.Capture(workspace,out var recovery);
            Publish("recovery-observed.json",new JObject{["id"]=id,["actual"]=Files(actual),["recovered"]=recovery!=null,["committed"]=recovery?.Committed==true});
            Need(JToken.DeepEquals(Files(actual),expected["expected"]),"Recovery did not select the complete expected pair.");
            Need(Read(RoomStorage.FileName+".backup")== (string)expected["backups"]["room"]&&Read(ProgramMemoryStore.FileName+".backup")== (string)expected["backups"]["memory"],"Retained backups differ.");
            Need(!File.Exists(Path.Combine(workspace,RoomSnapshotTransaction.FileName)),"Recovered journal was not retired.");
            Need(stage=="before-journal"?recovery==null:recovery!=null&&recovery.Committed==(bool)expected["committed"],"Recovery phase differs.");
            var again=RoomSnapshotTransaction.Capture(workspace,out var second);
            Need(second==null&&again.Identity==actual.Identity,"Repeated startup is not idempotent.");
            Need(Read(RoomStorage.FileName+".backup")== (string)expected["backups"]["room"]&&Read(ProgramMemoryStore.FileName+".backup")== (string)expected["backups"]["memory"],"Repeated startup changed backups.");
            Publish("verified.json",new JObject{["version"]=1,["id"]=id,["stage"]=stage,["firstSave"]=first,["recoveryStop"]=owner["recoveryStop"],
                ["committed"]=(bool)expected["committed"],["identity"]=actual.Identity,["backupsMatched"]=true,["idempotent"]=true,["headset"]=false,["powerLoss"]=false,
                ["runtime"]="Unity Editor "+Application.unityVersion+" / "+Application.platform});
        }
    }
}
