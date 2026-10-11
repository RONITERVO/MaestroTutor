// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Accepted owned geometry, replaced only after preparation succeeds.</summary>
    public sealed class HeightFieldView:MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner {
        HeightFieldGeometry geometry;Mesh previewMesh;string encoded;Color tint=Color.white;
        RoomResourceOwner resourceOwner=new(null,null,"unscoped");
        internal void ConfigureResourceOwner(RoomResourceOwner owner){if(geometry!=null)throw new InvalidOperationException("Terrain geometry already has an owner");resourceOwner=owner??throw new ArgumentNullException(nameof(owner));}
        internal void Tint(Color color){tint=color;if(geometry!=null){geometry.Tint(color);RoomAppearanceView.VisualsChanged(this);}}
        public Collider Collision=>geometry?.Collision;
        internal RoomHeightField Accepted {get;private set;}
        public Bounds WorldBounds {
            get {if(geometry==null||!geometry.Surface||!geometry.Mesh)return default;var b=geometry.Mesh.bounds;var t=geometry.Surface.transform;var x=t.TransformVector(Vector3.right*b.extents.x);var y=t.TransformVector(Vector3.up*b.extents.y);var z=t.TransformVector(Vector3.forward*b.extents.z);return new Bounds(t.TransformPoint(b.center),2*new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));}
        }
        public Transform Surface=>geometry?.Surface?geometry.Surface.transform:null;
        internal void Preview(RoomHeightField field){
            if(geometry==null||!geometry.Surface)return;if(field==null){geometry.Surface.GetComponent<MeshFilter>().sharedMesh=geometry.Mesh;return;}
            if(!previewMesh)previewMesh=new Mesh{name="Sculpt gesture preview"};HeightFieldGeometry.Fill(previewMesh,field);geometry.Surface.GetComponent<MeshFilter>().sharedMesh=previewMesh;
        }
        public bool Apply(RoomHeightField[] fields)=>Apply(fields,null);
        internal bool Apply(RoomHeightField[] fields,RoomEditPreparation preparation){
            var data=fields?.Length==1?fields[0]:null;
            if(data!=null&&!data.Validate(out var error))throw new ArgumentException(error);
            string next=data==null?null:JsonUtility.ToJson(data);if(next==encoded)return false;
            if(data==null){ReleaseVisual();encoded=null;Accepted=null;return true;}
            var candidate=preparation?.TakeHeightField(resourceOwner.Target,data)??new HeightFieldGeometry(data,resourceOwner);
            var old=geometry;old?.SetActive(false);geometry=candidate;
            geometry.Attach(transform,tint);encoded=next;Accepted=data.Copy();Preview(null);old?.Dispose();RoomAppearanceView.VisualsChanged(this);return true;
        }
        void ReleaseVisual(){geometry?.Dispose();geometry=null;ArtResources.Release(previewMesh);previewMesh=null;}
        bool nativeResourcesReleased;
        void OnDestroy()=>ReleaseNativeResources();
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(nativeResourcesReleased)return;nativeResourcesReleased=true;ReleaseVisual();}
    }
}
