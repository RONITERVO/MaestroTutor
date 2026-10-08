// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;

namespace Maestro.Quest.Creation
{
    public enum RoomObjectKind { Book, Maestro, Block, Ball, Cylinder, Drawing, ImportedModel, Assembly }

    [Serializable]
    public sealed class RoomObjectData
    {
        public string id;
        public string name;
        public RoomRecipe recipe;
        public string environmentProfile="";
        public AppearanceBinding[] appearanceBindings=Array.Empty<AppearanceBinding>();
        public ScanDrawingAnchor[] scanAnchors=Array.Empty<ScanDrawingAnchor>();
        public CollisionRecipe collision;
        public DrawingSurface[] surfaces=Array.Empty<DrawingSurface>();
        public DrawingTip[] drawingTips=Array.Empty<DrawingTip>();
        public RoomConnection[] connections=Array.Empty<RoomConnection>();
        public RoomSnapPoint[] snapPoints=Array.Empty<RoomSnapPoint>();
        public RoomContainer[] containers=Array.Empty<RoomContainer>();
        public RoomHeightField[] heightFields=Array.Empty<RoomHeightField>();
        public SculptTip[] sculptTips=Array.Empty<SculptTip>();
        public RoomMaterialStore[] materialStores=Array.Empty<RoomMaterialStore>();
        public RoomAudioEmitter[] audioEmitters=Array.Empty<RoomAudioEmitter>();
        public RoomObjectKind kind;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float scale = 1;
        public Color color = Color.white;
        public Vector3[] points;
        public float radius = .003f;
        public JointPose[] joints;
        public RoomMotion motion;
        public string modelHash;
        public ItemPhysics physics;
        public ItemCollider collisionShape;
        public float mass = .5f;
        // Zero preserves the defaults when loading rooms created before movement controls.
        public float followDistance, walkSpeed;
        // Zero means the included gait; positive values are one-based clips of modelHash.
        public int walkClip;
        public string walkMotionId;
        public bool IsBuiltIn => kind == RoomObjectKind.Book || kind == RoomObjectKind.Maestro;
        public RoomObjectData Copy() => new() { id = id, name = name, appearanceBindings=(appearanceBindings??Array.Empty<AppearanceBinding>()).Select(x=>x.Copy()).ToArray(),environmentProfile=environmentProfile, scanAnchors=scanAnchors?.Select(a=>a?.Copy()).ToArray(), recipe = recipe?.Copy(), collision=collision?.Copy(), surfaces=surfaces?.Select(s=>s?.Copy()).ToArray(), drawingTips=drawingTips?.Select(t=>t?.Copy()).ToArray(), connections=connections?.Select(h=>h?.Copy()).ToArray(), snapPoints=snapPoints?.Select(p=>p?.Copy()).ToArray(), containers=containers?.Select(c=>c?.Copy()).ToArray(), heightFields=heightFields?.Select(f=>f?.Copy()).ToArray(), sculptTips=sculptTips?.Select(t=>t?.Copy()).ToArray(), materialStores=materialStores?.Select(s=>s?.Copy()).ToArray(), audioEmitters=audioEmitters?.Select(s=>s?.Copy()).ToArray(), kind = kind, position = position, rotation = rotation, scale = scale, color = color, radius = radius, points = points == null ? null : (Vector3[])points.Clone(), joints = MotionFrame.CopyJoints(joints), motion = motion?.Copy(), modelHash = modelHash, physics = physics, mass = mass, collisionShape = collisionShape, followDistance = followDistance, walkSpeed = walkSpeed, walkClip = walkClip, walkMotionId = walkMotionId };
    }

    [Serializable]
    public sealed class RoomDocument
    {
        public const int CurrentVersion=25;
        public const int MaximumObjects = 64;
        public const int MaximumStrokePoints = 2048;
        public const int MaximumTotalPoints = 32768;
        public int version;
        public RoomViewpoint viewpoint = new();
        public RoomWorldIdentity world = RoomWorldIdentity.Create();
        [NonSerialized] internal bool WorldNeedsSave;
        public RoomObjectData[] objects = Array.Empty<RoomObjectData>();
        public RoomStructure[] structures = Array.Empty<RoomStructure>();
        public RoomAudioDefinition[] audioSources = Array.Empty<RoomAudioDefinition>();
        public RoomEnvironmentProfile[] environmentProfiles=Array.Empty<RoomEnvironmentProfile>();
        public RoomAppearance[] appearances=Array.Empty<RoomAppearance>();

