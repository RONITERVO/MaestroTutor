// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Presentation of saved bindings. Never edits geometry, collision,
    /// audio or browser-page materials. Sources and shared variants have separate owners.</summary>
    internal sealed class RoomAppearanceView:MonoBehaviour
    {
        sealed class Rendered {internal Renderer Renderer;internal Material[] Original,Applied;}
        readonly List<Rendered> rendered=new();
        readonly List<AppearanceMaterials.Lease> leases=new();
        readonly List<VisibilityMaterials.Lease> visibilityLeases=new();
        AppearanceImages images;
        readonly Dictionary<string,AppearanceImages.Lease> imageLeases=new(StringComparer.Ordinal);
        internal void ConfigureImages(AppearanceImages value){if(images==value)return;if(images)images.Changed-=ImagesChanged;images=value;if(images)images.Changed+=ImagesChanged;refreshPending=true;}
        void ImagesChanged(string hash){if(imageLeases.ContainsKey(hash))refreshPending=true;}
        internal string ImageState(string hash){if(!isActiveAndEnabled||!images||!imageLeases.ContainsKey(hash))return "unloaded";var state=images.State(hash);return state=="ready"&&refreshPending?"loading":state;}
        Texture Image(AppearanceStyle style){if(style.patternMode!="image")return null;if(!images)return null;if(!imageLeases.TryGetValue(style.imageHash,out var lease)){lease=images.Acquire(style.imageHash);imageLeases.Add(style.imageHash,lease);}return lease.Texture;}
        VisibilityState visibility;
        bool refreshPending;
        internal bool PointerVisible=>!isActiveAndEnabled||visibility==null||visibility.Opacity>0;
        internal static void VisualsChanged(Component source){var owner=source.GetComponentInParent<RoomItem>();var view=owner?owner.GetComponent<RoomAppearanceView>():null;if(view)view.refreshPending=true;}
        void LateUpdate(){if(refreshPending)Refresh();}
        RoomObjectKind kind;
        AppearanceBinding[] bindings=Array.Empty<AppearanceBinding>();
        Dictionary<string,RoomAppearance> definitions=new();
        MaestroAvatar avatar;
        internal void Configure(RoomObjectData value,IEnumerable<RoomAppearance> appearances) {
            kind=value.kind;bindings=value.appearanceBindings.Select(x=>x.Copy()).ToArray();
            var used=bindings.Select(x=>x.appearanceId).ToHashSet();definitions=appearances.Where(x=>used.Contains(x.id)).ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            if(!avatar){avatar=GetComponent<MaestroAvatar>();if(avatar)avatar.ModelChanged+=Refresh;}
            Refresh();
        }
        internal void ConfigureLayer(RoomObjectData value,IEnumerable<RoomAppearance> appearances,VisibilityState layer){visibility=layer;Configure(value,appearances);}
        internal void ConfigureVisibility(VisibilityState value) {
            if(ReferenceEquals(visibility,value))return;
            visibility=value;Refresh();
        }
        internal void Refresh() {
            refreshPending=false;
            var previous=leases.ToArray();leases.Clear();
            var previousImages=imageLeases.Values.ToArray();imageLeases.Clear();
            var previousVisibility=visibilityLeases.ToArray();visibilityLeases.Clear();Restore();
            try {
            if(!isActiveAndEnabled||bindings.Length==0&&visibility==null)return;
            var owner=GetComponent<RoomItem>();var recipe=GetComponent<RecipeObject>();
            var root=bindings.FirstOrDefault(b=>b.kind=="root");
            foreach(var renderer in GetComponentsInChildren<Renderer>(true)) {
                if(renderer.GetComponent<Book.BookPageTarget>()||renderer.GetComponentInParent<RoomItem>()!=owner)continue;
                bool appearanceEligible=!renderer.GetComponent<PencilMarks>()||kind==RoomObjectKind.Drawing&&renderer.transform==transform;
                string part=recipe?recipe.AppearancePart(renderer):null;
                var model=renderer.GetComponentInParent<ImportedModel>();
                string hash=model&&model.Ready?model.AssetHash:null;
                var original=renderer.sharedMaterials;var applied=(Material[])original.Clone();bool changed=false;
                for(int i=0;i<original.Length;i++) {
                    var source=original[i];if(!source||source.shader.name!="Maestro/Watercolor")continue;
                    var binding=root;
                    if(part!=null)binding=bindings.FirstOrDefault(b=>b.kind=="part"&&b.partId==part)??binding;
                    if(hash!=null&&int.TryParse(source.GetTag("MaestroMaterialIndex",false,""),out int index))
                        binding=bindings.FirstOrDefault(b=>b.kind=="material"&&b.modelHash==hash&&b.materialIndex==index)??binding;
                    if(appearanceEligible&&binding!=null&&definitions.TryGetValue(binding.appearanceId,out var definition)) {
                        var basePigment=binding.kind=="material"&&model&&model.Ready?model.Instance.GetComponent<PencilModelStyle>()?.ImportedBaseColor(binding.materialIndex):null;
                        var lease=AppearanceMaterials.Acquire(source,binding.Effective(definition),basePigment,Image(definition.style));leases.Add(lease);applied[i]=lease.Material;changed=true;
                    }
                    if(visibility!=null) {
                        var lease=VisibilityMaterials.Acquire(applied[i],visibility);visibilityLeases.Add(lease);applied[i]=lease.Material;changed=true;
                    }
                }
                if(changed){rendered.Add(new Rendered{Renderer=renderer,Original=original,Applied=applied});renderer.sharedMaterials=applied;}
            }
            } finally {foreach(var lease in previousVisibility)lease.Dispose();foreach(var lease in previous)lease.Dispose();foreach(var lease in previousImages)lease.Dispose();}
        }
        void Restore() {
            foreach(var entry in rendered)if(entry.Renderer) {
                var current=entry.Renderer.sharedMaterials;
                for(int i=0;i<Math.Min(current.Length,entry.Applied.Length);i++)if(current[i]==entry.Applied[i])current[i]=entry.Original[i];
                entry.Renderer.sharedMaterials=current;
            }
            rendered.Clear();
        }
        void Clear(){Restore();foreach(var lease in visibilityLeases)lease.Dispose();visibilityLeases.Clear();foreach(var lease in leases)lease.Dispose();leases.Clear();foreach(var lease in imageLeases.Values)lease.Dispose();imageLeases.Clear();}
        void OnEnable(){Refresh();}
        void OnDisable(){Clear();}
        void OnDestroy(){if(images)images.Changed-=ImagesChanged;if(avatar)avatar.ModelChanged-=Refresh;Clear();}
    }
}
