// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Native evaluation of saved recipes, shared by manual and agent edits.</summary>
    public sealed partial class RecipeObject : MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner
    {
        Dictionary<string,Transform> nodes = new();
        Dictionary<string,Quaternion> rest = new();
        List<RecipeMaterials.Lease> materials = new();
        List<Renderer> renderers = new();
        Color appliedTint=Color.white;
        readonly HashSet<string> appearanceParts=new();bool appearanceTintChanged;
        RecipeVisual visual;
        RoomResourceOwner resourceOwner=new(null,null,"unscoped");
        GameObject highlight;
        string highlightedPart;
        RoomRecipe recipe;
        string encoded;
        float time;
        bool interrupted;
        RoomRuntimeGate runtimeGate;
        internal void ConfigureRuntime(RoomRuntimeGate gate) {if(runtimeGate==gate)return;if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged;runtimeGate=gate;if(gate!=null)gate.Changed+=RuntimeChanged;RuntimeChanged();}
        void RuntimeChanged(){if(runtimeGate?.Held==true)Stop();}
        bool? runtimeLoop;
        bool RuntimePlaying => runtimeLoop.HasValue || recipe.playing;
        public Bounds LocalBounds { get; private set; }
        bool WholePlaying => recipe != null && runtimeGate?.Held!=true && RuntimePlaying && !interrupted && ((runtimeLoop ?? recipe.loop) || time < recipe.duration);
        public bool IsPlaying => partsPlaying.Count>0 || WholePlaying && System.Array.Exists(recipe.tracks,t=>!suppressedParts.Contains(t.part));
        public void StartRule(bool loop) {if(runtimeGate?.Held==true)return;runtimeLoop=loop;Restart();}
        public void StopRule() {runtimeLoop=null;Stop();}
        public void Restart() { if(runtimeGate?.Held==true||recipe == null || recipe.tracks.Length == 0) return; CancelParts();suppressedParts.Clear();time=0; interrupted=false; }
        public void Stop() { interrupted=true;CancelParts(); }
        internal string AppearancePart(Renderer renderer){foreach(var pair in nodes)if(renderer.transform.parent==pair.Value)return pair.Key;return null;}
        public Transform Part(string id) => nodes.TryGetValue(id,out var node) ? node : null;
        internal void ConfigureResourceOwner(RoomResourceOwner owner)
        {
            if(visual!=null)throw new System.InvalidOperationException("Recipe ownership is fixed before construction");
            resourceOwner=owner??throw new System.ArgumentNullException(nameof(owner));
        }
        public bool Apply(RoomRecipe value)=>Apply(value,null,resourceOwner.Target);
        internal bool Apply(RoomRecipe value,RoomEditPreparation preparation,string target)
        {
            if(value==null||preparation==null&&!value.Validate(out _))return false;
            string json=JsonUtility.ToJson(value);if(encoded==json)return false;
            var candidate=preparation?.TakeRecipe(target,value);
            // Exact prepared sources have already passed the shared validator.
            // Reconciliation of an unchanged source has no geometry work to repeat.
            if(candidate==null){if(preparation!=null&&!value.Validate(out _))return false;candidate=new RecipeVisual(value,resourceOwner);}
            // Construction and source copying have finished before stopping any
            // accepted playback, changing part handles or retiring old resources.
            CancelParts();suppressedParts.Clear();var old=visual;old?.SetActive(false);
            visual=candidate;encoded=json;recipe=candidate.Source;time=0;interrupted=runtimeGate?.Held==true;runtimeLoop=null;
            nodes=candidate.Nodes;rest=candidate.Rest;materials=candidate.Materials;renderers=candidate.Renderers;appliedTint=Color.white;
            candidate.Attach(transform);LocalBounds=candidate.LocalBounds;old?.Dispose();Highlight(highlightedPart);return true;
        }
        public void Highlight(string partId)
        {
            highlightedPart=partId;
            if(highlight) {highlight.SetActive(false);ArtResources.Release(highlight);}
            if(string.IsNullOrEmpty(partId) || !nodes.TryGetValue(partId,out var node))return;
            var part=System.Array.Find(recipe.parts,value=>value.id==partId);var half=part.size*.5f+Vector3.one*.008f;
            highlight=new GameObject("Selected recipe part",typeof(PencilMarks));highlight.transform.SetParent(node,false);
            var paths=new List<Vector3[]>();
            foreach(float z in new[]{-half.z,half.z})paths.Add(new[]{new Vector3(-half.x,-half.y,z),new Vector3(half.x,-half.y,z),new Vector3(half.x,half.y,z),new Vector3(-half.x,half.y,z),new Vector3(-half.x,-half.y,z)});
            foreach(float x in new[]{-half.x,half.x})foreach(float y in new[]{-half.y,half.y})paths.Add(new[]{new Vector3(x,y,-half.z),new Vector3(x,y,half.z)});
            var marks=highlight.GetComponent<PencilMarks>();marks.SetPaths(paths,.0015f);marks.SetColor(IllustratedMaterials.Ribbon);
        }
        internal void ConfigureAppearanceBindings(AppearanceBinding[] bindings) {
            var next=new HashSet<string>();foreach(var b in bindings)if(b.kind=="part")next.Add(b.partId);
            if(appearanceParts.SetEquals(next))return;appearanceParts.Clear();appearanceParts.UnionWith(next);appearanceTintChanged=true;
        }
        public void Tint(Color tint)
        {
            if(appliedTint.Equals(tint)&&!appearanceTintChanged)return;appearanceTintChanged=false;appliedTint=tint;
            for(int i=0;i<materials.Count;i++) {
                var material=RecipeMaterials.Acquire(recipe.parts[i],appearanceParts.Contains(recipe.parts[i].id)?Color.white:tint);renderers[i].sharedMaterial=material.Material;
                materials[i].Dispose();materials[i]=material;
            }
        }
        void Update()
        {
            if (runtimeGate?.Held==true||recipe == null) return;
            float delta=Mathf.Min(Time.deltaTime,.05f);
            AdvanceParts();
            if(!WholePlaying)return;
            time += delta;
            foreach (var track in recipe.tracks) if(!suppressedParts.Contains(track.part))nodes[track.part].localRotation=rest[track.part]*recipe.Sample(track,time,runtimeLoop);
        }
        void OnApplicationPause(bool paused) { if (paused) Stop(); }
        void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
        void OnDisable() {Stop();}
        bool nativeResourcesReleased;
        void OnDestroy()=>ReleaseNativeResources();
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(nativeResourcesReleased)return;nativeResourcesReleased=true;CancelParts();if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged; visual?.Dispose();visual=null; }
    }
}