        public static (float minimum, float maximum) ScaleLimits(RoomObjectKind kind) => kind switch {
            RoomObjectKind.Book => (.65f, 1.8f), RoomObjectKind.Maestro => (.3f, 1.5f), _ => (.1f, 4f)
        };

        public bool Validate(out string error)
        {
            error = null;
            if (version != 1 && version != 2 && version != 3 && version != 4 && version != 5 && version != 7 && version != 8 && version != 9 && version != 10 && version != 11 && version != 12 && version != 13 && version != 14 && version != 15 && version != 16 && version != 17 && version != 18 && version != 19 && version != 20 && version != 21 && version != 22 && version != 23 && version != 24 && version != CurrentVersion || objects == null || objects.Length < 2 || objects.Length > MaximumObjects + 2)
                return Fail("This room file has an unsupported version or object count.", out error);
            if(world!=null&&!world.Valid||version>=23&&world==null)return Fail("This room has an invalid world or region identity.",out error);
            if(version>=22&&(viewpoint==null||!viewpoint.Valid))return Fail("This room has an invalid saved viewpoint.",out error);
            var ids = new HashSet<string>(); int partCount = 0; int pointCount = 0, builtIns = 0, frameCount = 0, jointCount = 0;
            foreach (var item in objects)
            {
                if (item == null || !Enum.IsDefined(typeof(RoomObjectKind), item.kind) || string.IsNullOrEmpty(item.id) || !ids.Add(item.id))
                    return Fail("This room contains invalid or duplicate objects.", out error);
                if (item.name != null && (item.name.Length > 80 || item.name.Any(char.IsControl))) return Fail("Object names must be at most 80 readable characters.",out error);
                if (item.kind == RoomObjectKind.Assembly ? item.recipe == null || !item.recipe.Validate(out _) : item.recipe != null) return Fail("An object has an invalid construction recipe.",out error);
                if(version<11&&item.recipe?.parts.Any(p=>p.shape=="sweep")==true)return Fail("Sweeps require the current room format.",out error);
                if(version<10&&item.recipe?.parts.Any(p=>p.shape=="extrude")==true)return Fail("Extrusion requires the current room format.",out error);
                partCount += item.recipe?.parts.Length ?? 0;
                if(version<4&&(item.surfaces?.Length??0)>0)return Fail("Drawing surfaces require the current room format.",out error);
                if(!DrawingSurface.ValidateCollection(item,out error))return false;
                if(version<12&&item.surfaces?.Any(s=>s.version>1)==true)return Fail("Curved drawing surfaces require the current room format.",out error);
                if(version<5&&(item.drawingTips?.Length??0)>0)return Fail("Drawing tips require the current room format.",out error);
                if(!DrawingTip.ValidateCollection(item,out error))return false;
                if(version<14&&item.recipe?.parts?.Any(p=>p.pattern?.Enabled==true)==true)return Fail("Recipe patterns require the current room format.",out error);
                if(version<13&&item.drawingTips?.Any(t=>t.version>1)==true)return Fail("Held erasers require the current room format.",out error);
                if(version<8&&(item.snapPoints?.Length??0)>0)return Fail("Snap points require the current room format.",out error);
                if(!RoomSnapPoint.ValidateCollection(item,out error))return false;
                if(version<9&&(item.containers?.Length??0)>0)return Fail("Liquid containers require the current room format.",out error);
                if(!RoomContainer.ValidateCollection(item,out error))return false;
                if(version<19&&item.containers?.Any(c=>c.version>1)==true)return Fail("Rectangular liquid cavities require the current room format.",out error);
                if(version<15&&(item.heightFields?.Length??0)>0)return Fail("Height surfaces require the current room format.",out error);
                if(!RoomHeightField.ValidateCollection(item,out error))return false;
                if(version<16&&(item.sculptTips?.Length??0)>0)return Fail("Sculpt tips require the current room format.",out error);
                if(!SculptTip.ValidateCollection(item,out error))return false;
                if(version<18&&item.sculptTips?.Any(t=>t.version>1)==true)return Fail("Material scoops require the current room format.",out error);
                if(version<17&&(item.materialStores?.Length??0)>0)return Fail("Measured material stores require the current room format.",out error);
                if(!RoomMaterialStore.ValidateCollection(item,out error))return false;
                if(version<7&&(item.connections?.Length??0)>0)return Fail("Physical connections require the current room format.",out error);
                pointCount+=DrawingSurface.PointCount(item);
                bool mayHaveModel = item.kind == RoomObjectKind.ImportedModel || item.kind == RoomObjectKind.Maestro;
                if (!string.IsNullOrEmpty(item.walkMotionId) && (version < 2 || item.kind != RoomObjectKind.Maestro || !Guid.TryParseExact(item.walkMotionId,"N",out _) || item.walkClip != 0))
                    return Fail("The library walking motion reference is invalid.",out error);
                if (item.walkClip < 0 || item.walkClip > 32 || item.walkClip > 0 && (item.kind != RoomObjectKind.Maestro || !ModelLibrary.ValidHash(item.modelHash)))
                    return Fail("The walking clip reference is invalid.",out error);
                if (!float.IsFinite(item.followDistance) || !float.IsFinite(item.walkSpeed) ||
                    item.followDistance != 0 && (item.kind != RoomObjectKind.Maestro || item.followDistance < .8f || item.followDistance > 2.5f) ||
                    item.walkSpeed != 0 && (item.kind != RoomObjectKind.Maestro || item.walkSpeed < .2f || item.walkSpeed > 1.2f))
                    return Fail("Maestro movement settings are invalid.",out error);
                if (item.kind == RoomObjectKind.ImportedModel ? !ModelLibrary.ValidHash(item.modelHash) :
                    !string.IsNullOrEmpty(item.modelHash) && (!mayHaveModel || !ModelLibrary.ValidHash(item.modelHash)))
                    return Fail("This room contains an invalid model reference.", out error);
                if (!Enum.IsDefined(typeof(ItemPhysics),item.physics) || !Enum.IsDefined(typeof(ItemCollider),item.collisionShape) || !float.IsFinite(item.mass) || item.mass < .05f || item.mass > 20 || (item.IsBuiltIn && (item.physics != ItemPhysics.Fixed || item.collisionShape != ItemCollider.Automatic)))
                    return Fail("An object has invalid physics settings.",out error);
                if(item.collision!=null&&(item.IsBuiltIn||!item.collision.Validate(out _)))return Fail("An object has invalid collision shapes.",out error);
                if (item.IsBuiltIn)
                {
                    if (item.id != (item.kind == RoomObjectKind.Book ? "book" : "maestro")) return Fail("The included book and Maestro identities are invalid.", out error);
                    builtIns++;
                }
                else if (!Guid.TryParseExact(item.id, "N", out _)) return Fail("This room contains an invalid object identity.", out error);
                var limits = ScaleLimits(item.kind);
                float norm = item.rotation.x * item.rotation.x + item.rotation.y * item.rotation.y + item.rotation.z * item.rotation.z + item.rotation.w * item.rotation.w;
                if (!Finite(item.position) || item.position.sqrMagnitude > 625 || !float.IsFinite(norm) || Mathf.Abs(norm - 1) > .01f ||
                    !float.IsFinite(item.scale) || item.scale < limits.minimum - .0001f || item.scale > limits.maximum + .0001f)
                    return Fail("An object has an invalid position, rotation or size.", out error);
                var c = item.color;
                if (!Unit(c.r) || !Unit(c.g) || !Unit(c.b) || !float.IsFinite(c.a) || Mathf.Abs(c.a - 1) > .001f)
                    return Fail("An object has an invalid paint color.", out error);
                if(version<20&&ScanDrawingAnchor.Has(item))return Fail("Scanned ink layers require the current room format.",out error);
                if(!ScanDrawingAnchor.ValidateOwner(item,out error))return false;
                if (item.kind == RoomObjectKind.Drawing&&!ScanDrawingAnchor.Has(item))
                {
                    if(!ValidateDrawing(item.points,item.radius,out error))return false;
                    pointCount+=item.points.Length;
                }
                else if (item.points != null && item.points.Length != 0) return Fail("Only drawings can contain stroke points.", out error);
                if (!MotionFrame.ValidJoints(item.joints) || (item.kind != RoomObjectKind.Maestro && item.joints?.Length > 0) || (item.motion != null && !item.motion.Validate(item.kind)))
                    return Fail("An object has an invalid pose or animation.",out error);
                frameCount += item.motion?.frames.Length ?? 0;
                jointCount += item.motion?.frames.Sum(frame => frame.joints?.Length ?? 0) ?? 0;
            }
            if (builtIns != 2 || !ids.Contains("book") || !ids.Contains("maestro")) return Fail("The included book and Maestro must remain in the room.", out error);
            if(objects.Sum(item=>RecipeGeometry.VertexCost(item.recipe))>RecipeGeometry.MaximumRoomVertices)return Fail("Generated recipe geometry exceeds the room vertex budget.",out error);
            if(objects.Sum(CollisionRecipe.ReservedPieces)>CollisionRecipe.MaximumRoomPieces)return Fail("This room has reached its collision-piece budget.",out error);
            if(objects.Sum(x=>x.containers?.Length??0)>RoomContainer.MaximumPerRoom)return Fail("Keep at most 16 liquid containers in this room.",out error);
            if(objects.Sum(x=>x.heightFields?.Length??0)>RoomHeightField.MaximumPerRoom)return Fail("Keep at most four height surfaces in this room.",out error);
            if(objects.Sum(x=>x.materialStores?.Length??0)>RoomMaterialStore.MaximumPerRoom)return Fail("Keep at most 16 measured material stores in this room.",out error);
            if (partCount > 256) return Fail("Keep at most 256 recipe parts in this room.",out error);
            if(objects.Sum(x=>x.snapPoints?.Length??0)>RoomSnapPoint.MaximumPerRoom)return Fail("This room has reached its snap-point budget.",out error);
            if(objects.Sum(x=>x.surfaces?.Length??0)>DrawingSurface.MaximumRoomSurfaces||objects.Sum(DrawingSurface.StrokeCount)>DrawingSurface.MaximumRoomStrokes)return Fail("This room has reached its surface drawing budget.",out error);
            if (pointCount > MaximumTotalPoints) return Fail("This room has reached its drawing limit.", out error);
            if (frameCount > 1200 || jointCount > 6000) return Fail("This room has reached its animation limit.",out error);
            if (objects.Count(item => item.kind == RoomObjectKind.ImportedModel) > 4) return Fail("Keep at most four imported models in this room.", out error);
            if(version<3 && (structures?.Length??0)>0)return Fail("Structures require the current room format.",out error);
            if(!RoomConnection.ValidateCollection(objects,out error))return false;
            if(!RoomAppearance.ValidateCollection(appearances??(version<25?Array.Empty<RoomAppearance>():null),objects,version,out error))return false;
            if(!RoomEnvironmentProfile.ValidateCollection(environmentProfiles??(version<24?Array.Empty<RoomEnvironmentProfile>():null),objects,version,out error))return false;
            if(!RoomAudioDefinition.ValidateCollection(audioSources??(version<21?Array.Empty<RoomAudioDefinition>():null),objects,version,out error))return false;
            return RoomStructure.ValidateCollection(structures??(version<3?Array.Empty<RoomStructure>():null),ids,out error);
        }

