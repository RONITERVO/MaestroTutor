// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    public sealed class ImportWorkshop : MonoBehaviour
    {
        [Serializable] sealed class Selection { public string path, name, error; }
        RoomEditor editor;
        ModelAsset pending;
        ImportedModel preview;
        bool busy, picking, disposed, loop;
        int clip, page;
        string selected;
        public string Status { get; private set; } = "Import your GLB or VRM model";
        public string Details { get; private set; } = "Models stay on this headset.\nChoose Import to select a local file.";
        public event Action Changed;
        public bool HasPreview => pending != null && preview && preview.Ready;
        public bool Busy => busy || picking;
        public void Initialize(RoomEditor source, AnimationWorkshop animations = null)
        {
            editor = source; editor.Changed += SelectionChanged; editor.Editing += Stop;
            editor.ItemGrabbed += Grabbed; if (animations) animations.Starting += StopTarget;
            animationWorkshop = animations;
        }
        AnimationWorkshop animationWorkshop;
        public void Pick()
        {
            if (Busy) return;
            Cancel(); Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"); using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var picker = new AndroidJavaClass("com.maestro.quest.browser.ModelPicker"); picker.CallStatic("Start", activity);
                picking = true; Say("Choose one .glb or .vrm file in the document picker");
            }
            catch (Exception) { Say("The document picker could not open. Resume Maestro and try again."); }
#elif UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Import a model you may use", "", new[] { "GLB and VRM models", "glb,vrm" });
            if (!string.IsNullOrEmpty(path)) _ = PreparePathAsync(path, Path.GetFileName(path));
#else
            Say("File selection is currently available on Quest and in the Unity Editor.");
#endif
        }
        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!picking) return;
            try
            {
                using var picker = new AndroidJavaClass("com.maestro.quest.browser.ModelPicker"); var json = picker.CallStatic<string>("ReadResult");
                if (string.IsNullOrEmpty(json)) return;
                picking = false; var result = JsonUtility.FromJson<Selection>(json);
                if (!string.IsNullOrEmpty(result.error)) { Say(result.error); ReleasePicker(); }
                else _ = PreparePathAsync(result.path, result.name, true);
            }
            catch (Exception) { picking = false; ReleasePicker(); Say("The selected model could not be read."); }
