// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>The editor ticks this even while its missing-anchor object is inactive.</summary>
    public sealed class ScannedDrawingView:MonoBehaviour
    {
        RoomEditor editor;string id;ScanDrawingAnchor binding;float width,height;ScannedSurface previous;
        public bool Visible {get;private set;}
        public string Status {get;private set;}="Load the saved scanned room";
        internal string BindingSignature=>binding==null?"":JsonUtility.ToJson(binding);
        internal void Apply(RoomEditor owner,RoomObjectData data){editor=owner;id=data.id;binding=data.scanAnchors[0].Copy();width=data.surfaces[0].width;height=data.surfaces[0].height;Sync();}
        internal void Sync()
        {
            var scan=editor&&editor.PhysicsWorld?editor.PhysicsWorld.GetComponent<ScannedRoom>():null;
            string error="The scanned-room service is unavailable";ScannedSurface surface=null;Vector3 position=default;Quaternion rotation=Quaternion.identity;
            bool available=scan&&scan.ResolveDrawing(editor.transform,binding,width,height,null,out surface,out position,out rotation,out error);
            if(!available){if(Visible)editor.GetComponent<SpatialDrawing>()?.InterruptSurface(id);Visible=false;previous=null;Status=error;if(gameObject.activeSelf)gameObject.SetActive(false);return;}
            if(Visible&&previous!=null&&!previous.Same(surface))editor.GetComponent<SpatialDrawing>()?.InterruptSurface(id);
            previous=surface;Visible=true;Status="Ink follows its exact scanned plane; check physical alignment";
            var worldPosition=editor.transform.TransformPoint(position);var worldRotation=editor.transform.rotation*rotation;
            bool moved=!transform.position.Equals(worldPosition)||!transform.rotation.Equals(worldRotation)||transform.localScale!=Vector3.one;
            if(moved){transform.SetPositionAndRotation(worldPosition,worldRotation);transform.localScale=Vector3.one;GetComponent<RigidRoomItem>()?.Teleported();}
            if(!gameObject.activeSelf)gameObject.SetActive(true);
        }
    }
}
