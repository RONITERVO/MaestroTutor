// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Owned geometry and paint for one user-created object.</summary>
    public sealed partial class CreatedRoomObject : MonoBehaviour
    {
        Material pigment;
        RecipeObject recipe;
        PencilMarks drawing;
        GameObject selection;
        bool primarySelected,constructionSelected;
        internal bool ConstructionMarked=>constructionSelected&&selection&&selection.activeSelf;
        Color tint;
        Collider originalCollider, chosenCollider;
        ItemCollider collisionShape;
        CollisionGeometry customGeometry;
        string collisionEncoded;
        public int CollisionPieces => GetComponent<RoomItem>()?.Grab.colliders.Count??0;
        Bounds geometryBounds;
        bool pendingCollider;
        public ImportedModel Model { get; private set; }
        public string ModelStatus { get; private set; }
        public RoomItem Build(RoomObjectData data, ModelLibrary library = null, RoomRuntimeGate runtimeGate = null)
        {
            Bounds bounds;
            Collider collider;
            if(ScanDrawingAnchor.Has(data)) {
                bounds=new Bounds(Vector3.zero,new Vector3(data.surfaces[0].width,data.surfaces[0].height,.002f));
                var box=gameObject.AddComponent<BoxCollider>();box.size=bounds.size;box.isTrigger=true;collider=box;
            }
            else if (data.kind == RoomObjectKind.Assembly)
            {
                recipe=gameObject.AddComponent<RecipeObject>(); recipe.ConfigureRuntime(runtimeGate);recipe.Apply(data.recipe); bounds=recipe.LocalBounds;
                var box=gameObject.AddComponent<BoxCollider>(); box.center=bounds.center; box.size=bounds.size; collider=box;
            }
            else if (data.kind == RoomObjectKind.Drawing)
            {
                drawing = gameObject.AddComponent<PencilMarks>(); drawing.SetPaths(new[] { data.points }, data.radius);
                drawingPoints=(Vector3[])data.points.Clone();drawingRadius=data.radius;
                bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
                var box = gameObject.AddComponent<BoxCollider>(); box.center = bounds.center;
                box.size = bounds.size + Vector3.one * .018f; collider = box;
            }
            else
            {
                var primitive = data.kind == RoomObjectKind.Ball ? PrimitiveType.Sphere : data.kind == RoomObjectKind.Cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                var shape = GameObject.CreatePrimitive(primitive); shape.name = data.kind.ToString(); shape.transform.SetParent(transform, false);
                shape.transform.localScale = data.kind == RoomObjectKind.Cylinder ? new Vector3(.13f,.065f,.13f) : Vector3.one * .13f;
                pigment = IllustratedMaterials.Create(data.color); shape.GetComponent<Renderer>().sharedMaterial = pigment;
                collider = shape.GetComponent<Collider>(); bounds = new Bounds(Vector3.zero, Vector3.one * .13f);
            }
            ApplyColor(data.color);
            BuildSelection(bounds);
            var item = gameObject.AddComponent<RoomItem>(); var limits = RoomDocument.ScaleLimits(data.kind);
            item.Configure(new[] { collider }, limits.minimum, limits.maximum);item.PoseLocked=ScanDrawingAnchor.Has(data);
            geometryBounds = bounds; originalCollider = chosenCollider = collider;
            var rigid = gameObject.AddComponent<RigidRoomItem>(); rigid.Initialize(item);
            collider.gameObject.layer = RoomPhysicsLayers.Item;
            if (data.kind == RoomObjectKind.ImportedModel) rigid.SetGeometryReady(false);
            if (data.kind == RoomObjectKind.ImportedModel && library != null) LoadModel(data.modelHash, library, collider);
            ApplyCollision(data.collision);SetCollisionShape(data.collisionShape);
            return item;
        }
        internal void ApplyScanLayer(RoomObjectData data) {
            if(!ScanDrawingAnchor.Has(data))return;var size=new Vector3(data.surfaces[0].width,data.surfaces[0].height,.002f);if(geometryBounds.size==size)return;
            geometryBounds=new Bounds(Vector3.zero,size);((BoxCollider)originalCollider).size=size;
            bool selected=selection&&selection.activeSelf;if(selection){selection.SetActive(false);Destroy(selection);}BuildSelection(geometryBounds);SetSelected(selected);
        }
        public void ApplyRecipe(RoomRecipe value)
        {
            if (!recipe || !recipe.Apply(value)) return;
            recipe.Tint(tint);
            geometryBounds=recipe.LocalBounds;
            var box=(BoxCollider)originalCollider; box.center=geometryBounds.center; box.size=geometryBounds.size;
            bool selected=selection && selection.activeSelf; if(selection) { selection.SetActive(false); Destroy(selection); }
            BuildSelection(geometryBounds); SetSelected(selected); SetCollisionShape(collisionShape,true);
        }
        public void ApplyCollision(CollisionRecipe source)
        {
            string encoded=source==null||source.shapes.Length==0?null:JsonUtility.ToJson(source);
            if(encoded==collisionEncoded)return;
            // Build a detached replacement before unregistering the previous handles.
            var candidate=encoded==null?null:new CollisionGeometry(source,transform);
            var old=customGeometry;customGeometry=candidate;collisionEncoded=encoded;
            SetCollisionShape(collisionShape,true);old?.Dispose();
        }
        public void SetCollisionShape(ItemCollider shape, bool rebuild = false)
        {
            if (!rebuild && shape == collisionShape && !pendingCollider) return;
            var item = GetComponent<RoomItem>(); if (!item) return;
            collisionShape=shape;
            if (item.Grab.isSelected) { pendingCollider=true; return; }
            pendingCollider=false;item.Grab.enabled=false;
            foreach(var old in item.Grab.colliders)if(old)old.enabled=false;
            if(chosenCollider&&chosenCollider!=originalCollider)ArtResources.Release(chosenCollider);
            chosenCollider=null;customGeometry?.SetActive(false);
            Collider[] active;
            if(shape==ItemCollider.Automatic&&customGeometry!=null){customGeometry.SetActive(true);active=customGeometry.Colliders;}
            else {
                if(shape==ItemCollider.Automatic)chosenCollider=originalCollider;
                else if(shape==ItemCollider.Sphere){var sphere=gameObject.AddComponent<SphereCollider>();sphere.center=geometryBounds.center;sphere.radius=Mathf.Max(geometryBounds.extents.x,Mathf.Max(geometryBounds.extents.y,geometryBounds.extents.z));chosenCollider=sphere;}
                else {var box=gameObject.AddComponent<BoxCollider>();box.center=geometryBounds.center;box.size=geometryBounds.size;chosenCollider=box;}
                active=new[]{chosenCollider};
            }
            var field=GetComponent<HeightFieldView>();if(field&&field.Collision&&field.Collision.gameObject.activeSelf)active=active.Append(field.Collision).ToArray();
            foreach(var collider in active){collider.gameObject.layer=RoomPhysicsLayers.Item;collider.enabled=true;collider.sharedMaterial=originalCollider.sharedMaterial;}
            item.Grab.colliders.Clear();item.Grab.colliders.AddRange(active);item.Grab.enabled=true;
            var body=GetComponent<Rigidbody>();body.ResetCenterOfMass();body.ResetInertiaTensor();
        }

        async void LoadModel(string hash, ModelLibrary library, Collider collider)
        {
            ModelStatus = "Loading local model…";
            try
            {
                var asset = await library.ReadAsync(hash); if (!this) return;
                var root = new GameObject("Imported geometry"); root.transform.SetParent(transform, false);
                Model = root.AddComponent<ImportedModel>(); await Model.LoadAsync(asset); if (!this) return;
                var box = (BoxCollider)collider; box.transform.localScale = Vector3.one; box.center = Model.LocalBounds.center; box.size = Model.LocalBounds.size + Vector3.one * .02f;
                box.GetComponent<Renderer>().enabled = false;
                bool selected = selection && selection.activeSelf; if (selection) { selection.SetActive(false); Destroy(selection); }
                BuildSelection(Model.LocalBounds); SetSelected(selected); ApplyColor(tint);
                geometryBounds = Model.LocalBounds; SetCollisionShape(collisionShape,true);
                ModelStatus = asset.Inspection.IsAvatar ? "VRM imported as room object" : "Model ready";
                GetComponent<RigidRoomItem>().SetGeometryReady(true);
            }
            catch (System.Exception error) { if (this) ModelStatus = error is ModelImportException ? error.Message : "This model could not be loaded. Import a compatible GLB or VRM again."; }
        }
        public void ApplyColor(Color color) { tint = color; GetComponent<HeightFieldView>()?.Tint(color); if(recipe) recipe.Tint(color); if (pigment) pigment.color = color; if (drawing) drawing.SetColor(color); if (Model && Model.Ready) Model.Instance.GetComponent<PencilModelStyle>()?.Tint(color); }
        public void SetSelection(bool primary,bool member){primarySelected=primary;constructionSelected=member;SetSelected(primary||member);}
        public void SetSelected(bool value) { if (selection) {selection.SetActive(value);selection.GetComponent<PencilMarks>().SetColor(constructionSelected&&!primarySelected?IllustratedMaterials.Hex("2B8D88"):IllustratedMaterials.Ribbon);} }

        void BuildSelection(Bounds bounds)
        {
            selection = new GameObject("Selected object outline", typeof(PencilMarks)); selection.transform.SetParent(transform, false);
            var low = bounds.min - Vector3.one * .012f; var high = bounds.max + Vector3.one * .012f;
            var paths = new List<Vector3[]>();
            foreach (float z in new[] { low.z, high.z }) paths.Add(new[] { new Vector3(low.x,low.y,z), new Vector3(high.x,low.y,z), new Vector3(high.x,high.y,z), new Vector3(low.x,high.y,z), new Vector3(low.x,low.y,z) });
            foreach (float x in new[] { low.x,high.x }) foreach (float y in new[] { low.y,high.y }) paths.Add(new[] { new Vector3(x,y,low.z), new Vector3(x,y,high.z) });
            var outline = selection.GetComponent<PencilMarks>(); outline.SetPaths(paths,.001f); outline.SetColor(IllustratedMaterials.Ribbon);
            selection.SetActive(false);
        }
        void OnDestroy() {customGeometry?.Dispose();ArtResources.Release(pigment);}
        void LateUpdate() { if (pendingCollider && !GetComponent<RoomItem>().Grab.isSelected) SetCollisionShape(collisionShape,true); }
    }
}
