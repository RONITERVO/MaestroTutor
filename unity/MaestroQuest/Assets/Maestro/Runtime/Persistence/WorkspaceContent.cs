// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    /// <summary>Generation-scoped content. The XR rig, book/browser and room origin outlive it.</summary>
    public sealed class WorkspaceContent:MonoBehaviour
    {
        readonly TaskCompletionSource<bool> destroyed=new(TaskCreationOptions.RunContinuationsAsynchronously);Task retirement;
        bool detached;RoomInteraction room;RoomItem bookItem;BookPointerRouter router;BookControllerInput input;
        public RoomEditor Editor {get;private set;}
        public RuleWorkshop Rules {get;private set;}
        public MovementControls Controls {get;private set;}
        internal void Build(RoomInteraction interaction,RoomItem bookOwner,NativeBookBrowser browser,BookPointerRouter pointer,BookControllerInput controller,
            RoomPhysicsWorld physics,RoomNavigation navigation,ScannedRoom scan,VirtualRoomView virtualView,Func<bool> headTracked,
            string applicationData,string directory,string receiptDirectory,RoomRuntimeGate gate)
        {
            room=interaction;bookItem=bookOwner;router=pointer;input=controller;var content=gameObject;
            var avatar = new GameObject("Full body Maestro");
            avatar.transform.SetParent(content.transform, false);
            avatar.transform.localPosition = WorkspaceDefaults.MaestroPosition;
            avatar.transform.localRotation = WorkspaceDefaults.MaestroRotation;
            avatar.AddComponent<MaestroAvatar>().Browser = browser;
            var avatarHandle = avatar.AddComponent<CapsuleCollider>(); avatarHandle.center = new Vector3(0,.85f,0); avatarHandle.height = 1.7f; avatarHandle.radius = .25f;
            avatar.layer = RoomPhysicsLayers.Environment;
            var avatarItem = avatar.AddComponent<RoomItem>(); avatarItem.Configure(new Collider[] { avatarHandle }, .3f, 1.5f); room.Register(avatarItem);
            Editor = content.AddComponent<RoomEditor>(); var editor=Editor; editor.Initialize(room,bookItem,avatarItem,directory,physics,gate,receiptDirectory);
            router.Editor = editor; input.Editor = editor;
            var drawing = content.AddComponent<SpatialDrawing>(); drawing.Editor = editor; input.Drawing = drawing;
            var tray = new GameObject("Creation tools"); tray.transform.SetParent(content.transform,false);
            tray.transform.localPosition = new Vector3(.87f,.98f,.9f); tray.transform.localRotation = Quaternion.Euler(24,35,0);
            tray.AddComponent<RoomToolTray>().Build(editor,room);
            var workshop = content.AddComponent<AnimationWorkshop>(); workshop.Initialize(editor);
            var movement = avatar.AddComponent<AvatarSpatialMotion>(); movement.Initialize(editor,workshop,room,navigation,headTracked);
            var animationTools = new GameObject("Animation tools"); animationTools.transform.SetParent(content.transform,false);
            animationTools.transform.localPosition = new Vector3(.87f,.55f,.9f); animationTools.transform.localRotation = Quaternion.Euler(40,35,0);
            animationTools.AddComponent<AnimationTools>().Build(workshop,room);
            Rules=content.AddComponent<RuleWorkshop>();var rules=Rules;rules.Initialize(editor);
            content.AddComponent<RoomRules>().Initialize(rules,editor,workshop,browser,room,input);
            var ruleTools = new GameObject("Behaviour rules"); ruleTools.transform.SetParent(content.transform,false);
            ruleTools.transform.localPosition = new Vector3(-.95f,.68f,.75f); ruleTools.transform.localRotation = Quaternion.Euler(28,-35,0);
            ruleTools.AddComponent<RuleTools>().Build(rules,room);
            var imports = content.AddComponent<ImportWorkshop>(); imports.Initialize(editor, content.GetComponent<AnimationWorkshop>());
            content.AddComponent<LibraryBookController>().Initialize(editor,imports,rules,browser);
            var importTools = new GameObject("Model import tools"); importTools.transform.SetParent(content.transform, false);
            importTools.transform.localPosition = new Vector3(1.25f, .80f, 1.65f); importTools.transform.localRotation = Quaternion.Euler(20, 55, 0);
            importTools.AddComponent<ImportTools>().Build(imports, room);
            var physicsTools = new GameObject("Room physics tools"); physicsTools.transform.SetParent(content.transform,false);
            physicsTools.transform.localPosition = new Vector3(-1.2f,1.0f,1.55f); physicsTools.transform.localRotation = Quaternion.Euler(20,-45,0);
            router.Placement = physicsTools.AddComponent<PhysicsTools>(); router.Placement.Build(editor,physics,scan,room);
            var movementTools = new GameObject("Maestro movement tools"); movementTools.transform.SetParent(content.transform,false);
            movementTools.transform.localPosition = new Vector3(.85f,.38f,1.25f); movementTools.transform.localRotation = Quaternion.Euler(40,25,0);
            movementTools.AddComponent<AvatarSpatialTools>().Build(movement,editor,workshop,content.GetComponent<RoomRules>(),room);
            Controls=content.AddComponent<MovementControls>();var movementControls=Controls;
            movementControls.Initialize(room,editor,workshop,movement,content.GetComponent<RoomRules>(),rules,input,virtualView,headTracked);
            var controlTools=new GameObject("Movement and controller bindings"); controlTools.transform.SetParent(content.transform,false);
            controlTools.transform.localPosition=new Vector3(-1.15f,.4f,1.25f); controlTools.transform.localRotation=Quaternion.Euler(40,-30,0);
            controlTools.AddComponent<MovementTools>().Build(movementControls,room);
        }
        internal void Detach(bool preserveBookLock=false)
        {
            if(detached)return;detached=true;
            GetComponent<LibraryBookController>()?.DetachWorkspace();
            if(router)router.DetachContent(Editor);
            if(input&&input.Editor==Editor){input.Editor=null;input.Drawing=null;}
            if(room)foreach(var item in GetComponentsInChildren<RoomItem>(true))room.Unregister(item);
            if(Editor&&!preserveBookLock){room?.DetachWrites(Editor.WriteGate);bookItem?.DetachWrites(Editor.WriteGate);}
        }
        internal Task Retire(bool preserveBookLock=false)
        {
            if(retirement!=null)return retirement;
            IDisposable activity=null;
            try{if(Editor){activity=Editor.RuntimeGate.Hold("Closing the previous workspace");Editor.PrepareAgentEdit();}}
            catch(Exception){Debug.LogWarning("Previous workspace authoring could not finish while closing.");}
            finally{
                var writers=Editor?.WriteGate.Retire()??Task.CompletedTask;
                if(this){Detach(preserveBookLock);gameObject.SetActive(false);Destroy(gameObject);}else destroyed.TrySetResult(true);
                retirement=FinishRetirement(writers,activity);
            }
            return retirement;
        }
        async Task FinishRetirement(Task writers,IDisposable activity)
        {
            try{await Task.WhenAll(writers,destroyed.Task);await Task.Yield();}
            finally{activity?.Dispose();}
        }
        void OnDestroy(){try{Detach();}finally{destroyed.TrySetResult(true);}}

    }
}
