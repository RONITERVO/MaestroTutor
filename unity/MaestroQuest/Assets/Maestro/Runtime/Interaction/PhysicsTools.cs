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
            Action[] commands = { scan.Load,()=>{if(scan.Busy)scan.CancelSetup();else scan.Scan();},scan.ToggleSurfaces,world.StartPhysics,world.PausePhysics,editor.CyclePhysics,editor.CycleMass,ArmPlacement,editor.CycleCollider };
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
            world.Changed += Refresh; scan.Changed += Refresh; editor.Changed += Refresh; Refresh();
        }
        async void ArmPlacement()
        {
            if (preparingPlacement) return;
            if (Placing) { CancelPlacement(); return; }
            var item = editor.Find(editor.SelectedId);
            if (!item || editor.AnyHeld) { status.text = "Select and release an item first"; return; }
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
            if(editor.AnyHeld){status.text="Release held objects before placing";return;}
            // Preserve the physical tool's explicit human interruption priority.
            editor.PrepareAgentEdit();
            if(!scan.PlaceObject(editor,id,placementRevision,placementSetup,ray,out _,out var error))status.text=error;
        }
        void OnEnable()=>Refresh();
        void Refresh()
        {
            if(!isActiveAndEnabled||!status||!editor)return;
            if (!status || !editor) return;
            var data = editor.Read(editor.SelectedId);
            selection.text = data == null ? "Select a creation to set physics" : data.kind + " · " + data.physics + " · " + data.mass.ToString("0.##") + " kg\nCollision: " + data.collisionShape;
            if(scanLabel){scanLabel.text=scan.Busy?(scan.CanCancel?"Cancel setup":"Wait for system"):"Scan room";scanAction.AccessibleName=scanLabel.text;scanAction.GetComponent<Collider>().enabled=!scan.Busy||scan.CanCancel;}
            if (!Placing) status.text = string.Join("\n",ModelText.Wrap(scan.Status,62).Take(3));
        }
        Material Paint(string color) { var material = IllustratedMaterials.Create(IllustratedMaterials.Hex(color)); materials.Add(material); return material; }
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
        void OnDestroy() { if (world) world.Changed -= Refresh; if(scan)scan.Changed-=Refresh; if (editor) editor.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}
