// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Native evaluation of saved recipes, shared by manual and agent edits.</summary>
    public sealed partial class RecipeObject : MonoBehaviour
    {
        readonly Dictionary<string,Transform> nodes = new();
        readonly Dictionary<string,Quaternion> rest = new();
        readonly List<Material> materials = new();
        readonly List<Color> colors = new();
        GameObject geometry,highlight;
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
        public Transform Part(string id) => nodes.TryGetValue(id,out var node) ? node : null;
        public bool Apply(RoomRecipe value)
        {
            if (value == null || !value.Validate(out _)) return false;
            string json = JsonUtility.ToJson(value); if (encoded == json) return false;
            CancelParts();suppressedParts.Clear();encoded = json; recipe = value.Copy(); time = 0; interrupted = runtimeGate?.Held==true; runtimeLoop=null;
            if (geometry) { geometry.SetActive(false); ArtResources.Release(geometry); }
            foreach (var material in materials) ArtResources.Release(material);
            nodes.Clear(); rest.Clear(); materials.Clear(); colors.Clear();
            geometry = new GameObject("Recipe geometry"); geometry.transform.SetParent(transform,false);
            Bounds bounds = default; bool first = true;
            foreach (var part in recipe.parts)
            {
                var node = new GameObject(part.id).transform;
                node.SetParent(string.IsNullOrEmpty(part.parent) ? geometry.transform : nodes[part.parent],false);
                node.SetLocalPositionAndRotation(part.position,part.rotation); nodes.Add(part.id,node); rest.Add(part.id,part.rotation);
                var shape = GameObject.CreatePrimitive(part.shape == "sphere" ? PrimitiveType.Sphere : part.shape == "cylinder" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                shape.transform.SetParent(node,false); shape.transform.localScale = Vector3.Scale(part.size,part.shape == "cylinder" ? new Vector3(1,.5f,1) : Vector3.one);
                // One stable proxy collider belongs to the complete grabbable assembly.
                var collider = shape.GetComponent<Collider>(); collider.enabled = false; ArtResources.Release(collider);
                var material = IllustratedMaterials.Create(part.color); materials.Add(material); colors.Add(part.color); shape.GetComponent<Renderer>().sharedMaterial = material;
                for (int i=0;i<8;i++)
                {
                    var corner = new Vector3((i&1)==0 ? -.5f : .5f,(i&2)==0 ? -.5f : .5f,(i&4)==0 ? -.5f : .5f);
                    var point = transform.InverseTransformPoint(node.TransformPoint(Vector3.Scale(part.size,corner)));
                    if (first) { bounds = new Bounds(point,Vector3.zero); first=false; } else bounds.Encapsulate(point);
                }
            }
            LocalBounds=bounds; Highlight(highlightedPart); return true;
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
        public void Tint(Color tint) { for(int i=0;i<materials.Count;i++) materials[i].color=colors[i]*tint; }
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
        void OnDestroy() {CancelParts();if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged; foreach (var material in materials) ArtResources.Release(material); }
    }
}
