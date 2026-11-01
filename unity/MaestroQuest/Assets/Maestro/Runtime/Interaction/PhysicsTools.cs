// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    public sealed class PhysicsTools : MonoBehaviour
    {
        readonly List<Material> materials = new();
        RoomEditor editor;
        RoomPhysicsWorld world;
        ScannedRoom scan;
        TextMesh status, selection, scanLabel; RuleToolAction scanAction;
        string placementId,placementSetup;
        int placementRevision,placementGeneration;
        bool preparingPlacement;
        public bool Placing => placementId != null;
        public bool Busy => Placing || preparingPlacement;
        public void Build(RoomEditor source, RoomPhysicsWorld physics, ScannedRoom environment, RoomInteraction room)
        {
            editor = source; world = physics; scan = environment;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88");
            Part(transform,Vector3.zero,new Vector3(.72f,.59f,.04f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.72f,.59f,.04f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item,true);
            var labels = new[] { "Load room","Scan room","Show room","Start physics","Pause","Object mode","Mass","Place surface","Collision shape" };
            Action[] commands = { scan.Load,()=>{if(scan.Busy)scan.CancelSetup();else scan.Scan();},scan.ToggleSurfaces,StartPhysics,world.PausePhysics,editor.CyclePhysics,editor.CycleMass,ArmPlacement,editor.CycleCollider };
            for (int i = 0; i < labels.Length; i++)
            {
                var tool = new GameObject(labels[i]); tool.transform.SetParent(transform,false); tool.transform.localPosition = new Vector3(-.23f+i%3*.23f,.11f-i/3*.12f,-.05f);
                var collider = tool.AddComponent<BoxCollider>(); collider.size = new Vector3(.10f,.06f,.065f);
                var action = tool.AddComponent<RuleToolAction>(); action.Command = commands[i]; action.AccessibleName = labels[i];
                Part(tool.transform,Vector3.zero,new Vector3(.06f,.03f,.04f),teal);
                var label=Label(tool.transform,new Vector3(0,-.04f,-.024f),labels[i],.0048f);if(i==1){scanLabel=label;scanAction=action;}
            }
            selection = Label(transform,new Vector3(0,.23f,-.023f),"",.005f);
            status = Label(transform,new Vector3(0,-.235f,-.023f),"",.0047f);
            world.Changed += PhysicsChanged; scan.Changed += Refresh; editor.Changed += EditorChanged; Refresh();
        }
        internal bool RequestPhysicsStart(out string run,out string error)
        {
            run=null;error="Room actions are not ready";var rules=editor?editor.GetComponent<RoomRules>():null;
            if(!world||!rules||rules.Scheduler==null||!rules.isActiveAndEnabled)return false;
            var call=new Newtonsoft.Json.Linq.JObject{["id"]="physics.simulation.set",["version"]=1,["arguments"]=new Newtonsoft.Json.Linq.JObject{["operation"]="start",["stateId"]=world.ObserveSimulation()["stateId"]}};
            return rules.Scheduler.Invoke(call,Time.unscaledTime,out run,out error);
        }
        void StartPhysics(){if(!RequestPhysicsStart(out _,out var error)&&status)status.text=error;}
        async void ArmPlacement()
        {
            if (preparingPlacement) return;
            if(editor.ManualCreationPending){RefreshStatus(editor.Status);return;}
            if (Placing) { CancelPlacement(); return; }
            var item = editor.Find(editor.SelectedId);
            if (!item || item.Grab.isSelected) { status.text = "Select and release an item first"; return; }
            string id = editor.SelectedId;int generation=++placementGeneration;preparingPlacement = true;
            int revision=editor.ObjectRevision(id);string setup=scan.SetupIdentity;
            bool ready;
            try { ready = await scan.PreparePlacement(); }
            catch (Exception) { ready = false; }
            finally { if (this) preparingPlacement = false; }
            if (!this || !isActiveAndEnabled || generation!=placementGeneration) return;
            if (!ready || id != editor.SelectedId || revision!=editor.ObjectRevision(id) || setup!=scan.SetupIdentity) { status.text = "Room or object changed; finish room access and try Place surface again"; return; }
            placementId = id;placementRevision=revision;placementSetup=setup;
            status.text = "Aim at a real floor or table and tap trigger";
        }
        public void CancelPlacement() { placementGeneration++;placementId = null; Refresh(); }
        public void Place(Ray ray)
        {
            string id=placementId;placementId=null;if(id==null)return;
            // The shared physical coordinator takes over only this target and
            // retains its saved completion after the pointing gesture has ended.
            editor.PlaceOnSurfaceManually(scan,id,placementRevision,placementSetup,ray);
        }
        void OnEnable()=>Refresh();
        void Refresh()=>RefreshStatus(scan?scan.Status:"");
        void PhysicsChanged()=>RefreshStatus(world?world.Status:"");
        void EditorChanged()=>RefreshStatus(editor?editor.Status:"");
        void RefreshStatus(string message)
        {
            if(!isActiveAndEnabled||!status||!editor)return;
            if (!status || !editor) return;
            var data = editor.Read(editor.SelectedId);
            selection.text = data == null ? "Select a creation to set physics" : data.kind + " · " + data.physics + " · " + data.mass.ToString("0.##") + " kg\nCollision: " + data.collisionShape;
            if(scanLabel){scanLabel.text=scan.Busy?(scan.CanCancel?"Cancel setup":"Wait for system"):"Scan room";scanAction.AccessibleName=scanLabel.text;scanAction.GetComponent<Collider>().enabled=!scan.Busy||scan.CanCancel;}
            if (!Placing) status.text = string.Join("\n",ModelText.Wrap(editor.ManualCreationPending?editor.Status:message,62).Take(3));
        }
        Material Paint(string color) { var material = IllustratedMaterials.CreateControl(IllustratedMaterials.Hex(color)); materials.Add(material); return material; }
        static void Part(Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(parent,false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false; ArtResources.Release(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = material;
        }
        static TextMesh Label(Transform parent,Vector3 position,string text,float size)
        {
            var root = new GameObject("Physics tool marking",typeof(TextMesh)); root.transform.SetParent(parent,false); root.transform.localPosition = position;
            var mesh = root.GetComponent<TextMesh>(); mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); mesh.fontSize = 48; mesh.characterSize = size;
            mesh.text = text; mesh.richText = false; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
            root.GetComponent<MeshRenderer>().sharedMaterial = IllustratedMaterials.TextMaterial(mesh.font); return mesh;
        }
        void OnApplicationPause(bool paused) { if (paused) CancelPlacement(); }
        void OnApplicationFocus(bool focused) { if(!focused)CancelPlacement(); }
        void OnDisable() { if (editor) CancelPlacement(); }
        void OnDestroy() { if (world) world.Changed -= PhysicsChanged; if(scan)scan.Changed-=Refresh; if (editor) editor.Changed -= EditorChanged; foreach (var material in materials) ArtResources.Release(material); }
    }
}
