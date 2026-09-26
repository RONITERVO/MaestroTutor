// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Book;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class RoomAgentCommand
    {
        public string action, target, reference, name, kind;
        public Vector3 position;
        public bool atPosition;
        public float scale=1;
        public Color color=Color.white;
        public RoomRecipe recipe;
    }
    [Serializable] public sealed class RoomAgentRequest
    {
        public int version, sequence, sceneRevision;
        public string session;
        public RoomAgentCommand[] commands;
    }
    [Serializable] public sealed class RoomAgentSnapshot { public string session; public RoomAgentRequest request; }
    [Serializable] public sealed class RoomAgentObject
    {
        public string id,name,kind;
        public Vector3 position;
        public float scale;
        public Color color;
        public bool animated;
    }
    [Serializable] public sealed class RoomAgentState
    {
        public int version=1,revision,sceneRevision,ack;
        public string session,status,selectedId;
        public bool ok,canUndo,canRedo,physicsRunning;
        public RoomAgentObject[] objects;
        public string[] created=Array.Empty<string>();
    }
    /// <summary>Every accepted request has one journal transaction and an actual native receipt.</summary>
    public sealed class RoomAgentExecutor
    {
        readonly RoomEditor editor;
        public RoomAgentExecutor(RoomEditor source) => editor=source;
        public bool Execute(RoomAgentRequest request,out string status,out string[] created)
        {
            created=Array.Empty<string>(); status="Invalid room request";
            if(request == null || request.version != 1 || request.commands == null || request.commands.Length<1 || request.commands.Length>8) return false;
            if(request.sceneRevision != editor.Revision) {status="The scene changed; inspect the new state before retrying."; return false;}
            if(editor.AnyHeld) {status="Release the held object before editing."; return false;}
            var commands=request.commands;
            if(commands.Length==1 && (commands[0]?.action=="undo" || commands[0]?.action=="redo"))
            {
                bool undo=commands[0].action=="undo";
                if(undo ? !editor.CanUndo : !editor.CanRedo) {status="There is no edit to "+commands[0].action; return false;}
                if(undo) editor.Undo(); else editor.Redo(); status=editor.Status; return status=="Undone" || status=="Redone";
            }
            var changes=new Dictionary<string,RoomObjectData>(); var removed=new HashSet<string>(); var refs=new Dictionary<string,string>();
            var added=new List<string>();
            foreach(var command in commands)
            {
                if(command==null) return false;
                if(command.action=="create")
                {
                    if(!RoomRecipe.ValidId(command.reference) || refs.ContainsKey(command.reference) || command.name==null || command.name.Length>80 || command.name.Any(char.IsControl)) return false;
                    RoomObjectKind kind;
                    switch(command.kind) {
                        case "block": kind=RoomObjectKind.Block;break; case "ball": kind=RoomObjectKind.Ball;break;
                        case "cylinder": kind=RoomObjectKind.Cylinder;break; case "recipe": case "boxRobot": kind=RoomObjectKind.Assembly;break;
                        default: status="This creation type is unavailable";return false;
                    }
                    string id=Guid.NewGuid().ToString("N"); refs.Add(command.reference,id); added.Add(id);
                    changes[id]=new RoomObjectData { id=id,name=command.name,kind=kind,position=command.atPosition ? command.position : editor.CreationPosition,
                        scale=command.scale,color=command.color,physics=kind==RoomObjectKind.Ball ? Interaction.ItemPhysics.Bouncy : Interaction.ItemPhysics.Fixed,
                        recipe=command.kind=="boxRobot" ? RecipeTemplates.BoxRobot(true) : command.kind=="recipe" ? command.recipe?.Copy() : null };
                    continue;
                }
                string target=command.target; if(target!=null && refs.TryGetValue(target,out var mapped)) target=mapped;
                if(target==null || removed.Contains(target)) {status="The target no longer exists"; return false;}
                var data=changes.TryGetValue(target,out var pending) ? pending : editor.Read(target);
                if(data==null) {status="The target no longer exists";return false;}
                switch(command.action)
                {
                    case "move": data.position=command.position; break;
                    case "resize": data.scale=command.scale; break;
                    case "paint": if(data.IsBuiltIn) {status="Painting the book or tutor is unavailable";return false;} data.color=command.color;break;
                    case "recipe": if(data.kind!=RoomObjectKind.Assembly || command.recipe==null) return false; data.recipe=command.recipe.Copy();break;
                    case "delete": if(data.IsBuiltIn) {status="The book and Maestro must remain in the room";return false;} removed.Add(target);changes.Remove(target);continue;
                    default: status="This room action is unavailable"; return false;
                }
                changes[target]=data;
            }
            // Validate the entire candidate before touching any scene object or interrupting authoring.
            var candidate=editor.Snapshot(); candidate.objects=candidate.objects.Where(x=>!removed.Contains(x.id) && !changes.ContainsKey(x.id)).Concat(changes.Values).ToArray();
            if(!candidate.Validate(out status)) return false;
            if(!editor.ApplyAgentEdit(request.sceneRevision,changes.Values.ToArray(),removed.ToArray(),out status)) return false;
            created=added.Where(id=>!removed.Contains(id)).ToArray(); status="Completed "+commands.Length+" room actions. One Undo restores the preceding scene edit."; return true;
        }
    }
    public sealed class RoomAgent : MonoBehaviour
    {
        RoomEditor editor;
        NativeBookBrowser browser;
        RoomAgentExecutor executor;
        string session=Guid.NewGuid().ToString("N"),status="Room actions ready";
        int ack,revision;
        float next;
        bool connected,ok=true;
        string[] created=Array.Empty<string>();
        public void Initialize(RoomEditor source,NativeBookBrowser book) {editor=source;browser=book;executor=new RoomAgentExecutor(source);}
        void Update()
        {
            if(!editor || !browser) return;
            if(browser.Snapshot==null)
            {
                if(connected) {session=Guid.NewGuid().ToString("N");ack=0;revision=0;connected=false;created=Array.Empty<string>();status="Room session reopened";}
                return;
            }
            connected=true;
            if(Time.unscaledTime<next) return;next=Time.unscaledTime+.25f;
            string json=browser.ReadRoomAgentSnapshot();
            if(!string.IsNullOrEmpty(json) && json.Length<=32768)
            {
                try {
                    var snapshot=JsonUtility.FromJson<RoomAgentSnapshot>(json); var request=snapshot?.request;
                    if(snapshot?.session==session && request!=null && request.session==session && request.sequence>ack && request.sequence==ack+1)
                    { ack=request.sequence; ok=executor.Execute(request,out status,out created); }
                } catch(ArgumentException) { /* Invalid or partial messages never execute. */ }
            }
            var state=new RoomAgentState { session=session,revision=++revision,sceneRevision=editor.Revision,ack=ack,ok=ok,status=status,created=created,
                selectedId=editor.SelectedId,canUndo=editor.CanUndo,canRedo=editor.CanRedo,physicsRunning=editor.PhysicsWorld && editor.PhysicsWorld.Running,
                objects=editor.Snapshot().objects.Select(x=>new RoomAgentObject {id=x.id,name=x.name??x.kind.ToString(),kind=x.kind.ToString(),position=x.position,scale=x.scale,color=x.color,animated=x.recipe?.playing??false}).ToArray() };
            browser.PublishRoomAgentState(JsonUtility.ToJson(state));
        }
    }
}
