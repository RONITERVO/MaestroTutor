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
        string placementId;
        bool preparingPlacement;
        public bool Placing => placementId != null;
        public void Build(RoomEditor source, RoomPhysicsWorld physics, ScannedRoom environment, RoomInteraction room)
        {
            editor = source; world = physics; scan = environment;
            var wood = Paint("C89D65"); var teal = Paint("2B8D88");
            Part(transform,Vector3.zero,new Vector3(.72f,.59f,.04f),wood);
            var handle = gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(.72f,.59f,.04f);
            var item = gameObject.AddComponent<RoomItem>(); item.Configure(new Collider[] { handle },1,1); room.Register(item);
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
            string id = editor.SelectedId; preparingPlacement = true;
            bool ready;
            try { ready = await scan.PreparePlacement(); }
            catch (Exception) { ready = false; }
            finally { if (this) preparingPlacement = false; }
            if (!this || !isActiveAndEnabled) return;
            if (!ready || id != editor.SelectedId) { status.text = "Live surface placement needs Quest room access"; return; }
            placementId = id;
            status.text = "Aim at a real floor or table and tap trigger";
        }
        public void CancelPlacement() { placementId = null; Refresh(); }
        public void Place(Ray ray)
        {
            var item = editor.Find(placementId); placementId = null;
            if (!item || editor.AnyHeld || !scan.TrySurface(ray,out var point,out var normal) || normal.y < .7f)
            { status.text = "No clear level surface detected — try Place surface again"; return; }
            var colliders = item.Grab.colliders.Where(value => value && value.enabled).ToArray();
            if (colliders.Length == 0) return;
            var bounds = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
            float support = Vector3.Dot(new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)),bounds.extents);
            editor.Select(item); editor.PlaceSelected(point + normal * (support+.01f) - (bounds.center-item.transform.position));
        }
        void Refresh()
        {
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
        void OnDisable() { if (editor) CancelPlacement(); }
        void OnDestroy() { if (world) world.Changed -= Refresh; if(scan)scan.Changed-=Refresh; if (editor) editor.Changed -= Refresh; foreach (var material in materials) ArtResources.Release(material); }
    }
}
