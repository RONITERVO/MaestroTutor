// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal static bool PaintAppearance(RoomObjectData data,Color color,out string error) {
            error="Use a finite opaque RGB colour; use appearance rendering for opacity";
            if(!float.IsFinite(color.r)||!float.IsFinite(color.g)||!float.IsFinite(color.b)||color.r<0||color.r>1||color.g<0||color.g>1||color.b<0||color.b>1||color.a!=1)return false;
            var binding=data.appearanceBindings.FirstOrDefault(b=>b.kind=="root");
            if(binding==null)data.color=color;else {binding.tint="#"+ColorUtility.ToHtmlStringRGB(color);data.color=Color.white;}
            error=null;return true;
        }
        internal RoomRecipe RecipeForEditing(RoomObjectData data) {
            var recipe=data.recipe?.Copy();if(recipe==null)return null;
            foreach(var binding in data.appearanceBindings.Where(b=>b.kind=="part")) {
                var part=recipe.parts.FirstOrDefault(p=>p.id==binding.partId);var definition=ReadAppearance(binding.appearanceId);
                if(part!=null&&definition!=null)ColorUtility.TryParseHtmlString(binding.Effective(definition).tint,out part.color);
            }
            return recipe;
        }
        internal bool ApplyRecipeAppearance(RoomObjectData data,RoomRecipe recipe,out string error) {
            error="Use a valid recipe";if(recipe==null||!recipe.Validate(out error))return false;var observed=RecipeForEditing(data);
            foreach(var binding in data.appearanceBindings.Where(b=>b.kind=="part")) {
                var part=recipe.parts.FirstOrDefault(p=>p.id==binding.partId);if(part==null)continue;
                var old=observed.parts.FirstOrDefault(p=>p.id==binding.partId);var definition=ReadAppearance(binding.appearanceId);
                if(definition==null){error="The bound appearance is missing";return false;}
                if(old==null||part.color!=old.color)binding.tint="#"+ColorUtility.ToHtmlStringRGB(part.color);
                if(old!=null&&definition.style.patternMode!="inherit"&&JsonUtility.ToJson(part.pattern)!=JsonUtility.ToJson(old.pattern)){error="This part uses an appearance pattern; edit its appearance or unbind it before editing the source pattern";return false;}
                part.color=Color.white;
            }
            data.recipe=recipe;return true;
        }
        public RoomAppearance[] Appearances()=>journal.AppearanceSnapshot();
        public int AppearanceRevision(string id)=>journal.AppearanceRevision(id);
        public RoomAppearance ReadAppearance(string id)=>journal.ReadAppearance(id);
        internal string[] AppearanceMembers(string id)=>Snapshot().objects.Where(x=>x.appearanceBindings.Any(b=>b.appearanceId==id)).Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        bool CanChangeAppearance(string target,out string error) {
            if(!CanEditStructures(out error)||!CanEditObject(target,false,out error))return false;
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object before editing its appearance";return false;}
            return new CapabilityContext(this,GetComponent<AnimationWorkshop>()).Target(new JObject{["target"]=target},out _,out error);
        }
        internal bool PrepareAppearance(RoomAppearance definition,string id,int revision,string[] members,out AppearanceEdits edits,out string error) {
            edits=null;if(!CanEditStructures(out error))return false;
            var current=ReadAppearance(id);
            error="The appearance changed; read its current revision and complete member list";
            if(AppearanceRevision(id)!=revision||current==null&&revision!=0||definition==null&&current==null)return false;
            if(definition!=null&&(definition.id!=id||!definition.Validate(out error)))return false;
            var expected=current==null?Array.Empty<string>():AppearanceMembers(id);
            error="Include every bound object exactly once before changing a shared appearance";
            if(members==null||members.Length!=members.Distinct().Count()||!expected.ToHashSet().SetEquals(members))return false;
            if(definition==null&&members.Length>0){error="Unbind this appearance before removing it";return false;}
            foreach(string member in members)if(!CanChangeAppearance(member,out error))return false;
            edits=definition==null?new AppearanceEdits{Removals=new[]{id}}:new AppearanceEdits{Replacements=new[]{definition.Copy()}};
            if(!edits.Validate(out error))return false;
            var candidate=Snapshot();candidate.appearances=edits.Apply(candidate.appearances);return candidate.Validate(out error);
        }
        internal bool EditAppearance(RoomAppearance definition,string id,int revision,string[] members,out string error) {
            if(!PrepareAppearance(definition,id,revision,members,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),definition==null?"Appearance removed":"Appearance saved",false,out error,appearanceEdits:edits);
        }
        internal string AppearanceBindingState(string target,AppearanceBinding binding) {
            var data=Read(target);var item=Find(target);
            if(data==null)return "missing object";
            if(!item||!item.isActiveAndEnabled)return "native instance unavailable";
            if(ScanDrawingAnchor.Has(data))return "use scanned-layer ink controls";
            if(binding.kind=="part"&&!data.recipe.parts.Any(p=>p.id==binding.partId))return "missing part";
            var avatar=item.GetComponent<MaestroAvatar>();
            var imported=item.GetComponent<CreatedRoomObject>()?.Model;
            if(avatar&&avatar.ModelBusy||data.kind==RoomObjectKind.ImportedModel&&(!imported||!imported.Ready))return "model not ready";
            if(binding.kind=="material"&&binding.modelHash!=data.modelHash)return "different model";
            foreach(var renderer in item.GetComponentsInChildren<Renderer>(true)) {
                if(renderer.GetComponent<Book.BookPageTarget>()||renderer.GetComponentInParent<RoomItem>()!=item||
                    renderer.GetComponent<PencilMarks>()&&!(data.kind==RoomObjectKind.Drawing&&renderer.transform==item.transform))continue;
                if(binding.kind=="part"&&item.GetComponent<RecipeObject>()?.AppearancePart(renderer)!=binding.partId)continue;
                foreach(var material in renderer.sharedMaterials) {
                    if(!material||material.shader.name!="Maestro/Watercolor")continue;
                    if(binding.kind=="material") {
                        var model=renderer.GetComponentInParent<ImportedModel>();
                        if(!model||!model.Ready||model.AssetHash!=binding.modelHash||!int.TryParse(material.GetTag("MaestroMaterialIndex",false,""),out int index)||index!=binding.materialIndex)continue;
                    }
                    return "active";
                }
            }
            return "no supported surface";
        }
        internal bool PrepareAppearanceBinding(string target,int revision,AppearanceBinding binding,int appearanceRevision,bool remove,bool independent,out RoomObjectData data,out AppearanceEdits edits,out string error) {
            data=null;edits=null;
            error="Read the exact object and appearance revisions before editing a binding";
            var definition=binding==null?null:ReadAppearance(binding.appearanceId);
            if(ObjectRevision(target)!=revision||binding==null||definition==null||AppearanceRevision(binding.appearanceId)!=appearanceRevision||remove&&independent)return false;
            if(!CanChangeAppearance(target,out error))return false;
            data=Pose(Read(target),Find(target).transform);
            if(!binding.Validate(data)){error="Choose a valid stable root, recipe part or exact imported material address";return false;}
            var previous=data.appearanceBindings.FirstOrDefault(x=>x.Key==binding.Key);
            if(remove) {
                if(previous==null||previous.appearanceId!=binding.appearanceId){error="That binding changed; inspect the current object appearance";return false;}
                if(binding.kind=="root") {ColorUtility.TryParseHtmlString(previous.Effective(definition).tint,out data.color);}
                if(binding.kind=="part") {var part=data.recipe.parts.FirstOrDefault(p=>p.id==binding.partId);if(part!=null)ColorUtility.TryParseHtmlString(previous.Effective(definition).tint,out part.color);}
                data.appearanceBindings=data.appearanceBindings.Where(x=>x.Key!=binding.Key).ToArray();
            } else {
                string state=AppearanceBindingState(target,binding);if(state!="active"){error="Cannot bind appearance: "+state;return false;}
                binding=binding.Copy();
                if(independent) {
                    definition=definition.Copy();definition.id=Guid.NewGuid().ToString("N");definition.name=(definition.name.Length>73?definition.name.Substring(0,73):definition.name)+" (copy)";
                    definition.style=binding.Effective(definition);binding.tint="";binding.appearanceId=definition.id;edits=new AppearanceEdits{Replacements=new[]{definition}};
                }
                data.appearanceBindings=data.appearanceBindings.Where(x=>x.Key!=binding.Key).Append(binding).ToArray();
                if(binding.kind=="root")data.color=Color.white;
                if(binding.kind=="part")data.recipe.parts.Single(p=>p.id==binding.partId).color=Color.white;
            }
            var candidate=Snapshot();var replacement=data;candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();
            if(edits!=null)candidate.appearances=edits.Apply(candidate.appearances);
            return candidate.Validate(out error);
        }
        internal bool BindAppearance(string target,int revision,AppearanceBinding binding,int appearanceRevision,bool remove,bool independent,out string error) {
            if(!PrepareAppearanceBinding(target,revision,binding,appearanceRevision,remove,independent,out var data,out var edits,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),remove?"Appearance unbound":independent?"Independent appearance bound":"Appearance bound",false,out error,appearanceEdits:edits);
        }
        internal JObject ObserveAppearanceTargets(string target,int offset) {
            var data=Read(target);var item=Find(target);if(data==null||!item||offset<0||offset>1057)return null;
            var addresses=new System.Collections.Generic.List<AppearanceBinding>{new()};
            if(data.kind==RoomObjectKind.Assembly)addresses.AddRange(data.recipe.parts.Select(p=>new AppearanceBinding{kind="part",partId=p.id}));
            foreach(var model in item.GetComponentsInChildren<ImportedModel>(true).Where(m=>m.Ready&&m.AssetHash==data.modelHash))
                foreach(int index in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.shader.name=="Maestro/Watercolor").Select(m=>int.TryParse(m.GetTag("MaestroMaterialIndex",false,""),out int i)?i:-1).Where(i=>i>=0&&i<1024).Distinct().OrderBy(i=>i))
                    addresses.Add(new AppearanceBinding{kind="material",modelHash=data.modelHash,materialIndex=index});
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["offset"]=offset,["total"]=addresses.Count,["pageSize"]=3,
                ["targets"]=new JArray(addresses.Skip(offset).Take(3).Select(b=>new JObject{["kind"]=b.kind,["partId"]=b.partId,["modelHash"]=b.modelHash,["materialIndex"]=b.materialIndex,["state"]=AppearanceBindingState(target,b)}))};
        }
        internal JObject ObserveAppearances(int offset) {
            if(offset<0||offset>RoomAppearance.MaximumDefinitions)return null;var values=Appearances();
            return new JObject{["offset"]=offset,["total"]=values.Length,["pageSize"]=3,["entries"]=new JArray(values.Skip(offset).Take(3).Select(p=>new JObject{["id"]=p.id,["revision"]=AppearanceRevision(p.id),["name"]=p.name}))};
        }
        internal JObject ObserveAppearance(string id) {
            var p=ReadAppearance(id);if(p==null)return null;var style=JObject.Parse(JsonUtility.ToJson(p.style));
            JObject Group(params string[] names)=>new(names.Select(n=>new JProperty(n,style[n].DeepClone())));
            return new JObject{["id"]=id,["revision"]=AppearanceRevision(id),["source"]=Group("tint","patternMode","imageHash","pattern","tiling","offset"),["surface"]=Group("renderMode","opacity","cutoff","sidedness","grain","shading")};
        }
        internal JObject ObserveAppearanceMembers(string id,int offset) {
            if(ReadAppearance(id)==null||offset<0||offset>RoomDocument.MaximumObjects+2)return null;var members=AppearanceMembers(id);
            return new JObject{["id"]=id,["revision"]=AppearanceRevision(id),["offset"]=offset,["total"]=members.Length,["pageSize"]=16,["members"]=new JArray(members.Skip(offset).Take(16))};
        }
        internal string AppearanceRenderState(string target,AppearanceBinding binding) {
            var state=AppearanceBindingState(target,binding);if(state!="active")return state;
            var style=ReadAppearance(binding.appearanceId)?.style;
            if(style?.patternMode!="image")return state;
            var loaded=Find(target)?.GetComponent<RoomAppearanceView>()?.ImageState(style.imageHash)??"unavailable";
            return loaded=="ready"?"active":"image "+loaded;
        }
        internal JObject ObserveObjectAppearances(string target,int offset) {
            var data=Read(target);if(data==null||offset<0||offset>33)return null;
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["offset"]=offset,["total"]=data.appearanceBindings.Length,["pageSize"]=2,
                ["bindings"]=new JArray(data.appearanceBindings.Skip(offset).Take(2).Select(b=>new JObject{["appearanceId"]=b.appearanceId,["appearanceRevision"]=AppearanceRevision(b.appearanceId),
                    ["kind"]=b.kind,["partId"]=b.partId,["modelHash"]=b.modelHash,["materialIndex"]=b.materialIndex,["tint"]=b.tint,["state"]=AppearanceRenderState(target,b)})),["temporary"]=TemporaryRoom};
        }
    }
}
