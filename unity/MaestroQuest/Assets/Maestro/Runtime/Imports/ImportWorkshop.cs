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
    public sealed partial class ImportWorkshop : MonoBehaviour
    {
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
            editor = source; editor.Changed += SelectionChanged; editor.Editing += Stop; InitializeSelection();
            runtimeGate=editor.RuntimeGate;runtimeGate.Changed+=RuntimeChanged;RuntimeChanged();
            editor.ItemGrabbed += Grabbed; if (animations) animations.Starting += StopTarget;
            animationWorkshop = animations;
            maestro = editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            if (maestro) maestro.ModelChanged += MaestroChanged;
        }
        AnimationWorkshop animationWorkshop;
        public void Pick()
        {
            if(HasArchive){BrowseArchive();return;}
#if UNITY_EDITOR
            if(!CanBeginSelection(false,out var error)){Say(error);return;}
            string path=UnityEditor.EditorUtility.OpenFilePanelWithFilters("Import a model you may use","",new[]{"GLB and VRM models","glb,vrm"});
            if(!string.IsNullOrEmpty(path))_ = PrepareLocalPathAsync(path);
#else
            if(!CanBeginSelection(true,out var error)){Say(error);return;}
            BeginSelection();
#endif
        }
        void Update()=>PollSelection();
        public Task PrepareAsync(string name,byte[] bytes)
        {
            if(!CanBeginSelection(false,out var error)){Say(error);return Task.CompletedTask;}
            BeginSelectionOwner();var copy=(byte[])bytes.Clone();
            return PrepareSelectionAsync(()=>Task.Run(()=>ModelLibrary.Inspect(name,copy)),selectionId,false);
        }
        Task PrepareLocalPathAsync(string path)
        {
            BeginSelectionOwner();return PrepareSelectionAsync(()=>Task.Run(()=>ModelLibrary.Inspect(Path.GetFileName(path),ModelLibrary.ReadBounded(path))),selectionId,false);
        }
        async Task PreviewAsync(ModelAsset asset)
        {
            if (!this || disposed) return;
            var root = new GameObject("Model import preview"); root.transform.SetParent(transform, false); root.transform.localPosition = new Vector3(.55f, 1.35f, .65f);
            preview = root.AddComponent<ImportedModel>();preview.ConfigureResourceOwner(editor.WorldIdentity,"","preview");
            try { await preview.LoadAsync(asset); }
            catch { if (preview) Destroy(preview.gameObject); preview = null; throw; }
            if (!this || disposed) return;
            pending = asset; libraryMode = false; clip = 0; page = 0; ShowDetails(); Say("Preview ready — choose Add model" + (preview.IsHumanoid ? " or Use Maestro" : ""));
        }
        public async void Accept() => await AcceptAsync();
        public Task<bool> AcceptAsync()=>AcceptPreviewManually("object");
        public void Cancel() { if(!CanCancelSelection(selectionId,out var error)){Say(error);return;} CancelSelection(selectionId); }
        void ClearPreview(bool finishSelection=true) { if (preview) { preview.gameObject.SetActive(false); Destroy(preview.gameObject); } preview = null; pending = null; if(finishSelection)FinishSelectionPreview(); Details = "Select Maestro or an imported object to play its clips.\nUse Maestro selects a compatible GLB or VRM humanoid."; Changed?.Invoke(); }
        public async void UseMaestro() => await UseMaestroAsync();
        public async Task<bool> UseMaestroAsync()
        {
            if(HasArchive&&!HasPreview){Say("Preview a ZIP model before using it as Maestro");return false;}
            if(HasPreview)return await AcceptPreviewManually("maestro");
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
        public async void DefaultMaestro()
        {
            if(Busy)return;
            if(!editor.SetMaestroModel(null)){Say(editor.Status);return;}
            bool ready=await maestro.ModelLoad;
            if(this&&!disposed)Say(ready?"Included Maestro restored — Undo brings back your custom avatar":maestro.ModelStatus);
        }
        void MaestroChanged() { motionRequest++; if (libraryMode) ShowLibraryDetails(); if (maestro) Say(maestro.ModelStatus); }
        ImportedModel Target => HasPreview ? preview : editor.SelectedId == "maestro" ? maestro?.CustomModel : editor.Find(editor.SelectedId)?.GetComponent<CreatedRoomObject>()?.Model;
        public void NextClip() { if (libraryMode) { NextLibraryMotion(); return; } var target = Target; if (!target || target.ClipCount == 0) { Say("This model has no embedded animation clips"); return; } Stop(); clip = (clip + 1) % target.ClipCount; Say("Clip " + (clip + 1) + ": " + target.ClipName(clip)); }
        void RuntimeChanged(){if(runtimeGate.Held)Stop();}
        public void Play()
        {
            if(runtimeGate?.Held==true){Say(runtimeGate.Reason);return;}
            if (libraryMode) { _ = PlayLibraryAsync(); return; }
            var target = Target; if (!target || !target.Ready || target.ClipCount == 0) { Say("Choose an imported model or Maestro with animation clips"); return; }
            if(target.PlaybackIssue!=null){Say(target.PlaybackIssue);return;}
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
        public void NextDetails() { if(BrowsingArchive){ShowArchive();return;}page++; ShowDetails(); }
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
            if(HasArchive&&!HasPreview){Say("Preview a ZIP model before saving its motions");return false;}
            if(HasPreview)return await AcceptPreviewManually("motions");
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
        void OnApplicationPause(bool value) { selectionPaused=value; if (value) { if(selectionPhase=="accepting")selectionCancel?.Cancel(); Stop(); } }
        void OnApplicationFocus(bool value) { selectionFocused=value; if (!value) { if(selectionPhase=="accepting")selectionCancel?.Cancel(); Stop(); } }
        void OnDisable() { CloseSelection(); if (editor) Stop(); }
        void OnDestroy()
        {
            if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged;
            Stop(); disposed = true; CloseSelection(); ClearPreview();
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
