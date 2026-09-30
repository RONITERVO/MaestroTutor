// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Avatar;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    public sealed class ImportWorkshop : MonoBehaviour
    {
        [Serializable] sealed class Selection { public string path, name, error; }
        RoomEditor editor;
        RoomRuntimeGate runtimeGate;
        ModelAsset pending;
        ImportedModel preview;
        bool busy, picking, disposed, loop;
        int clip, page;
        bool libraryMode;
        string libraryMotionId;
        int motionRequest;
        public bool LibraryMode => libraryMode;
        public string SelectedLibraryMotionId => libraryMotionId;
        string selected;
        MaestroAvatar maestro;
        public string Status { get; private set; } = "Import your GLB or VRM model";
        public string Details { get; private set; } = "Models stay on this headset.\nChoose Import to select a local file.";
        public event Action Changed, BrowseRequested;
        public void BrowseLibrary() { if (BrowseRequested != null) BrowseRequested(); else ToggleLibrary(); }
        public bool HasPreview => pending != null && preview && preview.Ready;
        public ImportBatchWorkshop Batches { get; private set; }
        public bool Busy => busy || picking || (Batches && Batches.Busy);
        public void Initialize(RoomEditor source, AnimationWorkshop animations = null)
        {
            Batches=gameObject.AddComponent<ImportBatchWorkshop>(); Batches.Initialize(source,this);
            editor = source; editor.Changed += SelectionChanged; editor.Editing += Stop;
            runtimeGate=editor.RuntimeGate;runtimeGate.Changed+=RuntimeChanged;RuntimeChanged();
            editor.ItemGrabbed += Grabbed; if (animations) animations.Starting += StopTarget;
            animationWorkshop = animations;
            maestro = editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            if (maestro) maestro.ModelChanged += MaestroChanged;
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
            pending = asset; libraryMode = false; clip = 0; page = 0; ShowDetails(); Say("Preview ready — choose Add model" + (preview.IsHumanoid ? " or Use Maestro" : ""));
        }
        public async void Accept() => await AcceptAsync();
        public async Task<bool> AcceptAsync()
        {
            using var write=editor.WriteGate.TryWrite(out var blocked);if(write==null){Say(blocked);return false;}
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
        void ClearPreview() { if (preview) { preview.gameObject.SetActive(false); Destroy(preview.gameObject); } preview = null; pending = null; Details = "Select Maestro or an imported object to play its clips.\nUse Maestro selects a compatible GLB or VRM humanoid."; Changed?.Invoke(); }
        public async void UseMaestro() => await UseMaestroAsync();
        public async Task<bool> UseMaestroAsync()
        {
            using var write=editor.WriteGate.TryWrite(out var blocked);if(write==null){Say(blocked);return false;}
            if (Busy || !maestro || maestro.ModelBusy) return false;
            if (editor.AnyHeld) { Say("Release the object before changing Maestro"); return false; }
            var target = Target;
            if (!target || !target.Ready || !target.IsHumanoid) { Say(target?.HumanoidIssue ?? "Preview or select a GLB or VRM humanoid, then choose Use Maestro"); return false; }
            string hash = HasPreview ? pending.Hash : editor.Read(editor.SelectedId)?.modelHash;
            busy = true;
            try
            {
                if (HasPreview) { await editor.Models.SaveAsync(pending); if (!this || disposed) return false; ClearPreview(); await Task.Yield(); }
                if (!this || disposed || !editor.SetMaestroModel(hash)) return false;
                bool ready = await maestro.ModelLoad;
                if (ready && this && !disposed) editor.Select(editor.Find("maestro"));
                if (this && !disposed) Say(maestro.ModelStatus);
                return ready;
            }
            catch (Exception error) { Report(error); return false; }
            finally { busy = false; }
        }
        public void DefaultMaestro() { if (Busy) return; if (editor.SetMaestroModel(null)) Say("Included Maestro restored — Undo brings back your custom avatar"); }
        void MaestroChanged() { motionRequest++; if (libraryMode) ShowLibraryDetails(); if (maestro) Say(maestro.ModelStatus); }
        ImportedModel Target => HasPreview ? preview : editor.SelectedId == "maestro" ? maestro?.CustomModel : editor.Find(editor.SelectedId)?.GetComponent<CreatedRoomObject>()?.Model;
        public void NextClip() { if (libraryMode) { NextLibraryMotion(); return; } var target = Target; if (!target || target.ClipCount == 0) { Say("This model has no embedded animation clips"); return; } Stop(); clip = (clip + 1) % target.ClipCount; Say("Clip " + (clip + 1) + ": " + target.ClipName(clip)); }
        void RuntimeChanged(){if(runtimeGate.Held)Stop();}
        public void Play()
        {
            if(runtimeGate?.Held==true){Say(runtimeGate.Reason);return;}
            if (libraryMode) { _ = PlayLibraryAsync(); return; }
            var target = Target; if (!target || !target.Ready || target.ClipCount == 0) { Say("Choose an imported model or Maestro with animation clips"); return; }
            if (editor.AnyHeld) { Say("Release the object before previewing its clip"); return; }
            if (!HasPreview && editor.SelectedId == "maestro")
            {
                if (!animationWorkshop || !animationWorkshop.PreviewImportedClip(clip % target.ClipCount,loop)) { Say(animationWorkshop ? animationWorkshop.Status : "Animation controls are unavailable"); return; }
            }
            else target.Play(clip % target.ClipCount, loop);
            Say("Playing " + target.ClipName(clip % target.ClipCount));
        }
        public void ToggleLoop() { loop = !loop; Stop(); Say(loop ? "Clip loop enabled — press Play" : "Clip plays once — press Play"); }
        public void StopPreview() { Stop(); Say(libraryMode ? "Motion stopped" : "Clip stopped"); }
        public void Stop() { motionRequest++; if (editor) editor.GetComponent<RoomRules>()?.StopAll(); if (maestro) { maestro.GetComponent<AvatarSpatialMotion>()?.Stop(); maestro.StopImportedClip(); } if (animationWorkshop && animationWorkshop.IsImportedPreview) animationWorkshop.Stop(); if (preview) preview.Stop(); if (editor) foreach (var model in editor.GetComponentsInChildren<ImportedModel>()) model.Stop(); }
        void Grabbed(RoomItem item) { motionRequest++; item.GetComponent<CreatedRoomObject>()?.Model?.Stop(); }
        void StopTarget(string id) { motionRequest++; editor.Find(id)?.GetComponent<CreatedRoomObject>()?.Model?.Stop(); }
        void SelectionChanged()
        {
            if (selected == editor.SelectedId) return; selected = editor.SelectedId; clip = 0;
            if (libraryMode) { ShowLibraryDetails(); return; }
            if (!HasPreview) { var created = editor.Find(selected)?.GetComponent<CreatedRoomObject>(); Details = selected == "maestro" && maestro ? maestro.ModelStatus : created && created.Model ? created.ModelStatus : "Select Maestro or an imported object to play its clips."; Changed?.Invoke(); }
        }
        public void NextDetails() { page++; ShowDetails(); }
        void ShowDetails()
        {
            if (libraryMode) { ShowLibraryDetails(); return; }
            if (pending == null) { var created = editor.Find(editor.SelectedId)?.GetComponent<CreatedRoomObject>(); Details = editor.SelectedId == "maestro" && maestro ? maestro.ModelStatus : created?.ModelStatus ?? "Import a model to view its information."; Changed?.Invoke(); return; }
            var info = pending.Inspection;
            string text = pending.Name + "\n" + info.Vertices + " vertices / " + info.Triangles + " triangles\n" + info.Clips + " embedded clips\n" + (preview && preview.IsHumanoid ? "Humanoid: Add as an object, or Use Maestro as your tutor.\n" : "") + "Author and use terms:\n" + info.Attribution;
            var lines = ModelText.Wrap(text, 64); int pages = Math.Max(1, (lines.Length + 7) / 8); page %= pages;
            Details = "Model information " + (page + 1) + "/" + pages + "\n" + string.Join("\n", lines, page * 8, Math.Min(8, lines.Length - page * 8)); Changed?.Invoke();
        }
        MotionEntry[] CompatibleMotions() => editor.Motions.List(rigHash:maestro && maestro.CustomModel ? maestro.CustomModel.MotionRigHash ?? "" : "");
        MotionEntry CurrentMotion()
        {
            var entry = editor.Motions.Inspect(libraryMotionId) ?? (libraryMotionId == null ? CompatibleMotions().FirstOrDefault() : null);
            libraryMotionId = entry?.id; return entry;
        }
        public bool SelectLibraryMotion(string id)
        {
            var entry = editor.Motions.Inspect(id); if (entry == null) return false;
            if (libraryMotionId != id) Stop();
            libraryMode = true; libraryMotionId = id; page = 0; ShowLibraryDetails(); Say("Selected "+entry.name); return true;
        }
        public void RefreshLibraryDetails() { if (libraryMode) ShowLibraryDetails(); }
        public void ToggleLibrary()
        {
            if (Busy) return; Stop(); libraryMode = !libraryMode;
            if (libraryMode) { ShowLibraryDetails(); Say("Saved motions — Next clip chooses one; Play previews on Maestro"); }
            else { ShowDetails(); Say("Embedded clips — select a model or Maestro"); }
        }
        void NextLibraryMotion()
        {
            Stop(); var entries = CompatibleMotions();
            if (entries.Length == 0) { ShowLibraryDetails(); Say("No saved motions match the loaded Maestro rig"); return; }
            int current = Array.FindIndex(entries,x => x.id == libraryMotionId); libraryMotionId = entries[(current+1)%entries.Length].id;
            ShowLibraryDetails(); Say("Selected " + CurrentMotion().name);
        }
        void ShowLibraryDetails()
        {
            var entry = CurrentMotion();
            if (entry == null)
            {
                string issue = maestro && maestro.CustomModel ? maestro.CustomModel.MotionRigIssue : null;
                Details = "Motion library\n"+(issue == null ? "Load a compatible custom Maestro, then save motions\nfrom its model or another export of the same rig." : string.Join("\n",ModelText.Wrap(issue,64)));
                Changed?.Invoke(); return;
            }
            var source = editor.Motions.Sources().FirstOrDefault(x => x.hash == entry.origins[0].sourceHash);
            bool compatible = maestro && !maestro.ModelBusy && maestro.CustomModel && maestro.CustomModel.MotionRigHash == entry.rigHash;
            string text = (entry.removed ? "Download removed. Import the original export, then Save motions to restore it.\n" : "")+(entry.archived ? "Archived: existing assignments are retained.\n" : "")+(compatible ? "" : "Load a compatible Maestro to preview this saved motion.\n")+entry.name+"\n"+entry.duration.ToString("0.00")+" seconds · "+CompatibleMotions().Length+" compatible motions\n"+string.Join(", ",entry.tags)+"\nSource terms:\n"+source?.attribution;
            var lines = ModelText.Wrap(text,64); int pages = Math.Max(1,(lines.Length+6)/7); page %= pages;
            Details = "Motion library " + (page+1) + "/" + pages + "\n" + string.Join("\n",lines,page*7,Math.Min(7,lines.Length-page*7)); Changed?.Invoke();
        }
        public async void SaveMotions() => await SaveMotionsAsync();
        public async Task<bool> SaveMotionsAsync()
        {
            using var write=editor.WriteGate.TryWrite(out var blocked);if(write==null){Say(blocked);return false;}
            if (Busy) return false; busy = true; Stop(); Say("Extracting motions without saving another model…");
            try
            {
                var asset = pending;
                if (asset == null)
                {
                    string hash = editor.Read(editor.SelectedId)?.modelHash;
                    if (!ModelLibrary.ValidHash(hash)) { Say("Import an animated GLB, or select your custom Maestro first"); return false; }
                    asset = await editor.Models.ReadAsync(hash);
                }
                if (!this || disposed) return false;
                var entries = await editor.Motions.ImportAsync(asset.Name,asset.Bytes);
                if (!this || disposed) return false;
                libraryMode = true; libraryMotionId = entries.FirstOrDefault(x => !x.Short)?.id; page = 0; ClearPreview(); ShowLibraryDetails();
                Say("Motions saved locally — Next clip chooses one; Play previews on Maestro."); return true;
            }
            catch (Exception error) { Report(error); return false; }
            finally { busy = false; }
        }
        public async Task<bool> PlayLibraryAsync(string motionId = null,bool? repeat = null)
        {
            if(runtimeGate?.Held==true){Say(runtimeGate.Reason);return false;}
            if (Busy || !isActiveAndEnabled) return false;
            var entry = motionId == null ? CurrentMotion() : editor.Motions.Find(motionId);
            if (entry == null || !maestro || maestro.ModelBusy || !maestro.CustomModel || !animationWorkshop || entry.rigHash != maestro.CustomModel.MotionRigHash) { Say("Choose a saved motion compatible with the loaded Maestro"); return false; }
            if (editor.AnyHeld) { Say("Release the object before previewing its motion"); return false; }
            libraryMotionId = entry.id; if (repeat.HasValue) loop = repeat.Value;
            Stop(); int request = ++motionRequest; busy = true; MotionLibrary.Lease lease = null; Say("Loading " + entry.name + "…");
            try
            {
                lease = await editor.Motions.AcquireAsync(entry.id,maestro.CustomModel.MotionRigHash);
                if (!this || disposed || !isActiveAndEnabled || request != motionRequest || editor.AnyHeld || !maestro || maestro.ModelBusy || !maestro.CustomModel || maestro.CustomModel.MotionRigHash != entry.rigHash) return false;
                editor.Select(editor.Find("maestro"));
                if (!animationWorkshop.PreviewLibraryMotion(lease,loop)) { Say(animationWorkshop.Status); return false; }
                lease = null; Say("Playing " + entry.name); return true;
            }
            catch (Exception error) { Report(error); return false; }
            finally { lease?.Dispose(); busy = false; }
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
        void OnApplicationFocus(bool value) { if (!value) Stop(); }
        void OnDisable() { if (editor) Stop(); }
        void OnDestroy()
        {
            if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged;
            Stop(); disposed = true; ClearPreview(); if (picking) ReleasePicker();
            if (Batches) Destroy(Batches);
            if (editor) { editor.Changed -= SelectionChanged; editor.Editing -= Stop; editor.ItemGrabbed -= Grabbed; }
            if (animationWorkshop) animationWorkshop.Starting -= StopTarget;
            if (maestro) maestro.ModelChanged -= MaestroChanged;
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
