// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    /// <summary>Opt-in local-file adapter for the real app scene. Never compiled into a player.</summary>
    [InitializeOnLoad]
    public static class QuestRoomProbe
    {
        const string Key="Maestro.RoomProbe.Directory";
        static string directory,id,clientId;static RoomAgent agent;static GameObject root;
        static double started,next;static bool finishing;
        static QuestRoomProbe(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;}
        public static void Start()
        {
            if(!Application.isBatchMode||EditorApplication.isPlaying)throw new InvalidOperationException("Run the room probe in a dedicated batch Editor mirror.");
            directory=Environment.GetEnvironmentVariable("MAESTRO_ROOM_PROBE_DIRECTORY");
            if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory))throw new InvalidOperationException("Supply an absolute, fresh probe directory.");
            directory=Path.GetFullPath(directory);var owner=JObject.Parse(File.ReadAllText(Path.Combine(directory,"owner.json")));
            if((int?)owner["version"]!=1||!Guid.TryParseExact((string)owner["id"],"N",out _))throw new InvalidOperationException("Invalid probe owner receipt.");
            foreach(var name in new[]{"workspace","ready.json","state.json","terminal.json"})if(File.Exists(Path.Combine(directory,name))||Directory.Exists(Path.Combine(directory,name)))throw new InvalidOperationException("Probe directories cannot be reused.");
            SessionState.SetString(Key,directory);
            // This is a disposable batch mirror, never an interactive user's open scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode){
                directory=SessionState.GetString(Key,"");if(string.IsNullOrEmpty(directory))return;
                try{
                    id=(string)JObject.Parse(File.ReadAllText(Path.Combine(directory,"owner.json")))["id"];
                    started=EditorApplication.timeSinceStartup;next=0;finishing=false;
                    Application.runInBackground=true;Application.targetFrameRate=72;
                    root=new GameObject("Maestro probe app");root.SetActive(false);
                    var room=root.AddComponent<MaestroRoom>();room.ProbeWorkspaceDirectory=Path.Combine(directory,"workspace");root.SetActive(true);
                    agent=root.GetComponent<RoomAgent>();
                    Publish("ready.json",new JObject {["version"]=1,["id"]=id,["boundary"]="Real Unity app in Editor; no Android WebView, headset or real room scan"});
                }catch(Exception e){Fail(e);}
            }else if(state==PlayModeStateChange.EnteredEditMode){
                directory=SessionState.GetString(Key,"");if(string.IsNullOrEmpty(directory))return;
                int exit=SessionState.GetInt(Key+".exit",1);SessionState.EraseString(Key);SessionState.EraseInt(Key+".exit");EditorApplication.Exit(exit);
            }
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||string.IsNullOrEmpty(directory)||finishing)return;
            if(EditorApplication.timeSinceStartup-started>900){Fail(new TimeoutException("Room probe exceeded fifteen minutes."));return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.1;
            try{
                var input=Path.Combine(directory,"request.json");
                if(File.Exists(input)){
                    byte[] bytes;
                    using(var stream=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                        if(stream.Length>40000)throw new InvalidDataException("Oversized room-probe request.");
                        using var data=new MemoryStream();var buffer=new byte[4096];int read;
                        while((read=stream.Read(buffer,0,buffer.Length))>0){if(data.Length+read>40000)throw new InvalidDataException("Oversized room-probe request.");data.Write(buffer,0,read);}bytes=data.ToArray();
                    }
                    var request=JObject.Parse(new UTF8Encoding(false,true).GetString(bytes));
                    if((int?)request["version"]!=1||(string)request["id"]!=id)throw new InvalidDataException("Wrong probe session.");
                    string operation=(string)request["operation"];
                    if(operation=="stop"){Finish(0);return;}
                    if(operation!="exchange"||request["snapshot"] is not JObject)throw new InvalidDataException("Unknown probe operation.");
                    agent.Receive(request["snapshot"].ToString(Newtonsoft.Json.Formatting.None));
                    clientId=(string)request["snapshot"]["clientId"];
                }
                if(agent)Publish("state.json",new JObject {["version"]=1,["id"]=id,["clientId"]=clientId,["state"]=JObject.Parse(RoomAgentWire.Serialize(agent.Observe()))});
            }catch(InvalidDataException e){Fail(e);}
            catch(IOException){ /* Atomic replacement can briefly contend on Windows. The bounded client times out. */ }
            catch(Exception e){Fail(e);}
        }
        static void Publish(string name,JObject value)
        {
            string path=Path.Combine(directory,name),pending=path+".pending";
            File.WriteAllText(pending,value.ToString(Newtonsoft.Json.Formatting.None),new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
        }
        static void Fail(Exception error){Debug.LogException(error);Finish(1,error.GetType().Name+": "+error.Message);}
        static void Finish(int code,string error=null)
        {
            if(finishing)return;finishing=true;SessionState.SetInt(Key+".exit",code);
            try{Publish("terminal.json",new JObject {["version"]=1,["id"]=id,["exitCode"]=code,["error"]=error});}
            finally{EditorApplication.ExitPlaymode();}
        }
    }
}
