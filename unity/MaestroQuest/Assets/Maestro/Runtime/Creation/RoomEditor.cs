// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor : MonoBehaviour
    {
        readonly Dictionary<string, RoomItem> objects = new();
        readonly Dictionary<RoomItem, string> identities = new();
        RoomInteraction room;
        RoomJournal journal;
        RoomStorage storage;
        public RoomOwnership Ownership {get;}=new();
        public RoomRuntimeGate RuntimeGate {get;private set;}=new();
        public Maestro.Quest.Persistence.WorkspaceWriteGate WriteGate {get;}=new();
        readonly Dictionary<string,RoomOwnership.Lease> handOwners=new();
        bool ownershipPaused,ownershipFocused=true;
        string selected;
        bool applying, dirty;
        float saveAt;
        Task<string> saveTask;
        readonly Persistence.WorkspaceWriteGate saveDispatch=new();
        internal IDisposable HoldSaveDispatch(out Task dispatched,out string error){var lease=saveDispatch.TryFreeze(out error);dispatched=(Task)saveTask??Task.CompletedTask;return lease;}
        string lastSaveError;
        internal bool HasUnsavedChanges => dirty || saveTask != null;
        public string Status { get; private set; } = "Choose a shape or pick up an object";
        public bool DrawingMode { get; private set; }
        public bool DrawingOnSurfaces {get;private set;}
        public bool SurfaceErasing {get;private set;}
        public float DrawingRadius {get;private set;}=.003f;
        public Color Paint { get; private set; } = IllustratedMaterials.Hex("2B8D88");
        public bool CanUndo => !WriteGate.Frozen && journal != null && journal.CanUndo;
        public bool CanRedo => !WriteGate.Frozen && journal != null && journal.CanRedo;
        public event Action Changed;
        public event Action Editing;
        public event Action<RoomItem> ItemGrabbed;
        public event Action<string> ItemReleased, ItemTapped;
        public event Action<string,string,string,Vector3,float> ItemCollided;
        public string Identity(RoomItem item) => item && identities.TryGetValue(item,out var id) ? id : null;
        public void Tapped(RoomItem item) { if(ConstructionTap(item))return;var id = Identity(item); if (id != null) ItemTapped?.Invoke(id); }
        public int Revision { get; private set; } = 1;
        public Vector3 CreationPosition => SpawnPosition();
        internal Transform Viewer=>room?room.Viewer:null;
        public string SelectedId => selected;
        public RoomItem Find(string id) => id != null && objects.TryGetValue(id,out var value) ? value : null;
        public RoomObjectData Read(string id) => journal.Read(id);
        public bool AnyHeld => objects.Values.Any(item => item && item.Grab && item.Grab.isSelected);
        public RoomDocument Snapshot() => journal.Snapshot();
        public int ObjectRevision(string id) => journal.ObjectRevision(id);
        public void PrepareAgentEdit() => Editing?.Invoke();
        public bool UsesMotion(string id) => journal.UsesMotion(id) || savedJournal?.UsesMotion(id)==true || savingSnapshot?.objects.Any(x=>x.walkMotionId==id)==true;
        public bool HistoricalMotion(string id) => journal.HistoricalMotionIds.Contains(id) || savedJournal?.HistoricalMotionIds.Contains(id)==true;
        public bool SavedMotion(string id,out bool uncertain,bool force=false) => storage.RetainsMotion(id,out uncertain,force);
        public ModelLibrary Models { get; private set; }
        public BundledAvatar IncludedAvatar { get; private set; }
        public MotionLibrary Motions { get; private set; }
        public AvatarActivityProfiles ActivityProfiles { get; private set; }
        public RoomPhysicsWorld PhysicsWorld { get; private set; }
        public string SaveDirectory { get; private set; }
        public string ReceiptDirectory {get;private set;}

        public void Initialize(RoomInteraction interaction, RoomItem book, RoomItem maestro, string saveDirectory = null, RoomPhysicsWorld physics = null, RoomRuntimeGate runtimeGate = null, string receiptDirectory = null, BundledAvatar includedAvatar = null, BundledMotions includedMotions = null)
        {
            room = interaction; room.ConfigureWrites(WriteGate); RuntimeGate=runtimeGate??RuntimeGate;RuntimeGate.Changed+=RefreshOwnership;RefreshOwnership();
            PhysicsWorld = physics;PhysicsWorld?.ConfigureRuntime(RuntimeGate);
            AddIdentity("book", book); AddIdentity("maestro", maestro);
            var directory = saveDirectory ?? Path.Combine(Application.persistentDataPath, "room"); SaveDirectory=directory;ReceiptDirectory=receiptDirectory??directory;
            bool fresh=!Directory.Exists(directory)||!Directory.EnumerateFileSystemEntries(directory).Any();
            IncludedAvatar=includedAvatar;
            ActivityProfiles=new AvatarActivityProfiles(directory,WriteGate,fresh?includedMotions?.DefaultActivities(includedAvatar?.Hash):null);
            storage = new RoomStorage(directory); Models = new ModelLibrary(Path.Combine(directory, "models"),WriteGate,includedAvatar); Motions = new MotionLibrary(Path.Combine(directory,"motions"),WriteGate,includedMotions);
            var loaded = storage.Load(out var message);
            journal = new RoomJournal(loaded ?? StarterDocument(book, maestro, includedAvatar));
            maestro.GetComponent<MaestroAvatar>()?.ConfigureRuntime(RuntimeGate);
            maestro.GetComponent<MaestroAvatar>()?.ConfigureOwnership(Ownership,"maestro");
            Liquids=gameObject.AddComponent<LiquidPouring>();Liquids.Initialize(this);
            Reconcile();
            room.Restoring += BeforeRestore; room.Restored += AfterRestore;
            if (message != null) SetStatus(message);
        }

        static RoomDocument StarterDocument(RoomItem book, RoomItem maestro, BundledAvatar includedAvatar)
        {
            var items = new List<RoomObjectData> { Pose(new RoomObjectData { id = "book", kind = RoomObjectKind.Book }, book.transform), Pose(new RoomObjectData { id = "maestro", kind = RoomObjectKind.Maestro, modelHash = includedAvatar?.Hash, walkClip = (includedAvatar?.WalkClipIndex??-1)+1 }, maestro.transform) };
            var kinds = new[] { RoomObjectKind.Block, RoomObjectKind.Ball, RoomObjectKind.Cylinder };
            var colors = new[] { IllustratedMaterials.Cover, IllustratedMaterials.Ribbon, IllustratedMaterials.Hex("2B8D88") };
            for (int i = 0; i < kinds.Length; i++) items.Add(new RoomObjectData { id = Guid.NewGuid().ToString("N"), kind = kinds[i], color = colors[i], position = new Vector3(.52f + i * .17f,1.45f,.8f) });
            return new RoomDocument { version = RoomDocument.CurrentVersion, objects = items.ToArray() };
        }

        void AddIdentity(string id, RoomItem item)
        {
            objects.Add(id,item); identities.Add(item,id);item.ConfigureWrites(WriteGate);
            item.GrabStarted += GrabStarted; item.GrabFinished += GrabFinished;
            var rigid=item.GetComponent<RigidRoomItem>();if(rigid)rigid.ContactStarted+=ContactStarted;
        }
        void ContactStarted(RoomItem item,Collider other,Vector3 point,float speed) {
            string id=Identity(item);if(applying||Ownership.Suspended||id==null||!other)return;
            var otherItem=other.attachedRigidbody?other.attachedRigidbody.GetComponent<RoomItem>():other.GetComponentInParent<RoomItem>();
            string otherId=Identity(otherItem)??"";
            string kind=otherId!=""?"object":other.gameObject.layer==RoomPhysicsLayers.Scanned?"scannedRoom":other.gameObject.layer==RoomPhysicsLayers.Controller?"controller":"environment";
            ItemCollided?.Invoke(id,otherId,kind,point,speed);
        }
        void OwnHeld(RoomItem item) {
            var id=Identity(item);if(id==null||Ownership.Suspended)return;
            if(handOwners.TryGetValue(id,out var held)&&held.Held)return;
            if(Ownership.TryAcquire("hand:"+id,"Your grip",RoomActorRole.Grab,new[]{new Maestro.Quest.Programs.BehaviourCatalog.Claim(id,"wholeTarget")},
                null,out var lease,out var error,preservePlacement:true))handOwners[id]=lease;else SetStatus(error);
        }
        void GrabStarted(RoomItem item) { if (!applying) { OwnHeld(item);ItemGrabbed?.Invoke(item); Select(item); } }
        void GrabFinished(RoomItem item)
        {
            if (!identities.TryGetValue(item,out var id)) return;
            if(handOwners.Remove(id,out var lease))lease.Dispose();
            if(applying)return;
            FinishLiquidPour(out _);
            var before = journal.Read(id); if (before == null) return;
            var after = Pose(before, item.transform);
            if (!Commit(new[] { after }, Array.Empty<string>(), "Placed — saving", true)) ApplyPose(item, journal.Read(id));
            ItemReleased?.Invoke(id);
        }

        public void Select(RoomItem item)
        {
            if (!identities.TryGetValue(item,out selected)) return;
            UpdateSelection();
            var data = journal.Read(selected);
            SetStatus(data.IsBuiltIn ? data.kind + " — move or resize" : data.kind + " — paint, duplicate or erase");
        }

        public void Create(RoomObjectKind kind)
        {
            if (kind != RoomObjectKind.Block && kind != RoomObjectKind.Ball && kind != RoomObjectKind.Cylinder) return;
            if (CreatePrimitive(kind,"",SpawnPosition(),1,Paint,out var id,out var error)) { selected=id;UpdateSelection(); }
            else SetStatus(error);
        }

        public bool CanCreatePrimitive(out string error) {
            error=null;
            if(WriteGate.Frozen){error=Maestro.Quest.Persistence.WorkspaceWriteGate.FrozenReason;return false;}
            if(journal==null) {error="Room editor is not ready";return false;}
            if(storage.ReadOnly) {error="This room was saved by a newer app and is read-only";return false;}
            if(journal.Snapshot().objects.Length>=RoomDocument.MaximumObjects+2) {error="This room has reached its creation limit";return false;}
            return true;
        }
        public bool CreatePrimitive(RoomObjectKind kind,string name,Vector3 position,float scale,Color color,out string id,out string error) {
            id=null;
            if(!CanCreatePrimitive(out error))return false;
            if(kind!=RoomObjectKind.Block&&kind!=RoomObjectKind.Ball&&kind!=RoomObjectKind.Cylinder) {error="Choose a ball, block or cylinder";return false;}
            var item=new RoomObjectData {id=Guid.NewGuid().ToString("N"),name=name,kind=kind,position=position,scale=scale,color=color,
                physics=kind==RoomObjectKind.Ball?ItemPhysics.Bouncy:ItemPhysics.Solid};
            return CommitCreatedObject(item,out id,out error);
        }
        public bool CanCreateRecipe(RoomRecipe recipe,out string error)=>CanCreateRecipe(recipe,null,null,out error);
        public bool CanCreateRecipe(RoomRecipe recipe,CollisionRecipe collision,ObjectPhysicsSettings physics,out string error,DrawingSurface[] surfaces=null,DrawingTip[] drawingTips=null,RoomSnapPoint[] snapPoints=null,RoomContainer[] containers=null) {
            if(!CanCreatePrimitive(out error))return false;
            if(recipe==null) {error="Provide a construction recipe";return false;}
            if(!recipe.Validate(out error))return false;
            if(!DrawingSurface.ValidateCollection(new RoomObjectData {kind=RoomObjectKind.Assembly,recipe=recipe,surfaces=surfaces},out error))return false;
            if(Snapshot().objects.Sum(x=>x.surfaces?.Length??0)+(surfaces?.Length??0)>DrawingSurface.MaximumRoomSurfaces){error="This room has reached its drawing-surface limit";return false;}
            if(!DrawingTip.ValidateCollection(new RoomObjectData {kind=RoomObjectKind.Assembly,recipe=recipe,drawingTips=drawingTips},out error))return false;
            if(!RoomSnapPoint.ValidateCollection(new RoomObjectData{kind=RoomObjectKind.Assembly,snapPoints=snapPoints},out error))return false;
            if(Snapshot().objects.Sum(x=>x.snapPoints?.Length??0)+(snapPoints?.Length??0)>RoomSnapPoint.MaximumPerRoom){error="This room has reached its snap-point limit";return false;}
            if(!RoomContainer.ValidateCollection(new RoomObjectData{kind=RoomObjectKind.Assembly,containers=containers},out error))return false;
            if(Snapshot().objects.Sum(x=>x.containers?.Length??0)+(containers?.Length??0)>RoomContainer.MaximumPerRoom){error="Keep at most 16 liquid containers in this room";return false;}
            if(collision!=null&&!collision.Validate(out error))return false;
            if(physics!=null&&!RoomControls.ValidPhysics(physics)){error="Provide valid fixed/solid/bouncy physics, collision mode and mass";return false;}
            if(journal.Snapshot().objects.Sum(CollisionRecipe.ReservedPieces)+Math.Max(1,collision?.Pieces??0)>CollisionRecipe.MaximumRoomPieces){error="Collision shapes exceed the room piece budget";return false;}
            if(journal.Snapshot().objects.Sum(x=>RecipeGeometry.VertexCost(x.recipe))+RecipeGeometry.VertexCost(recipe)>RecipeGeometry.MaximumRoomVertices){error="Generated recipe geometry exceeds the room vertex budget";return false;}
            if(journal.Snapshot().objects.Sum(x=>x.recipe?.parts.Length??0)+recipe.parts.Length>256) {error="Keep at most 256 recipe parts in this room";return false;}
            return true;
        }
        public bool CreateRecipe(string name,Vector3 position,float scale,RoomRecipe recipe,out string id,out string error)=>CreateRecipe(name,position,scale,recipe,null,null,out id,out error);
        public bool CreateRecipe(string name,Vector3 position,float scale,RoomRecipe recipe,CollisionRecipe collision,ObjectPhysicsSettings physics,out string id,out string error,DrawingSurface[] surfaces=null,DrawingTip[] drawingTips=null,RoomSnapPoint[] snapPoints=null,RoomContainer[] containers=null) {
            id=null;if(!CanCreateRecipe(recipe,collision,physics,out error,surfaces,drawingTips,snapPoints,containers))return false;
            if(!PrepareRecipeObject(name,position,scale,recipe,collision,physics,out var item,out error,surfaces,drawingTips,snapPoints,containers))return false;
            return CommitCreatedObject(item,out id,out error);
        }
        bool CommitCreatedObject(RoomObjectData item,out string id,out string error) {
            id=null;
            if(!CommitPersisted(new[]{item},Array.Empty<string>(),item.kind+" added",false,out error))return false;
            id=item.id;return true;
        }
        public bool CanEditObject(string id,bool creationOnly,out string error) {
            error=null;
            if(WriteGate.Frozen){error=Maestro.Quest.Persistence.WorkspaceWriteGate.FrozenReason;return false;}
            if(journal==null){error="Room editor is not ready";return false;}
            if(Liquids?.Owns(id)==true){error="Finish pouring or pause room physics before editing this container";return false;}
            if(storage.ReadOnly){error="This room was saved by a newer app and is read-only";return false;}
            var data=Read(id);var item=Find(id);
            if(data==null||!item){error="This object was removed; inspect the room first";return false;}
            if(creationOnly&&data.IsBuiltIn){error="Choose a user-created object";return false;}
            if(item.Grab.isSelected){error="Release this object before editing it";return false;}
            if(item.GetComponent<RigidRoomItem>()?.AnimationOwned==true){error="An animation or carried prop owns this object";return false;}
            return true;
        }
        public bool MoveObject(string id,Vector3 position,out string error)=>EditObject(id,false,data=>data.position=position,"Object moved",true,out error);
        public bool RotateObject(string id,Quaternion rotation,out string error)=>EditObject(id,false,data=>data.rotation=rotation,"Object rotated",true,out error);
        public bool ResizeObject(string id,float scale,out string error)=>EditObject(id,false,data=>data.scale=scale,"Object resized",true,out error);
        public bool PaintObject(string id,Color color,out string error)=>EditObject(id,true,data=>data.color=color,"Object painted",false,out error);
        public bool DeleteObject(string id,out string error) {
            if(!CanEditObject(id,true,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),new[]{id},"Object deleted — Undo restores it",false,out error);
        }
        bool EditObject(string id,bool creationOnly,Action<RoomObjectData> change,string message,bool applyPose,out string error) {
            if(!CanEditObject(id,creationOnly,out error))return false;
            // Runtime physics can be newer than the last periodic saved placement.
            var data=Pose(Read(id),Find(id).transform);change(data);
            return CommitPersisted(new[]{data},Array.Empty<string>(),message,applyPose,out error);
        }
        bool CommitPersisted(RoomObjectData[] replacements,string[] removals,string message,bool applyPose,out string error,RoomLayout observedBefore=null,StructureEdits structureEdits=null) {
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            if(structureEdits!=null&&!structureEdits.Validate(out error))return false;
            var candidate=journal.Snapshot();
            var changed=replacements.Select(x=>x.id).Concat(removals).ToHashSet();
            candidate.objects=candidate.objects.Where(x=>!changed.Contains(x.id)).Concat(replacements).ToArray();
            if(structureEdits!=null)candidate.structures=structureEdits.Apply(candidate.structures);
            if(!candidate.Validate(out error))return false;
            if(observedBefore!=null&&!journal.PlacementBaseline(observedBefore,out _,out error))return false;
            if(TemporaryRoom) {
                if(!Commit(replacements,removals,message,true,applyPose,observedBefore,structureEdits)){error=Status;return false;}
                return true;
            }
            // Same serialized writer and journal as manual edits; no global Editing
            // signal here because the caller already owns only the affected targets.
            CompleteSave(wait:true);
            if(!storage.Save(candidate,out error))return false;
            if(!Commit(replacements,removals,message,true,applyPose,observedBefore,structureEdits)){error=Status;return false;}
            dirty=false;lastSaveError=null;return true;
        }

        Vector3 SpawnPosition()
        {
            if (!room.Viewer) return new Vector3(.3f,1.3f,.65f);
            var forward = Vector3.ProjectOnPlane(room.Viewer.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            return transform.InverseTransformPoint(room.Viewer.position + forward * .7f + room.Viewer.right * .3f - Vector3.up * .2f);
        }

        internal bool CreateImportedModel(string hash,out string id,out string error)
        {
            id=null;if(!CanCreatePrimitive(out error))return false;
            if(!ModelLibrary.ValidHash(hash)){error="Choose a verified model identity";return false;}
            var data=new RoomObjectData {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.ImportedModel,modelHash=hash,position=SpawnPosition()};
            return CommitCreatedObject(data,out id,out error);
        }
        public bool AddModel(string hash)
        {
            if(!CreateImportedModel(hash,out var id,out var error)){SetStatus(error);return false;}
            selected=id;UpdateSelection();return true;
        }
        public void ChoosePaint(Color color)
        {
            CapturePhysicsPlacements();
            Paint = color; Paint = new Color(Paint.r,Paint.g,Paint.b,1);
            if(DrawingMode){SetStatus("Pencil colour chosen for the next stroke");return;}
            var item = journal.Read(selected);
            if (item != null && !item.IsBuiltIn) { Editing?.Invoke(); if(!PaintObject(selected,Paint,out var error))SetStatus(error); }
            else SetStatus("Paint chosen for your next creation");
        }

        public void Duplicate()
        {
            var item=Read(selected);var live=Find(selected);
            if(item==null||item.IsBuiltIn||!live){SetStatus("Select one of your creations to duplicate");return;}
            if(CopyObject(selected,ObjectRevision(selected),item.name,live.transform.localPosition+Vector3.right*.18f,out var id,out var error)){selected=id;UpdateSelection();}
            else SetStatus(error);
        }

        public void Erase()
        {
            var item = journal.Read(selected);
            if (item == null || item.IsBuiltIn) { SetStatus("Select one of your creations to erase"); return; }
            Editing?.Invoke(); if(DeleteObject(selected,out var error)) { selected = null; UpdateSelection(); } else SetStatus(error);
        }

        public void Undo() { if(!FinishLiquidPour(out var liquidError)){SetStatus(liquidError);return;} using var write=WriteGate.TryWrite(out var blocked);if(write==null){SetStatus(blocked);return;} Editing?.Invoke(); if (Busy()) return; if (journal.Undo()) { Reconcile(); MarkDirty(); SetStatus("Undone"); } else SetStatus("Nothing to undo"); }
        public void Redo() { if(!FinishLiquidPour(out var liquidError)){SetStatus(liquidError);return;} using var write=WriteGate.TryWrite(out var blocked);if(write==null){SetStatus(blocked);return;} Editing?.Invoke(); if (Busy()) return; if (journal.Redo()) { Reconcile(); MarkDirty(); SetStatus("Redone"); } else SetStatus("Nothing to redo"); }
        internal bool DrawingInProgress=>GetComponent<SpatialDrawing>() is SpatialDrawing drawing&&(drawing.IsDrawing||drawing.HasUnsavedStroke);
        internal bool PutPencilAwayForPose(out string error)
        {
            error=null;if(DrawingInProgress){error="Finish the current stroke before posing";return false;}
            // The pose caller already coordinates its target. The manual global
            // Editing signal would cancel its own invocation and unrelated actors.
            DrawingMode=false;DrawingOnSurfaces=false;SurfaceErasing=false;SetStatus("Pencil put away");return true;
        }
        internal bool CanConfigureDrawing(out string error){error=DrawingInProgress?"Finish or discard the current stroke first":WriteGate.Frozen?"Finish the workspace operation first":Ownership.Suspended?"Room actions are paused":null;return error==null;}
        internal bool ConfigureDrawing(string mode,Color color,float radius,out string error) {
            if(!CanConfigureDrawing(out error))return false;
            if(!new[]{"off","space","surface","surfaceErase"}.Contains(mode)||!float.IsFinite(radius)||radius<.001f||radius>.02f||new[]{color.r,color.g,color.b}.Any(n=>!float.IsFinite(n)||n<0||n>1)){error="Choose a supported pencil mode, colour and thickness";return false;}
            if(mode!="off")SuspendConstructionPicking();
            DrawingMode=mode!="off";DrawingOnSurfaces=mode=="surface"||mode=="surfaceErase";SurfaceErasing=mode=="surfaceErase";DrawingRadius=radius;Paint=new Color(color.r,color.g,color.b,1);
            SetStatus(mode=="off"?"Pencil put away":mode=="surfaceErase"?"Surface eraser: tap a stroke to remove it":mode=="surface"?"Surface pencil: draw on an enabled patch within 25 cm":"Space pencil: hold trigger or pinch to draw");return true;
        }
        public void ToggleDrawing(){if(!ConfigureDrawing(DrawingMode&&!DrawingOnSurfaces?"off":"space",Paint,DrawingRadius,out var error))SetStatus(error);}
        public void ToggleSurfaceDrawing(){if(!ConfigureDrawing(DrawingMode&&DrawingOnSurfaces&&!SurfaceErasing?"off":"surface",Paint,DrawingRadius,out var error))SetStatus(error);}

        public bool AddDrawing(IReadOnlyList<Vector3> worldPoints, Color color)
        {
            if(worldPoints==null||worldPoints.Count<2)return false;
            var origin=transform.InverseTransformPoint(worldPoints[0]);
            var points=worldPoints.Select(point=>transform.InverseTransformPoint(point)-origin).ToArray();
            if(!CreateDrawing("",origin,1,color,.003f,points,out var id,out var error)){SetStatus(error);return false;}
            selected=id;UpdateSelection();return true;
        }

        public bool ApplyAgentEdit(int expectedRevision,RoomObjectData[] replacements,string[] removals,out string error)
        {
            error=null;
            if (expectedRevision != Revision) { error="The room changed. Read its current state before trying again."; return false; }
            if (AnyHeld) { error="Release the held object before editing."; return false; }
            foreach(var data in replacements)
            {
                var old=Read(data.id);
                if (old != null && (old.kind != data.kind || old.modelHash != data.modelHash)) { error="An edit cannot replace object identity or model."; return false; }
            }
            Editing?.Invoke();
            if (expectedRevision != Revision) { error="Authoring changed the room. Read its current state before trying again."; return false; }
            if (!Commit(replacements,removals,"Room request completed — Undo is available")) { error=Status; return false; }
            return true;
        }

        bool Commit(RoomObjectData[] replacements, string[] removals, string success, bool placement = false, bool? applyPose = null, RoomLayout observedBefore = null, StructureEdits structureEdits = null)
        {
            using var write=WriteGate.TryWrite(out var blocked);if(write==null){SetStatus(blocked);return false;}
            if (!placement) Editing?.Invoke();
            if (journal == null || (!placement && Busy())) return false;
            if (!journal.Apply(replacements,removals,out var error,observedBefore,structureEdits)) { SetStatus(error); return false; }
            Reconcile(replacements.Select(item => item.id).ToHashSet(), applyPose ?? !placement); MarkDirty(); SetStatus(success); return true;
        }
        public bool SetItemPhysics(string id,ObjectPhysicsSettings settings)
        {
            if(!RoomControls.SetPhysics(Read(id),settings,out var error)){SetStatus(error);return false;}
            Editing?.Invoke();if(Busy())return false;
            if(ConfigurePhysics(id,ObjectRevision(id),settings,out error))return true;SetStatus(error);return false;
        }
        public void CyclePhysics()
        {
            var data=journal.Read(selected);if(data==null || data.IsBuiltIn) {SetStatus("Select a creation to change its physics");return;}
            var settings=RoomControls.Physics(data);settings.mode=((ItemPhysics)(((int)data.physics+1)%3)).ToString().ToLowerInvariant();SetItemPhysics(selected,settings);
        }
        public void CycleMass()
        {
            var data=journal.Read(selected);if(data==null || data.IsBuiltIn) {SetStatus("Select a creation to change its mass");return;}
            var settings=RoomControls.Physics(data);float[] masses={.1f,.5f,1,2,5,10,20};settings.mass=masses.FirstOrDefault(value=>value>data.mass);if(settings.mass==0)settings.mass=.1f;SetItemPhysics(selected,settings);
        }
        public void CycleCollider()
        {
            var data=journal.Read(selected);if(data==null || data.IsBuiltIn) {SetStatus("Select a creation to change its collision shape");return;}
            var settings=RoomControls.Physics(data);settings.shape=((ItemCollider)(((int)data.collisionShape+1)%3)).ToString().ToLowerInvariant();SetItemPhysics(selected,settings);
        }
        public bool PlaceSelected(Vector3 worldPosition)
        {
            Editing?.Invoke(); if (Busy()) return false;
            var data = journal.Read(selected); if (data == null) return false;
            if(MoveObject(selected,transform.InverseTransformPoint(worldPosition),out var error))return true;SetStatus(error);return false;
        }
        public bool SaveAnimation(string id, RoomMotion motion, JointPose[] joints, bool savePose)
        {
            bool saved=WriteAnimation(id,ObjectRevision(id),motion,joints,savePose,out var error);
            if(!saved)SetStatus(error);return saved;
        }
        internal bool WriteAnimation(string id,int revision,RoomMotion motion,JointPose[] joints,bool savePose,out string error)
        {
            error="The object changed; inspect its animation before editing";
            if(journal==null||ObjectRevision(id)!=revision)return false;
            var data=Read(id);if(data==null)return false;
            data.motion=motion?.Copy();if(savePose)data.joints=MotionFrame.CopyJoints(joints);
            // Physical authoring already owns the target (and can be recording a grip).
            // Remote callers separately require released/idle ownership before entering.
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Animation saved",false,out error);
        }
        public void RestorePose(string id)
        {
            var item = Find(id); var data = journal.Read(id);
            if (!item || data == null) return;
            ApplyPose(item,data); item.GetComponent<MaestroAvatar>()?.SetSavedPose(data.joints);
        }
        bool Busy()
        {
            if(DrawingInProgress){SetStatus("Finish or discard the current stroke before editing");return true;}
            if (!objects.Values.Any(item => item && item.Grab && item.Grab.isSelected)) return false;
            SetStatus("Release the object before editing"); return true;
        }

        void Reconcile(HashSet<string> changed = null, bool applyChangedPose = true)
        {
            applying = true;
            var document = journal.Snapshot(); var ids = document.objects.Select(item => item.id).ToHashSet();
            foreach (var id in objects.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                var item = objects[id]; room.Unregister(item); identities.Remove(item); objects.Remove(id);
                item.gameObject.SetActive(false); Destroy(item.gameObject);
            }
            int slot = 0;
            foreach (var data in document.objects)
            {
                bool created = false;
                if (!objects.TryGetValue(data.id,out var item))
                {
                    var root = new GameObject(data.kind.ToString()); root.transform.SetParent(transform,false);
                    // Canonical scale is linked to XRI before restoring saved pose/scale.
                    item = root.AddComponent<CreatedRoomObject>().Build(data, Models,RuntimeGate);
                    AddIdentity(data.id,item); room.Register(item);
                    created = true;
                }
                if (!item.Grab.isSelected && (created || changed == null || (applyChangedPose && changed.Contains(data.id)))) ApplyPose(item,data);
                if(created || changed==null || changed.Contains(data.id)) {
                item.GetComponent<CreatedRoomObject>()?.ApplyRecipe(data.recipe);
                item.GetComponent<CreatedRoomObject>()?.ApplyDrawing(data);
                var surfaces=item.GetComponent<DrawingSurfaceView>();if(!surfaces&&(data.surfaces?.Length??0)>0)surfaces=item.gameObject.AddComponent<DrawingSurfaceView>();if(surfaces)surfaces.Apply(data.surfaces);
                var tip=item.GetComponent<DrawingTipView>();if(!tip&&(data.drawingTips?.Length??0)>0)tip=item.gameObject.AddComponent<DrawingTipView>();if(tip)tip.Apply(this,data.id,data.drawingTips);
                var liquid=item.GetComponent<ContainerFillView>();if(!liquid&&(data.containers?.Length??0)>0)liquid=item.gameObject.AddComponent<ContainerFillView>();if(liquid)liquid.Apply(data.containers);
                item.GetComponent<CreatedRoomObject>()?.ApplyCollision(data.collision);
                item.GetComponent<CreatedRoomObject>()?.SetCollisionShape(data.collisionShape);
                item.GetComponent<RigidRoomItem>()?.Configure(PhysicsWorld,data.physics,data.mass);
                item.GetComponent<MaestroAvatar>()?.SetSavedPose(data.joints);
                var avatar = item.GetComponent<MaestroAvatar>(); if (avatar) { avatar.ConfigureActivityProfiles(ActivityProfiles,Motions); avatar.SetWalkReference(data.walkClip-1,data.walkMotionId,Motions); _ = avatar.SetModel(data.modelHash,Models); }
                }
                if (!data.IsBuiltIn)
                {
                    item.SetHome(new Vector3(-.63f + (slot % 8) * .18f,.7f + ((slot / 8) % 4) * .18f,1.15f + (slot / 32) * .25f),Quaternion.identity,Vector3.one);
                    item.GetComponent<CreatedRoomObject>().ApplyColor(data.color); slot++;
                }
            }
            // Resolve links only after every member and its current pose/collider exists.
            foreach(var data in document.objects){var item=Find(data.id);var hinge=item.GetComponent<RoomConnectionView>();if(!hinge&&(data.connections?.Length??0)>0)hinge=item.gameObject.AddComponent<RoomConnectionView>();if(hinge)hinge.Apply(this,data.connections);}
            Liquids?.Synchronize(document);
            applying = false; UpdateSelection();
        }

        static void ApplyPose(RoomItem item, RoomObjectData data)
        {
            item.transform.SetLocalPositionAndRotation(data.position,data.rotation); item.transform.localScale = Vector3.one * data.scale;
            item.GetComponent<RigidRoomItem>()?.Teleported();
        }
        static RoomObjectData Pose(RoomObjectData data, Transform pose)
        { data.position = pose.localPosition; data.rotation = pose.localRotation.normalized; data.scale = pose.localScale.x; return data; }

        void UpdateSelection()
        {
            ReconcileConstructionSelection();
            foreach (var pair in objects) { var created = pair.Value.GetComponent<CreatedRoomObject>(); if (created) created.SetSelection(pair.Key == selected,constructionMembers.Contains(pair.Key)); }
            Changed?.Invoke();
        }
        void BeforeRestore() { FinishLiquidPour(out _); Editing?.Invoke(); applying = true; }
        void AfterRestore()
        {
            applying = false;
            var placements = identities.Select(pair => Pose(journal.Read(pair.Value),pair.Key.transform)).ToArray();
            Commit(placements,Array.Empty<string>(),"Room brought back within reach");
        }

        void MarkDirty() { Revision++; dirty = !TemporaryRoom; saveAt = Time.unscaledTime + .5f; }
        public void RememberPlacement(string id)
        {
            using var write=WriteGate.TryWrite(out _);if(write==null)return;
            var item = Find(id);
            if (item && journal.UpdatePlacement(id,item.transform.localPosition,item.transform.localRotation.normalized)) MarkDirty();
        }
        public void SetAvatarMovement(float distance, float speed)
        {
            var settings=new AvatarMovementSettings {distance=distance,speed=speed};
            if(!RoomControls.SetMovement(Read("maestro"),settings,out var error)){SetStatus(error);return;}
            Editing?.Invoke();if(Busy())return;
            if(!ConfigureMovement(ObjectRevision("maestro"),settings,out error))SetStatus(error);
        }
        public bool SetAvatarWalkClip(int index)
        {
            if(index==-1)return SetAvatarWalkMotion("");var data=Read("maestro");
            if(!AvatarWalkSelection.ApplyEmbedded(this,data,data?.modelHash,index,out var error)){SetStatus(error);return false;}
            Editing?.Invoke();if(Busy())return false;
            if(ConfigureWalk(ObjectRevision("maestro"),"embedded",null,data.modelHash,index,out error))return true;SetStatus(error);return false;
        }
        public bool SetAvatarWalkMotion(string id)
        {
            if(!AvatarWalkSelection.Apply(this,Read("maestro"),id,out var error)){SetStatus(error);return false;}
            Editing?.Invoke();if(Busy())return false;
            if(ConfigureWalk(ObjectRevision("maestro"),id==""?"included":"library",id,null,-1,out error))return true;SetStatus(error);return false;
        }
        public bool SetAvatarSize(float scale)
        {
            Editing?.Invoke(); if (Busy()) return false;
            if(ResizeObject("maestro",scale,out var error))return true;SetStatus(error);return false;
        }
        public void SaveNow() {
            if(!FinishLiquidPour(out var liquidError)){SetStatus(liquidError);return;}
            using var write=WriteGate.TryWrite(out var blocked);if(write==null){SetStatus(blocked);return;}
            if(TemporaryRoom) {if(!KeepTemporaryRoom(out var error))SetStatus(error);return;}
            CapturePhysicsPlacements(); MarkDirty(); saveAt = 0; SetStatus("Saving room");
        }
        void CapturePhysicsPlacements()
        {
            using var write=WriteGate.TryWrite(out _);if(write==null)return;
            if (journal == null) return;
            foreach (var pair in objects)
            {
                var item = pair.Value; if (!item || item.Grab.isSelected) continue;
                var rigid = item.GetComponent<RigidRoomItem>();
                if (!rigid || !rigid.Dynamic || rigid.AnimationOwned) continue;
                if (journal.UpdatePlacement(pair.Key,item.transform.localPosition,item.transform.localRotation.normalized)) MarkDirty();
            }
        }
        float captureAt;
        void Update()
        {
            CompleteTemporarySave();
            if (Time.unscaledTime >= captureAt) { captureAt = Time.unscaledTime + 1; CapturePhysicsPlacements(); }
            CompleteSave();
            if (saveDispatch.Frozen || journal == null || TemporaryRoom || !dirty || saveTask != null || Time.unscaledTime < saveAt) return;
            var snapshot = journal.Snapshot(); dirty = false;
            saveTask = Task.Run(() => SaveSnapshot(snapshot));
        }
        string SaveSnapshot(RoomDocument snapshot)
        {
            try { return storage.Save(snapshot,out var error) ? null : error ?? "Room save was not confirmed; your edits remain unsaved."; }
            catch (Exception) { return "Room save was not confirmed; your edits remain unsaved. Check storage and try again."; }
        }
        void CompleteSave(bool wait=false)
        {
            if (saveTask == null || !wait && !saveTask.IsCompleted) return;
            string error;
            try { error=saveTask.GetAwaiter().GetResult(); }
            catch(Exception) { error="Room save was not confirmed; your edits remain unsaved."; }
            saveTask=null;
            // A failed older snapshot cannot mark the current journal as saved.
            // Retry the latest accepted state, with a delay to avoid hammering storage.
            if (error != null) SaveFailed(error);
            else if (!dirty) { if(lastSaveError!=null)ClearSaveError();else SetStatus("Room saved"); }
        }
        void SaveFailed(string error)
        {
            dirty=true;saveAt=Time.unscaledTime+5;lastSaveError=error;SetStatus(error);
        }
        void ClearSaveError()
        {
            string previous=lastSaveError;lastSaveError=null;
            if(previous!=null && Status==previous)SetStatus("Room saved");
        }
        // Called only under a workspace edit hold. The dispatch lease outlives the
        // worker so Update/lifecycle callbacks cannot start another writer meanwhile.
        internal IDisposable SaveAcceptedAsync(out Task<string> completion)
        {
            completion=Task.FromResult("Hold workspace edits before saving accepted contents.");
            if(!WriteGate.Frozen)return null;
            var held=saveDispatch.TryFreeze(out var blocked);
            if(held==null){completion=Task.FromResult(blocked);return null;}
            try {
                CompleteSave();
                if(journal == null || storage == null || storage.ReadOnly){completion=Persistence.WorkspaceAcceptedSave.FailureAfter(saveTask,"Room storage is unavailable; original files are preserved.");return held;}
                if(TemporaryRoom){completion=Persistence.WorkspaceAcceptedSave.FailureAfter(saveTask,"Keep or discard the temporary room before saving the ordinary workspace.");return held;}
                var previous=saveTask;
                if(!dirty && previous==null){completion=Task.FromResult<string>(null);return held;}
                var snapshot=journal.Snapshot();
                // A failed older snapshot must not prevent saving the latest accepted
                // one. Sequence writers without waiting on the Unity owner thread.
                completion=saveTask=Task.Run(async()=>{
                    if(previous!=null)try{await previous.ConfigureAwait(false);}catch(Exception){}
                    return SaveSnapshot(snapshot);
                });
                dirty=false;return held;
            }catch(Exception){completion=Persistence.WorkspaceAcceptedSave.FailureAfter(saveTask,"Room save could not begin; accepted edits remain unsaved.");return held;}
        }
        internal bool TryFlush(out string error)
        {
            if(!FinishLiquidPour(out error))return false;
            if(saveDispatch.Frozen){error="Room saves are paused while recovery preserves original files.";return false;}
            if (journal == null || storage == null) { error="Room storage is not ready."; return false; }
            if (TemporaryRoom) { error="Keep or discard the temporary room before saving the ordinary workspace."; return false; }
            CapturePhysicsPlacements(); CompleteSave(wait:true);
            if(storage.ReadOnly){error="Room storage is unavailable; original files are preserved.";return false;}
            if (!dirty) return true;
            int revision=Revision;
            error=SaveSnapshot(journal.Snapshot());
            dirty=error!=null || Revision!=revision;
            if (error != null) SaveFailed(error);
            else if(!dirty)ClearSaveError();
            return error==null;
        }
        void Flush()
        {
            // Lifecycle callbacks can finish a dispatched baseline/Keep but must
            // never persist the later live fork, even when its baseline failed.
            if(TemporaryRoom) {CompleteTemporarySave(wait:true);return;}
            if(journal!=null)TryFlush(out _);
        }
        public void ReportStatus(string value)=>SetStatus(value);
        void SetStatus(string value) { Status = value; Changed?.Invoke(); }
        void RefreshOwnership() {
            Ownership.Suspend(ownershipPaused||!ownershipFocused||RuntimeGate.Held);
            if(Ownership.Suspended){SuspendConstructionPicking();ClearViewCapture();}
            if(!Ownership.Suspended)foreach(var item in objects.Values)if(item&&item.Grab.isSelected)OwnHeld(item);
        }
        void OnApplicationPause(bool paused) { ownershipPaused=paused;RefreshOwnership();if (paused) Flush(); }
        void OnApplicationFocus(bool focused) { ownershipFocused=focused;RefreshOwnership();if (!focused) Flush(); }
        void OnApplicationQuit() => Flush();
        void OnDestroy()
        {
            ClearViewCapture();FinishLiquidPour(out _);
            RuntimeGate.Changed-=RefreshOwnership;Ownership.Suspend(true);Flush(); Motions?.Dispose();
            if (room) { room.Restoring -= BeforeRestore; room.Restored -= AfterRestore; }
            foreach (var item in objects.Values) if (item) { item.GrabStarted -= GrabStarted; item.GrabFinished -= GrabFinished;var rigid=item.GetComponent<RigidRoomItem>();if(rigid)rigid.ContactStarted-=ContactStarted; }
        }
    }
}
