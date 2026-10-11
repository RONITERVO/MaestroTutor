// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed class MaterialToolContentsView:MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner {
        const int Sides=16,Rings=4;
        RoomMaterialStore accepted,preview;SculptTip tip;
        GameObject heap;Mesh mesh;Material material;Transform anchor;
        internal bool Visible=>heap&&heap.activeInHierarchy;
        public void Apply(SculptTip definition,RoomMaterialStore stored){tip=definition?.Copy();accepted=stored?.Copy();Refresh();}
        internal void Preview(RoomMaterialStore value){preview=value?.Copy();Refresh();}
        void Refresh(){
            var data=preview??accepted;anchor=tip==null?null:string.IsNullOrEmpty(tip.part)?transform:GetComponent<RecipeObject>()?.Part(tip.part);
            if(tip?.IsMaterial!=true||data==null||data.amountLitres<=0||!anchor){if(heap)heap.SetActive(false);return;}
            if(!heap){heap=new GameObject("Carried material preview");mesh=new Mesh{name="Bounded carried material heap"};heap.AddComponent<MeshFilter>().sharedMesh=mesh;material=IllustratedMaterials.Create(data.color,0);heap.AddComponent<MeshRenderer>().sharedMaterial=material;}
            heap.transform.SetParent(anchor,false);heap.transform.SetLocalPositionAndRotation(tip.position,tip.rotation);heap.SetActive(true);material.SetColor("_Color",data.color);
            // This bounded icon shows fill fraction, not a second physical volume or per-grain simulation.
            float fraction=(float)(data.amountLitres/data.capacityLitres),radius=Mathf.Clamp(tip.radius,.01f,.15f),height=Mathf.Min(.08f,radius)*Mathf.Pow(fraction,1f/3);
            var vertices=new Vector3[1+Sides*Rings];var triangles=new int[Sides*3+(Rings-1)*Sides*6];vertices[0]=Vector3.forward*height;
            for(int ring=1;ring<=Rings;ring++)for(int side=0;side<Sides;side++){float r=(float)ring/Rings,angle=side*Mathf.PI*2/Sides;vertices[1+(ring-1)*Sides+side]=new Vector3(Mathf.Cos(angle)*radius*r,Mathf.Sin(angle)*radius*r,height*Mathf.Sqrt(1-r*r));}
            int at=0;for(int side=0;side<Sides;side++){triangles[at++]=0;triangles[at++]=1+side;triangles[at++]=1+(side+1)%Sides;}
            for(int ring=0;ring<Rings-1;ring++)for(int side=0;side<Sides;side++){int a=1+ring*Sides+side,b=1+ring*Sides+(side+1)%Sides,c=a+Sides,d=b+Sides;triangles[at++]=a;triangles[at++]=c;triangles[at++]=b;triangles[at++]=b;triangles[at++]=c;triangles[at++]=d;}
            mesh.Clear();mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        bool nativeResourcesReleased;
        void OnDestroy()=>ReleaseNativeResources();
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(nativeResourcesReleased)return;nativeResourcesReleased=true;ArtResources.Release(mesh);ArtResources.Release(material);if(heap)Destroy(heap);}
    }
}