        internal static bool ValidateDrawing(Vector3[] points,float radius,out string error)
        {
            error=null;
            if(points==null||points.Length<2||points.Length>MaximumStrokePoints||!float.IsFinite(radius)||radius<.001f||radius>.02f)return Fail("A drawing exceeds the supported size.",out error);
            bool hasLength=false;
            foreach(var point in points){if(!Finite(point)||point.sqrMagnitude>100)return Fail("A drawing contains invalid coordinates.",out error);hasLength|=(point-points[0]).sqrMagnitude>.000001f;}
            return hasLength||Fail("A drawing must have a visible stroke.",out error);
        }
        static bool Unit(float value) => float.IsFinite(value) && value >= 0 && value <= 1;
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool Fail(string message, out string error) { error = message; return false; }
        public RoomDocument Copy() => new() { version = version, appearances=appearances?.Select(x=>x.Copy()).ToArray(), environmentProfiles=environmentProfiles?.Select(x=>x.Copy()).ToArray(), world=world?.Copy(), WorldNeedsSave=WorldNeedsSave, viewpoint=viewpoint?.Copy(), objects = objects.Select(item => item.Copy()).ToArray(), structures = structures?.Select(item=>item.Copy()).ToArray()??Array.Empty<RoomStructure>(), audioSources=audioSources?.Select(item=>item.Copy()).ToArray()??Array.Empty<RoomAudioDefinition>() };
    }

