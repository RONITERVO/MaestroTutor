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
        RoomResourceOwner modelOwner,collisionOwner;
        Collider originalCollider, chosenCollider;
        ItemCollider collisionShape;
        CollisionGeometry customGeometry;
        string collisionEncoded;
        public int CollisionPieces => GetComponent<RoomItem>()?.Grab.colliders.Count??0;
        Bounds geometryBounds;
        bool pendingCollider;
        public ImportedModel Model { get; private set; }
        public string ModelStatus { get; private set; }
        RoomItem acousticItem;
        RigidRoomItem acousticBody;
        // Mobility is an acoustic input, independently of whether physics or
        // colliders are currently enabled. A paused dynamic ball is not a wall.
        internal bool AcousticBoundary => acousticBody && !acousticBody.Dynamic
            && acousticItem && acousticItem.Grab && !acousticItem.Grab.isSelected
            && !acousticBody.AnimationOwned && !(recipe && recipe.IsPlaying) && !(Model && Model.IsPlaying);
        public RoomItem Build(RoomObjectData data, ModelLibrary library = null, RoomRuntimeGate runtimeGate = null, RoomWorldIdentity world = null)
            =>BuildCore(data,library,runtimeGate,world,null);
        internal RoomItem BuildPrepared(RoomObjectData data,ModelLibrary library,RoomRuntimeGate runtimeGate,RoomWorldIdentity world,RoomEditPreparation preparation)=>BuildCore(data,library,runtimeGate,world,preparation);
        RoomItem BuildCore(RoomObjectData data,ModelLibrary library,RoomRuntimeGate runtimeGate,RoomWorldIdentity world,RoomEditPreparation preparation)
        {
            collisionOwner=new RoomResourceOwner(world,data.id,"collision");
            if(data.kind==RoomObjectKind.ImportedModel)modelOwner=new RoomResourceOwner(world,data.id,"object");
            importedObject=data.kind==RoomObjectKind.ImportedModel;requestedModelGeometry=data.modelGeometry.Copy();
            Bounds bounds;
            Collider collider;
            if(ScanDrawingAnchor.Has(data)) {
                bounds=new Bounds(Vector3.zero,new Vector3(data.surfaces[0].width,data.surfaces[0].height,.002f));
                var box=gameObject.AddComponent<BoxCollider>();box.size=bounds.size;box.isTrigger=true;collider=box;
            }
            else if (data.kind == RoomObjectKind.Assembly)
            {
                recipe=gameObject.AddComponent<RecipeObject>(); recipe.ConfigureRuntime(runtimeGate);recipe.ConfigureResourceOwner(new RoomResourceOwner(world,data.id,"object"));recipe.Apply(data.recipe,preparation,data.id); bounds=recipe.LocalBounds;
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
                if (data.kind != RoomObjectKind.ImportedModel) Book.AcousticSurface.Attach(shape, shape.GetComponent<MeshFilter>().sharedMesh);
                collider = shape.GetComponent<Collider>(); bounds = new Bounds(Vector3.zero, Vector3.one * .13f);
            }
            ApplyColor(data.color);
            BuildSelection(bounds);
            var item = gameObject.AddComponent<RoomItem>(); var limits = RoomDocument.ScaleLimits(data.kind);
            item.Configure(new[] { collider }, limits.minimum, limits.maximum);item.PoseLocked=ScanDrawingAnchor.Has(data);
            geometryBounds = bounds; originalCollider = chosenCollider = collider;
            var rigid = gameObject.AddComponent<RigidRoomItem>(); rigid.Initialize(item);
            acousticItem = item; acousticBody = rigid;
            collider.gameObject.layer = RoomPhysicsLayers.Item;
            if (importedObject) { rigid.SetGeometryReady(false);SetCollisionShape(data.collisionShape,true); }
            if (data.kind == RoomObjectKind.ImportedModel && library != null) LoadModel(data.modelHash, library, collider);
            ApplyCollision(data.collision,preparation);SetCollisionShape(data.collisionShape);
            return item;
        }
        internal void ApplyScanLayer(RoomObjectData data) {
            if(!ScanDrawingAnchor.Has(data))return;var size=new Vector3(data.surfaces[0].width,data.surfaces[0].height,.002f);if(geometryBounds.size==size)return;
            geometryBounds=new Bounds(Vector3.zero,size);((BoxCollider)originalCollider).size=size;
            bool selected=selection&&selection.activeSelf;if(selection){selection.SetActive(false);Destroy(selection);}BuildSelection(geometryBounds);SetSelected(selected);
        }
        public void ApplyRecipe(RoomRecipe value)=>ApplyRecipe(value,null);
        internal void ApplyRecipe(RoomRecipe value,RoomEditPreparation preparation)
        {
            if (!recipe || !recipe.Apply(value,preparation,collisionOwner.Target)) return;
            recipe.Tint(tint);
            geometryBounds=recipe.LocalBounds;
            var box=(BoxCollider)originalCollider; box.center=geometryBounds.center; box.size=geometryBounds.size;
            bool selected=selection && selection.activeSelf; if(selection) { selection.SetActive(false); Destroy(selection); }
            BuildSelection(geometryBounds); SetSelected(selected); SetCollisionShape(collisionShape,true);
        }
        public void ApplyCollision(CollisionRecipe source)=>ApplyCollision(source,null);
        internal void ApplyCollision(CollisionRecipe source,RoomEditPreparation preparation)
        {
            string encoded=source==null||source.shapes.Length==0?null:JsonUtility.ToJson(source);
            if(encoded==collisionEncoded)return;
            // Build a detached replacement before unregistering the previous handles.
            var candidate=encoded==null?null:preparation?.TakeCollision(collisionOwner.Target,source)??new CollisionGeometry(source,null,collisionOwner);
            candidate?.Attach(transform);
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
            chosenCollider=null;customGeometry?.SetActive(false);importedCollision?.SetActive(false);
            Collider[] active;
            if(importedObject&&!modelGeometryReady)active=System.Array.Empty<Collider>();
            else if(shape==ItemCollider.Automatic&&importedCollision!=null){importedCollision.SetActive(true);active=importedCollision.Colliders;}
            else if(shape==ItemCollider.Automatic&&customGeometry!=null){customGeometry.SetActive(true);active=customGeometry.Colliders;}
            else {
                if(shape==ItemCollider.Automatic)chosenCollider=originalCollider;
                else if(shape==ItemCollider.Sphere){var sphere=gameObject.AddComponent<SphereCollider>();sphere.center=geometryBounds.center;sphere.radius=Mathf.Max(geometryBounds.extents.x,Mathf.Max(geometryBounds.extents.y,geometryBounds.extents.z));chosenCollider=sphere;}
                else {var box=gameObject.AddComponent<BoxCollider>();box.center=geometryBounds.center;box.size=geometryBounds.size;chosenCollider=box;}
                active=new[]{chosenCollider};
            }
            var field=GetComponent<HeightFieldView>();if(field&&field.Collision&&field.Collision.gameObject.activeSelf)active=active.Append(field.Collision).ToArray();
            foreach(var collider in active){collider.gameObject.layer=RoomPhysicsLayers.Item;collider.enabled=true;collider.sharedMaterial=originalCollider.sharedMaterial;}
            item.Grab.colliders.Clear();item.Grab.colliders.AddRange(active);item.Grab.enabled=active.Length>0;
            var body=GetComponent<Rigidbody>();body.ResetCenterOfMass();body.ResetInertiaTensor();
        }

        async void LoadModel(string hash, ModelLibrary library, Collider collider)
        {
            ModelStatus = "Loading local model…";
            try
            {
                var asset = await library.ReadAsync(hash); if (!this) return;
                var root = new GameObject("Imported geometry"); root.transform.SetParent(transform, false);
                Model = root.AddComponent<ImportedModel>();
                Model.ConfigureResourceOwner(modelOwner);
                await Model.LoadAsync(asset); if (!this) return;
                // Only rigid visual meshes: no bounding-box approximation across
                // holes, no bind-pose avatar skin or decorative pencil overlay.
                foreach (var filter in Model.Instance.GetComponentsInChildren<MeshFilter>())
                    if (filter.GetComponent<MeshRenderer>() is { } renderer && !filter.GetComponent<PencilMarks>() &&
                        !renderer.sharedMaterials.Any(material => material && material.HasProperty("_AlphaCutoff") && material.GetFloat("_AlphaCutoff") > 0))
                        Book.AcousticSurface.Attach(filter.gameObject, filter.sharedMesh);
                ApplyModelGeometry(requestedModelGeometry);
            }
            catch (System.Exception error) { if (this) {ModelStatus = ModelGeometryIssue = error is ModelImportException ? error.Message : "This model could not be loaded. Import a compatible GLB or VRM again.";modelGeometryReady=false;SetCollisionShape(collisionShape,true);} }
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
        void OnDestroy() {preparedGeometry?.Dispose();importedCollision?.Dispose();customGeometry?.Dispose();ArtResources.Release(pigment);}
        void LateUpdate() { if (pendingCollider && !GetComponent<RoomItem>().Grab.isSelected) SetCollisionShape(collisionShape,true); }
    }
}
