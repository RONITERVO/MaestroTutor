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
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public sealed class RoomEditor : MonoBehaviour
    {
        readonly Dictionary<string, RoomItem> objects = new();
        readonly Dictionary<RoomItem, string> identities = new();
        RoomInteraction room;
        RoomJournal journal;
        RoomStorage storage;
        string selected;
        bool applying, dirty;
        float saveAt;
        Task<string> saveTask;
        public string Status { get; private set; } = "Choose a shape or pick up an object";
        public bool DrawingMode { get; private set; }
        public Color Paint { get; private set; } = IllustratedMaterials.Hex("2B8D88");
        public bool CanUndo => journal != null && journal.CanUndo;
        public bool CanRedo => journal != null && journal.CanRedo;
        public event Action Changed;
        public event Action Editing;
        public event Action<RoomItem> ItemGrabbed;
        public string SelectedId => selected;
        public RoomItem Find(string id) => id != null && objects.TryGetValue(id,out var value) ? value : null;
        public RoomObjectData Read(string id) => journal.Read(id);
        public bool AnyHeld => objects.Values.Any(item => item && item.Grab && item.Grab.isSelected);
        public RoomDocument Snapshot() => journal.Snapshot();

        public void Initialize(RoomInteraction interaction, RoomItem book, RoomItem maestro, string saveDirectory = null)
        {
            room = interaction;
            AddIdentity("book", book); AddIdentity("maestro", maestro);
            storage = new RoomStorage(saveDirectory ?? Path.Combine(Application.persistentDataPath, "room"));
            var loaded = storage.Load(out var message);
            journal = new RoomJournal(loaded ?? StarterDocument(book, maestro));
            Reconcile();
            room.Restoring += BeforeRestore; room.Restored += AfterRestore;
            if (message != null) SetStatus(message);
        }

        static RoomDocument StarterDocument(RoomItem book, RoomItem maestro)
        {
            var items = new List<RoomObjectData> { Pose(new RoomObjectData { id = "book", kind = RoomObjectKind.Book }, book.transform), Pose(new RoomObjectData { id = "maestro", kind = RoomObjectKind.Maestro }, maestro.transform) };
            var kinds = new[] { RoomObjectKind.Block, RoomObjectKind.Ball, RoomObjectKind.Cylinder };
            var colors = new[] { IllustratedMaterials.Cover, IllustratedMaterials.Ribbon, IllustratedMaterials.Hex("2B8D88") };
            for (int i = 0; i < kinds.Length; i++) items.Add(new RoomObjectData { id = Guid.NewGuid().ToString("N"), kind = kinds[i], color = colors[i], position = new Vector3(.52f + i * .17f,1.45f,.8f) });
            return new RoomDocument { version = 1, objects = items.ToArray() };
        }

        void AddIdentity(string id, RoomItem item)
        {
            objects.Add(id,item); identities.Add(item,id);
            item.GrabStarted += GrabStarted; item.GrabFinished += GrabFinished;
        }
        void GrabStarted(RoomItem item) { if (!applying) { ItemGrabbed?.Invoke(item); Select(item); } }
        void GrabFinished(RoomItem item)
        {
            if (applying || !identities.TryGetValue(item,out var id)) return;
            var before = journal.Read(id); if (before == null) return;
            var after = Pose(before, item.transform);
            if (!Commit(new[] { after }, Array.Empty<string>(), "Placed — saving", true)) ApplyPose(item, journal.Read(id));
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
            var item = new RoomObjectData { id = Guid.NewGuid().ToString("N"), kind = kind, color = Paint, position = SpawnPosition() };
            if (Commit(new[] { item }, Array.Empty<string>(), kind + " added", true)) { selected = item.id; UpdateSelection(); }
        }

        Vector3 SpawnPosition()
        {
            if (!room.Viewer) return new Vector3(.3f,1.3f,.65f);
            var forward = Vector3.ProjectOnPlane(room.Viewer.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            return transform.InverseTransformPoint(room.Viewer.position + forward * .7f + room.Viewer.right * .3f - Vector3.up * .2f);
        }

        public void ChoosePaint(Color color)
        {
            Paint = color; Paint = new Color(Paint.r,Paint.g,Paint.b,1);
            var item = journal.Read(selected);
            if (item != null && !item.IsBuiltIn) { item.color = Paint; Commit(new[] { item }, Array.Empty<string>(), "Paint changed"); }
            else SetStatus("Paint chosen for your next creation");
        }

        public void Duplicate()
        {
            var item = journal.Read(selected);
            if (item == null || item.IsBuiltIn) { SetStatus("Select one of your creations to duplicate"); return; }
            item.id = Guid.NewGuid().ToString("N"); item.position += Vector3.right * .18f;
            if (Commit(new[] { item }, Array.Empty<string>(), "Copy added")) { selected = item.id; UpdateSelection(); }
        }

        public void Erase()
        {
            var item = journal.Read(selected);
            if (item == null || item.IsBuiltIn) { SetStatus("Select one of your creations to erase"); return; }
            if (Commit(Array.Empty<RoomObjectData>(), new[] { selected }, "Erased — Undo brings it back")) { selected = null; UpdateSelection(); }
        }

        public void Undo() { Editing?.Invoke(); if (Busy()) return; if (journal.Undo()) { Reconcile(); MarkDirty(); SetStatus("Undone"); } else SetStatus("Nothing to undo"); }
        public void Redo() { Editing?.Invoke(); if (Busy()) return; if (journal.Redo()) { Reconcile(); MarkDirty(); SetStatus("Redone"); } else SetStatus("Nothing to redo"); }
        public void ToggleDrawing() { Editing?.Invoke(); DrawingMode = !DrawingMode; SetStatus(DrawingMode ? "Pencil: hold trigger or pinch to draw" : "Pencil put away"); }

        public bool AddDrawing(IReadOnlyList<Vector3> worldPoints, Color color)
        {
            if (worldPoints.Count < 2) return false;
            var origin = transform.InverseTransformPoint(worldPoints[0]);
            var points = worldPoints.Select(point => transform.InverseTransformPoint(point) - origin).ToArray();
            var data = new RoomObjectData { id = Guid.NewGuid().ToString("N"), kind = RoomObjectKind.Drawing, position = origin, color = color, points = points };
            if (!Commit(new[] { data }, Array.Empty<string>(), "Drawing added", true)) return false;
            selected = data.id; UpdateSelection(); return true;
        }

        bool Commit(RoomObjectData[] replacements, string[] removals, string success, bool placement = false)
        {
            if (!placement) Editing?.Invoke();
            if (journal == null || (!placement && Busy())) return false;
            if (!journal.Apply(replacements,removals,out var error)) { SetStatus(error); return false; }
            Reconcile(); MarkDirty(); SetStatus(success); return true;
        }
        public bool SaveAnimation(string id, RoomMotion motion, JointPose[] joints, bool savePose)
        {
            var data = journal.Read(id); if (data == null) return false;
            data.motion = motion?.Copy();
            if (savePose) data.joints = MotionFrame.CopyJoints(joints);
            return Commit(new[] { data },Array.Empty<string>(),"Animation saved",true);
        }
        public void RestorePose(string id)
        {
            var item = Find(id); var data = journal.Read(id);
            if (!item || data == null) return;
            ApplyPose(item,data); item.GetComponent<MaestroAvatar>()?.SetSavedPose(data.joints);
        }
        bool Busy()
        {
            if (!objects.Values.Any(item => item && item.Grab && item.Grab.isSelected)) return false;
            SetStatus("Release the object before editing"); return true;
        }

        void Reconcile()
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
                if (!objects.TryGetValue(data.id,out var item))
                {
                    var root = new GameObject(data.kind.ToString()); root.transform.SetParent(transform,false);
                    // Canonical scale is linked to XRI before restoring saved pose/scale.
                    item = root.AddComponent<CreatedRoomObject>().Build(data);
                    AddIdentity(data.id,item); room.Register(item);
                }
                if (!item.Grab.isSelected) ApplyPose(item,data);
                item.GetComponent<MaestroAvatar>()?.SetSavedPose(data.joints);
                if (!data.IsBuiltIn)
                {
                    item.SetHome(new Vector3(-.63f + (slot % 8) * .18f,.7f + ((slot / 8) % 4) * .18f,1.15f + (slot / 32) * .25f),Quaternion.identity,Vector3.one);
                    item.GetComponent<CreatedRoomObject>().ApplyColor(data.color); slot++;
                }
            }
            applying = false; UpdateSelection();
        }

        static void ApplyPose(RoomItem item, RoomObjectData data)
        {
            item.transform.SetLocalPositionAndRotation(data.position,data.rotation); item.transform.localScale = Vector3.one * data.scale;
        }
        static RoomObjectData Pose(RoomObjectData data, Transform pose)
        { data.position = pose.localPosition; data.rotation = pose.localRotation.normalized; data.scale = pose.localScale.x; return data; }

        void UpdateSelection()
        {
            foreach (var pair in objects) { var created = pair.Value.GetComponent<CreatedRoomObject>(); if (created) created.SetSelected(pair.Key == selected); }
            Changed?.Invoke();
        }
        void BeforeRestore() { Editing?.Invoke(); applying = true; }
        void AfterRestore()
        {
            applying = false;
            var placements = identities.Select(pair => Pose(journal.Read(pair.Value),pair.Key.transform)).ToArray();
            Commit(placements,Array.Empty<string>(),"Room brought back within reach");
        }

        void MarkDirty() { dirty = true; saveAt = Time.unscaledTime + .5f; }
        public void SaveNow() { MarkDirty(); saveAt = 0; SetStatus("Saving room"); }
        void Update()
        {
            if (saveTask != null && saveTask.IsCompleted)
            {
                var error = saveTask.GetAwaiter().GetResult(); saveTask = null;
                if (error != null) SetStatus(error);
                else if (!dirty) SetStatus("Room saved");
            }
            if (journal == null || !dirty || saveTask != null || Time.unscaledTime < saveAt) return;
            var snapshot = journal.Snapshot(); dirty = false;
            saveTask = Task.Run(() => { storage.Save(snapshot,out var error); return error; });
        }
        void Flush()
        {
            if (journal == null) return;
            var pendingError = saveTask?.GetAwaiter().GetResult(); saveTask = null;
            if (pendingError != null) SetStatus(pendingError);
            if (!dirty) return;
            dirty = false; if (!storage.Save(journal.Snapshot(),out var error)) SetStatus(error);
        }
        void SetStatus(string value) { Status = value; Changed?.Invoke(); }
        void OnApplicationPause(bool paused) { if (paused) Flush(); }
        void OnApplicationFocus(bool focused) { if (!focused) Flush(); }
        void OnApplicationQuit() => Flush();
        void OnDestroy()
        {
            Flush();
            if (room) { room.Restoring -= BeforeRestore; room.Restored -= AfterRestore; }
            foreach (var item in objects.Values) if (item) { item.GrabStarted -= GrabStarted; item.GrabFinished -= GrabFinished; }
        }
    }
}
