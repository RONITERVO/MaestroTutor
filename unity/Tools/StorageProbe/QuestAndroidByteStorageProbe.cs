// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID && !UNITY_EDITOR && MAESTRO_QUEST_DEVELOPMENT
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Diagnostics
{
    public static partial class QuestAndroidStorageProbe
    {
        [DllImport("maestro_storage_fault")]static extern int maestro_fault_arm(string root,string name,int mode);
        [DllImport("maestro_storage_fault")]static extern int maestro_fault_disarm();
        [DllImport("maestro_storage_fault")]static extern IntPtr maestro_fault_detail();
        static JObject Inventory()=>new(Directory.GetFiles(workspace).OrderBy(x=>x).Select(x=>new JProperty(Path.GetFileName(x),Wire(File.ReadAllBytes(x)))));
        static string Target()=>stage switch {
            "before-journal" or "memory"=>RoomSnapshotTransaction.FileName,
            "prepared"=>RoomStorage.FileName,"room"=>ProgramMemoryStore.FileName,
            "committed"=>RoomStorage.FileName+".backup","room-backup"=>ProgramMemoryStore.FileName+".backup",
            _=>throw new InvalidDataException("Invalid byte-fault stage.")};
        static void RunByteProbe(string runMode)
        {
            string fault=(string)owner["ioFault"];
            Need(fault is "block" or "enospc" or "stress","Invalid I/O fault.");
            if(fault=="stress"){Need(runMode=="write","Invalid stress mode.");Stress();Application.Quit(0);return;}
            _=Target();
            if(runMode=="write"){
                Need(!Directory.Exists(workspace),"Never reuse a byte-probe workspace.");
                var empty=RoomSnapshotTransaction.Capture(workspace);
                var earlier=Pair(1);RoomSnapshotTransaction.Publish(workspace,empty,earlier);
                RoomSnapshotTransaction.Publish(workspace,RoomSnapshotTransaction.Capture(workspace),Pair(2));
                var before=RoomSnapshotTransaction.Capture(workspace);var after=Pair(3);
                bool committed=stage is "committed" or "room-backup";
                Publish("expected.json",new JObject{["id"]=id,["before"]=Files(before),["after"]=Files(after),
                    ["expected"]=Files(committed?after:before),["backups"]=Files(committed?before:earlier),["target"]=Target(),["committed"]=committed});
                Exception observed=null;int errors=0;
                try {
                    RoomSnapshotTransaction.Publish(workspace,before,after,point=>{
                        if(point==stage)Need(maestro_fault_arm(workspace,Target(),fault=="block"?1:2)==1,"Native fault did not arm.");
                    });
                }catch(IOException error){observed=error;}
                finally{errors=maestro_fault_disarm();}
                Need(fault=="enospc"&&observed!=null&&errors>0,"Native write failure was not observed: "+Marshal.PtrToStringAnsi(maestro_fault_detail()));
                Publish("write-failed.json",new JObject{["id"]=id,["type"]=observed.GetType().FullName,["hresult"]=observed.HResult,
                    ["nativeEnospcCount"]=errors,["files"]=Inventory()});
            }else Need(runMode=="verify"&&fault=="block"&&File.Exists(Path.Combine(directory,"writer-stopped.json")),"Invalid byte-probe continuation.");
            if(fault=="block"){
                var before=Inventory();bool refused=false;
                try{RoomSnapshotTransaction.Capture(workspace);}catch(InvalidDataException){refused=true;}
                Need(refused&&JToken.DeepEquals(before,Inventory()),"Partial staging must be preserved for explicit recovery.");
                bool refusedAgain=false;try{RoomSnapshotTransaction.Capture(workspace);}catch(InvalidDataException){refusedAgain=true;}
                Need(refusedAgain&&JToken.DeepEquals(before,Inventory()),"Repeated startup changed interrupted evidence.");
                Publish("verified.json",new JObject{["id"]=id,["ioFault"]=fault,["stage"]=stage,["readOnlyRefusal"]=true,["files"]=before,["headset"]=true});
            }else{
                var expected=JObject.Parse(File.ReadAllText(Path.Combine(directory,"expected.json")));
                var actual=RoomSnapshotTransaction.Capture(workspace,out _);
                Need(JToken.DeepEquals(Files(actual),expected["expected"]),"I/O recovery did not choose the complete expected pair.");
                Need(Read(RoomStorage.FileName+".backup")== (string)expected["backups"]["room"]&&Read(ProgramMemoryStore.FileName+".backup")== (string)expected["backups"]["memory"],"I/O recovery backups differ.");
                Need(RoomSnapshotTransaction.Capture(workspace,out var again).Identity==actual.Identity&&again==null,"I/O recovery was not idempotent.");
                Need(!Directory.GetFiles(workspace).Any(x=>x.Contains(".snapshot."))&&!File.Exists(Path.Combine(workspace,RoomSnapshotTransaction.FileName)),"I/O staging remains.");
                Publish("verified.json",new JObject{["id"]=id,["ioFault"]=fault,["stage"]=stage,["recovered"]=true,["idempotent"]=true,["files"]=Inventory(),["headset"]=true});
            }
            Application.Quit(0);
        }
        static void Stress()
        {
            Need(!Directory.Exists(workspace),"Never reuse a stress workspace.");
            var timer=System.Diagnostics.Stopwatch.StartNew();var current=RoomSnapshotTransaction.Capture(workspace);
            const int count=256;
            for(int i=1;i<=count;i++){
                var previous=current;var next=Pair(i);RoomSnapshotTransaction.Publish(workspace,current,next);
                current=RoomSnapshotTransaction.Capture(workspace,out var recovered);
                Need(current.Identity==next.Identity&&recovered==null,"Repeated save did not publish exactly.");
                if(i>1)Need(Read(RoomStorage.FileName+".backup")==Wire(previous.Room)&&Read(ProgramMemoryStore.FileName+".backup")==Wire(previous.Memory),"Repeated save backups differ.");
            }
            Publish("verified.json",new JObject{["id"]=id,["ioFault"]="stress",["saves"]=count,["seconds"]=timer.Elapsed.TotalSeconds,["headset"]=true,["files"]=Inventory()});
        }
    }
}
#endif