    /// <summary>Bounded object deltas preserve drawings without retaining whole scene copies.</summary>
    public sealed partial class RoomJournal
    {
        sealed class Change { public RoomObjectData[] Before, After; public RoomStructure[] BeforeStructures,AfterStructures; public RoomAudioDefinition[] BeforeAudio,AfterAudio; public RoomEnvironmentProfile[] BeforeEnvironments,AfterEnvironments; public RoomAppearance[] BeforeAppearances,AfterAppearances; }
        readonly List<Change> undo = new(), redo = new();
        readonly Dictionary<string, RoomObjectData> items = new();
        readonly Dictionary<string,int> revisions = new();
        // Forks share one monotonic clock: an Undo, discard or recreated object
        // must never make a stale observation current again.
        sealed class RevisionClock { public int Next = 1; }
        readonly RevisionClock clock = new();
        readonly RoomWorldIdentity world;
        internal RoomWorldIdentity WorldIdentity=>world.Copy();
        public int ObjectRevision(string id) => id != null && revisions.TryGetValue(id,out var value) ? value : 0;
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public bool UsesMotion(string id) => items.Values.Any(x => x.walkMotionId == id);
        public string[] HistoricalMotionIds => undo.Concat(redo).SelectMany(x => x.Before.Concat(x.After)).Select(x => x.walkMotionId).Where(x => x != null).Distinct().ToArray();
        public RoomJournal(RoomDocument document)
        {
            if (!document.Validate(out var error)) throw new ArgumentException(error, nameof(document));
            world=document.world?.Copy()??RoomWorldIdentity.Create();
            viewpoint=document.version>=22?document.viewpoint.Copy():new RoomViewpoint();
            foreach (var item in document.objects) { items.Add(item.id, item.Copy()); revisions[item.id]=clock.Next++; }
            SetStructures(Array.Empty<RoomStructure>(),document.structures??Array.Empty<RoomStructure>());
            SetAudio(Array.Empty<RoomAudioDefinition>(),document.audioSources??Array.Empty<RoomAudioDefinition>());
            SetEnvironments(Array.Empty<RoomEnvironmentProfile>(),document.environmentProfiles??Array.Empty<RoomEnvironmentProfile>());
            SetAppearances(Array.Empty<RoomAppearance>(),document.appearances??Array.Empty<RoomAppearance>());
        }
        RoomJournal(RoomJournal source)
        {
            clock=source.clock;world=source.world.Copy();viewpoint=source.viewpoint.Copy();
            foreach(var pair in source.items) items.Add(pair.Key,pair.Value.Copy());
            foreach(var pair in source.revisions) revisions.Add(pair.Key,pair.Value);
            foreach(var pair in source.structures)structures.Add(pair.Key,pair.Value.Copy());
            foreach(var pair in source.structureRevisions)structureRevisions.Add(pair.Key,pair.Value);
            foreach(var pair in source.audioSources)audioSources.Add(pair.Key,pair.Value.Copy());
            foreach(var pair in source.audioRevisions)audioRevisions.Add(pair.Key,pair.Value);
            foreach(var pair in source.environments)environments.Add(pair.Key,pair.Value.Copy());
            foreach(var pair in source.environmentRevisions)environmentRevisions.Add(pair.Key,pair.Value);
            foreach(var pair in source.appearances)appearances.Add(pair.Key,pair.Value.Copy());
            foreach(var pair in source.appearanceRevisions)appearanceRevisions.Add(pair.Key,pair.Value);
        }
        // A temporary room has its own local Undo history; the saved history
        // remains untouched until a successfully written snapshot is accepted.
        public RoomJournal Fork() => new(this);
        public bool ApplySnapshot(RoomDocument document,out string error)
        {
            if(document==null) {error="Room snapshot is missing";return false;}
            if(!document.Validate(out error))return false;
            if(!world.Same(document.world)){error="This snapshot belongs to a different world or region; use workspace activation";return false;}
            var ids=document.objects.Select(x=>x.id).ToHashSet();
            var replacements=document.objects.Where(x=>!items.TryGetValue(x.id,out var before)||!Equivalent(new[]{before},new[]{x})).ToArray();
            var groupIds=(document.structures??Array.Empty<RoomStructure>()).Select(x=>x.id).ToHashSet();
            var edits=new StructureEdits {Replacements=(document.structures??Array.Empty<RoomStructure>()).Where(x=>!structures.TryGetValue(x.id,out var before)||!EquivalentStructures(new[]{before},new[]{x})).ToArray(),Removals=structures.Keys.Where(id=>!groupIds.Contains(id)).ToArray()};
            var soundIds=(document.audioSources??Array.Empty<RoomAudioDefinition>()).Select(x=>x.id).ToHashSet();
            var sounds=new AudioDefinitionEdits {Replacements=(document.audioSources??Array.Empty<RoomAudioDefinition>()).Where(x=>!audioSources.TryGetValue(x.id,out var before)||!EquivalentAudio(new[]{before},new[]{x})).ToArray(),Removals=audioSources.Keys.Where(id=>!soundIds.Contains(id)).ToArray()};
            var profileIds=(document.environmentProfiles??Array.Empty<RoomEnvironmentProfile>()).Select(x=>x.id).ToHashSet();
            var profiles=new EnvironmentProfileEdits{Replacements=(document.environmentProfiles??Array.Empty<RoomEnvironmentProfile>()).Where(x=>!environments.TryGetValue(x.id,out var before)||!EquivalentEnvironments(new[]{before},new[]{x})).ToArray(),Removals=environments.Keys.Where(id=>!profileIds.Contains(id)).ToArray()};
            var appearanceIds=(document.appearances??Array.Empty<RoomAppearance>()).Select(x=>x.id).ToHashSet();
            var styles=new AppearanceEdits{Replacements=(document.appearances??Array.Empty<RoomAppearance>()).Where(x=>!appearances.TryGetValue(x.id,out var before)||!EquivalentAppearances(new[]{before},new[]{x})).ToArray(),Removals=appearances.Keys.Where(id=>!appearanceIds.Contains(id)).ToArray()};
            if(!Apply(replacements,items.Keys.Where(id=>!ids.Contains(id)).ToArray(),out error,structureEdits:edits,audioEdits:sounds,environmentEdits:profiles,appearanceEdits:styles))return false;
            if(document.version>=22)viewpoint=document.viewpoint.Copy();return true;
        }
        public void InvalidateChangedObservations(RoomJournal other)
        {
            foreach(var id in items.Keys)
                if(ObjectRevision(id)!=other.ObjectRevision(id))revisions[id]=clock.Next++;
            foreach(var id in structures.Keys)if(StructureRevision(id)!=other.StructureRevision(id))structureRevisions[id]=clock.Next++;
            foreach(var id in audioSources.Keys)if(AudioRevision(id)!=other.AudioRevision(id))audioRevisions[id]=clock.Next++;
            foreach(var id in environments.Keys)if(EnvironmentRevision(id)!=other.EnvironmentRevision(id))environmentRevisions[id]=clock.Next++;
            foreach(var id in appearances.Keys)if(AppearanceRevision(id)!=other.AppearanceRevision(id))appearanceRevisions[id]=clock.Next++;
        }
        public RoomObjectData Read(string id) => id != null && items.TryGetValue(id, out var value) ? value.Copy() : null;
        // Physics updates persisted placement without filling Undo with every simulation step.
        public bool UpdatePlacement(string id, Vector3 position, Quaternion rotation)
        {
            if (!items.TryGetValue(id,out var data) || !float.IsFinite(position.sqrMagnitude) || position.sqrMagnitude > 625 || !MotionFrame.ValidRotation(rotation)) return false;
            if ((data.position-position).sqrMagnitude < .000001f && Quaternion.Angle(data.rotation,rotation) < .1f) return false;
            data.position = position; data.rotation = rotation; revisions[id]=clock.Next++; return true;
        }
        public RoomDocument Snapshot() => new() { version = RoomDocument.CurrentVersion, world=world.Copy(), viewpoint=viewpoint.Copy(), objects = items.Values.Select(item => item.Copy()).OrderBy(item => item.id, StringComparer.Ordinal).ToArray(), structures = StructureSnapshot(), audioSources=AudioSnapshot(), environmentProfiles=EnvironmentSnapshot(), appearances=AppearanceSnapshot() };