#endif
        }
        async Task PreparePathAsync(string path, string name, bool releasePicker = false)
        {
            busy = true; Say("Checking model…");
            try { var asset = await Task.Run(() => ModelLibrary.Inspect(name, ModelLibrary.ReadBounded(path))); await PreviewAsync(asset); }
            catch (Exception error) { Report(error); }
            finally { busy = false; if (releasePicker) ReleasePicker(); }
        }
        public async Task PrepareAsync(string name, byte[] bytes)
        {
            if (Busy) return; Cancel(); busy = true; Say("Checking model…");
            try { var asset = await Task.Run(() => ModelLibrary.Inspect(name, bytes)); await PreviewAsync(asset); }
            catch (Exception error) { Report(error); }
            finally { busy = false; }
        }
        async Task PreviewAsync(ModelAsset asset)
        {
            if (!this || disposed) return;
            var root = new GameObject("Model import preview"); root.transform.SetParent(transform, false); root.transform.localPosition = new Vector3(.55f, 1.35f, .65f);
            preview = root.AddComponent<ImportedModel>();
            try { await preview.LoadAsync(asset); }
            catch { if (preview) Destroy(preview.gameObject); preview = null; throw; }
            if (!this || disposed) return;
            pending = asset; clip = 0; page = 0; ShowDetails(); Say("Preview only — Add confirms you may use this model");
        }
        public async void Accept() => await AcceptAsync();
        public async Task<bool> AcceptAsync()
        {
            if (Busy || !HasPreview) return false;
            if (editor.AnyHeld) { Say("Release the object before adding the model"); return false; }
            busy = true;
            try
            {
                await editor.Models.SaveAsync(pending); if (!this || disposed) return false;
                if (!editor.AddModel(pending.Hash)) { Say(editor.Status); return false; }
                ClearPreview(); Say("Model added — pick it up to move, paint or animate it"); return true;
            }
            catch (Exception error) { Report(error); return false; }
            finally { busy = false; }
        }
        public void Cancel() { if (Busy) { Say("Please wait for the model check to finish"); return; } ClearPreview(); Say("Import cancelled"); }
        void ClearPreview() { if (preview) { preview.gameObject.SetActive(false); Destroy(preview.gameObject); } preview = null; pending = null; Details = "Select an imported object to play its clips.\nImported VRM objects do not yet replace Maestro."; Changed?.Invoke(); }
        ImportedModel Target => HasPreview ? preview : editor.Find(editor.SelectedId)?.GetComponent<CreatedRoomObject>()?.Model;
        public void NextClip() { var target = Target; if (!target || target.ClipCount == 0) { Say("This model has no embedded animation clips"); return; } target.Stop(); clip = (clip + 1) % target.ClipCount; Say("Clip " + (clip + 1) + ": " + target.ClipName(clip)); }
        public void Play() { var target = Target; if (!target || !target.Ready || target.ClipCount == 0) { Say("Choose an imported model with animation clips"); return; } if (editor.AnyHeld) { Say("Release the object before previewing its clip"); return; } target.Play(clip % target.ClipCount, loop); Say("Playing " + target.ClipName(clip % target.ClipCount)); }
        public void ToggleLoop() { loop = !loop; Stop(); Say(loop ? "Clip loop enabled — press Play" : "Clip plays once — press Play"); }
        public void Stop() { if (preview) preview.Stop(); if (editor) foreach (var model in editor.GetComponentsInChildren<ImportedModel>()) model.Stop(); }
        void Grabbed(RoomItem item) => item.GetComponent<CreatedRoomObject>()?.Model?.Stop();
        void StopTarget(string id) => editor.Find(id)?.GetComponent<CreatedRoomObject>()?.Model?.Stop();
        void SelectionChanged()
        {
            if (selected == editor.SelectedId) return; selected = editor.SelectedId; clip = 0;
            if (!HasPreview) { var created = editor.Find(selected)?.GetComponent<CreatedRoomObject>(); Details = created && created.Model ? created.ModelStatus : "Select an imported object to play its clips."; Changed?.Invoke(); }
        }
        public void NextDetails() { page++; ShowDetails(); }
        void ShowDetails()
        {
            if (pending == null) { var created = editor.Find(editor.SelectedId)?.GetComponent<CreatedRoomObject>(); Details = created?.ModelStatus ?? "Import a model to view its information."; Changed?.Invoke(); return; }
            var info = pending.Inspection;
            string text = pending.Name + "\n" + info.Vertices + " vertices / " + info.Triangles + " triangles\n" + info.Clips + " embedded clips\n" + (info.IsAvatar ? "VRM room object; Maestro replacement is pending.\n" : "") + "Author and use terms:\n" + info.Attribution;
            var lines = ModelText.Wrap(text, 64); int pages = Math.Max(1, (lines.Length + 7) / 8); page %= pages;
            Details = "Model information " + (page + 1) + "/" + pages + "\n" + string.Join("\n", lines, page * 8, Math.Min(8, lines.Length - page * 8)); Changed?.Invoke();
        }
        void Say(string value) { if (disposed) return; Status = value; Changed?.Invoke(); }
        void Report(Exception error) { if (!this || disposed) return; Say(error is ModelImportException ? error.Message : "The model could not be imported. Try exporting a self-contained GLB or VRM."); }
        static void ReleasePicker()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var picker = new AndroidJavaClass("com.maestro.quest.browser.ModelPicker"); picker.CallStatic("Release");
#endif
        }
        void OnApplicationPause(bool value) { if (value) Stop(); }
        void OnDestroy()
        {
            disposed = true; ClearPreview(); if (picking) ReleasePicker();
            if (editor) { editor.Changed -= SelectionChanged; editor.Editing -= Stop; editor.ItemGrabbed -= Grabbed; }
            if (animationWorkshop) animationWorkshop.Starting -= StopTarget;
        }
    }

    public static class ModelText
    {
        public static string[] Wrap(string text, int columns)
        {
            var lines = new System.Collections.Generic.List<string>();
            foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string remaining = line; while (remaining.Length > columns) { int space = remaining.LastIndexOf(' ', columns - 1, columns); int take = space > 0 ? space : columns; lines.Add(remaining.Substring(0, take)); remaining = remaining.Substring(take).TrimStart(); } lines.Add(remaining);
            }
            return lines.ToArray();
        }
    }
}