        internal bool PlacementBaseline(RoomLayout layout,out RoomObjectData[] baseline,out string error)
        {
            baseline=null;if(!layout.Validate(out error))return false;
            var snapshot=Snapshot();var values=snapshot.objects.ToDictionary(x=>x.id);
            foreach(var p in layout.placements) {if(!values.TryGetValue(p.target,out var data)){error="A layout member was removed";return false;}p.Apply(data);}
            if(!snapshot.Validate(out error))return false;
            baseline=layout.placements.Select(p=>values[p.target]).ToArray();return true;
        }
        internal bool EditBaseline(RoomObjectData[] replacements,string[] removals,RoomLayout observedBefore,out RoomObjectData[] baseline,out string error)
        {
            baseline=null;if(!observedBefore.Validate(out error))return false;
            // A mixed edit/create transaction has no prior pose for new members.
            // Require every existing edited member exactly once, and never invent
            // a baseline for an addition. Share this preflight with persistence.
            var existing=replacements.Select(item=>item.id).Where(items.ContainsKey).ToHashSet();
            if(removals.Length!=0||!existing.SetEquals(observedBefore.placements.Select(p=>p.target))){error="Layout baseline must match the existing edited members";return false;}
            return PlacementBaseline(observedBefore,out baseline,out error);
        }
        public bool Apply(RoomObjectData[] replacements, string[] removals, out string error, RoomLayout observedBefore=null, StructureEdits structureEdits=null,AudioDefinitionEdits audioEdits=null,EnvironmentProfileEdits environmentEdits=null,AppearanceEdits appearanceEdits=null)
        {
            if(structureEdits!=null&&!structureEdits.Validate(out error))return false;
            if(audioEdits!=null&&!audioEdits.Validate(out error))return false;
            if(environmentEdits!=null&&!environmentEdits.Validate(out error))return false;
            if(appearanceEdits!=null&&!appearanceEdits.Validate(out error))return false;
            var changedIds = replacements.Select(item => item.id).Concat(removals).ToHashSet();
            var candidate = new Dictionary<string, RoomObjectData>(items);
            foreach (var id in removals) candidate.Remove(id);
            foreach (var item in replacements) candidate[item.id] = item.Copy();
            var groups=structureEdits?.Apply(StructureSnapshot())??StructureSnapshot();
            var sounds=audioEdits?.Apply(AudioSnapshot())??AudioSnapshot();
            var profiles=environmentEdits?.Apply(EnvironmentSnapshot())??EnvironmentSnapshot();
            var styles=appearanceEdits?.Apply(AppearanceSnapshot())??AppearanceSnapshot();
            var appearanceIds=(appearanceEdits?.Replacements.Select(x=>x.id)??Array.Empty<string>()).Concat(appearanceEdits?.Removals??Array.Empty<string>()).ToHashSet();
            if (!(new RoomDocument { version = RoomDocument.CurrentVersion, world=world.Copy(), viewpoint=viewpoint.Copy(), objects = candidate.Values.ToArray(), structures=groups,audioSources=sounds,environmentProfiles=profiles,appearances=styles }).Validate(out error)) return false;
            var profileIds=(environmentEdits?.Replacements.Select(x=>x.id)??Array.Empty<string>()).Concat(environmentEdits?.Removals??Array.Empty<string>()).ToHashSet();
            var soundIds=(audioEdits?.Replacements.Select(x=>x.id)??Array.Empty<string>()).Concat(audioEdits?.Removals??Array.Empty<string>()).ToHashSet();
            var groupIds=(structureEdits?.Replacements.Select(x=>x.id)??Array.Empty<string>()).Concat(structureEdits?.Removals??Array.Empty<string>()).ToHashSet();
            var before=changedIds.Where(items.ContainsKey).Select(id=>items[id].Copy()).ToArray();
            if(observedBefore!=null&&!EditBaseline(replacements,removals,observedBefore,out before,out error))return false;
            var change = new Change {
                Before = before,
                After = changedIds.Where(candidate.ContainsKey).Select(id => candidate[id].Copy()).ToArray(),
                BeforeStructures=groupIds.Where(structures.ContainsKey).Select(id=>structures[id].Copy()).ToArray(),
                AfterStructures=groups.Where(x=>groupIds.Contains(x.id)).Select(x=>x.Copy()).ToArray(),
                BeforeAudio=soundIds.Where(audioSources.ContainsKey).Select(id=>audioSources[id].Copy()).ToArray(),
                AfterAudio=sounds.Where(x=>soundIds.Contains(x.id)).Select(x=>x.Copy()).ToArray(),
                BeforeEnvironments=profileIds.Where(environments.ContainsKey).Select(id=>environments[id].Copy()).ToArray(),
                AfterEnvironments=profiles.Where(x=>profileIds.Contains(x.id)).Select(x=>x.Copy()).ToArray(),
                BeforeAppearances=appearanceIds.Where(appearances.ContainsKey).Select(id=>appearances[id].Copy()).ToArray(),
                AfterAppearances=styles.Where(x=>appearanceIds.Contains(x.id)).Select(x=>x.Copy()).ToArray()
            };
            if (Equivalent(change.Before, change.After)&&EquivalentStructures(change.BeforeStructures,change.AfterStructures)&&EquivalentAudio(change.BeforeAudio,change.AfterAudio)&&EquivalentEnvironments(change.BeforeEnvironments,change.AfterEnvironments)&&EquivalentAppearances(change.BeforeAppearances,change.AfterAppearances)) {
                // A live layout may already match while its periodic saved pose lags.
                // Accept that snapshot without an empty Undo entry or stale journal.
                if(observedBefore!=null&&!Equivalent(changedIds.Where(items.ContainsKey).Select(id=>items[id]).ToArray(),change.After))Set(change.Before,change.After);
                return true;
            }
            Set(change.Before, change.After);SetStructures(change.BeforeStructures,change.AfterStructures);SetAudio(change.BeforeAudio,change.AfterAudio);SetEnvironments(change.BeforeEnvironments,change.AfterEnvironments);SetAppearances(change.BeforeAppearances,change.AfterAppearances);
            undo.Add(change); if (undo.Count > 32) undo.RemoveAt(0); redo.Clear(); return true;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var change = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            Set(change.After, change.Before);SetStructures(change.AfterStructures,change.BeforeStructures);SetAudio(change.AfterAudio,change.BeforeAudio);SetEnvironments(change.AfterEnvironments,change.BeforeEnvironments);SetAppearances(change.AfterAppearances,change.BeforeAppearances); redo.Add(change); return true;
        }
        public bool Redo()
        {
            if (!CanRedo) return false;
            var change = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            Set(change.Before, change.After);SetStructures(change.BeforeStructures,change.AfterStructures);SetAudio(change.BeforeAudio,change.AfterAudio);SetEnvironments(change.BeforeEnvironments,change.AfterEnvironments);SetAppearances(change.BeforeAppearances,change.AfterAppearances); undo.Add(change); return true;
        }
        void Set(RoomObjectData[] before, RoomObjectData[] after)
        {
            foreach (var item in before) { items.Remove(item.id); revisions.Remove(item.id); }
            foreach (var item in after) { items[item.id] = item.Copy(); revisions[item.id]=clock.Next++; }
        }
        [Serializable] sealed class ObjectDelta { public RoomObjectData[] objects; }
        static bool Equivalent(RoomObjectData[] a, RoomObjectData[] b) => JsonUtility.ToJson(new ObjectDelta { objects = a.OrderBy(x => x.id).ToArray() }) == JsonUtility.ToJson(new ObjectDelta { objects = b.OrderBy(x => x.id).ToArray() });
    }
}
