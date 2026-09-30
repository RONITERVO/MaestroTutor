// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    public sealed class ImportBatchWorkshop : MonoBehaviour
    {
        static readonly string[] Categories={ null,"idle","listening","thinking","speaking","walking","gesture","action","dance" };
        ImportWorkshop imports;
        RoomEditor editor;
        MotionBatch batch;
        bool picking,disposed;
        int selected,page,category;
#if UNITY_ANDROID && !UNITY_EDITOR
        string selectionSession;
        float selectionStarted;
#endif
        public bool Busy => picking || batch?.Running == true;
        public MotionBatch Batch => batch;
        public string Status { get; private set; } = "Choose files, then Save batch to import their motions.";
        public string Details { get; private set; } = "Animation collections\nChoose up to 128 GLB / VRM exports.\nEach file must be 64 MB or smaller.\nOnly motions are saved; your original files stay unchanged.\nPick a category before saving, or edit tags in Library.";
        public event Action Changed;
        public void Initialize(RoomEditor room,ImportWorkshop owner) { editor=room; imports=owner; }
        public void ChooseFiles()
        {
            if (imports.Busy) { Say("Stop the current batch or wait for the model check first."); return; }
            ClearSession(); imports.Cancel(); imports.Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            try { selectionSession=AndroidMotionBatchSource.Open(); selectionStarted=Time.realtimeSinceStartup; picking=true; Say("Select up to 128 animation exports in the document picker."); }
            catch (Exception) { Say("The document picker could not open. Resume Maestro and try again."); }
#elif UNITY_EDITOR
            string path=UnityEditor.EditorUtility.OpenFolderPanel("Choose a folder with up to 128 animated GLB / VRM exports", "", "");
            if (string.IsNullOrEmpty(path)) { Say("File selection cancelled."); return; }
            try
            {
                string[] paths=Directory.EnumerateFiles(path).Where(x => string.Equals(Path.GetExtension(x),".glb",StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(x),".vrm",StringComparison.OrdinalIgnoreCase)).Take(MotionBatch.MaximumFiles+1).OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToArray();
                Prepare(new LocalMotionBatchSource(paths));
            }
            catch (Exception error) { Say(error is ModelImportException ? error.Message : "The folder could not be read. Choose local files again."); }
#else
            Say("Batch selection is available on Quest and in the Unity Editor.");
#endif
        }
        public bool Prepare(IMotionBatchSource source)
        {
            if (imports.Busy || disposed) { source?.Dispose(); return false; }
            ClearSession();
            try { batch=new MotionBatch(editor.Motions,source); }
            catch { source?.Dispose(); throw; }
            batch.Changed+=Refresh; selected=0; page=0; category=0; Refresh(); return true;
        }
        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!picking) return;
            try
            {
                if (Time.realtimeSinceStartup-selectionStarted > 310) { picking=false; AndroidMotionBatchSource.ClosePicker(); Say("File selection timed out. Choose files again."); return; }
                var result=AndroidMotionBatchSource.Poll(); if (result == null || result.session != selectionSession) return;
                if (result.kind != "ready" || !string.IsNullOrEmpty(result.error))
                { picking=false; AndroidMotionBatchSource.ClosePicker(); Say(result.error ?? "The selected files could not be read."); return; }
                picking=false; Prepare(new AndroidMotionBatchSource(result.count,result.session));
            }
            catch (Exception error) { picking=false; AndroidMotionBatchSource.ClosePicker(); Say(error is ModelImportException ? error.Message : "File selection was interrupted. Choose files again."); }
#endif
        }
        public async void Save() => await SaveAsync();
        public async void Retry() => await SaveAsync(true);
        public async Task SaveAsync(bool retryFailed=false)
        {
            using var write=editor.WriteGate.TryWrite(out var blocked);if(write==null){Say(blocked);return;}
            if (imports.Busy || disposed) return;
            if (batch == null) { Say("Choose files first. Save batch confirms you may use these assets."); return; }
            imports.Stop(); page=0;
            await batch.RunAsync(retryFailed);
            if (!this || disposed) return;
            imports.RefreshLibraryDetails(); Refresh();
        }
        public void StopBatch() { if (batch?.Running == true) batch.Stop(); else Say("No batch is running. Choose files or Resume waiting files."); }
        public void NextResult() => Move(1);
        public void PreviousResult() => Move(-1);
        void Move(int direction) { if (batch == null) return; selected=(selected+direction+batch.Count)%batch.Count; page=0; Refresh(); }
        public void MoreInfo() { page++; Refresh(); }
        public void NextCategory()
        {
            if (batch == null) { Say("Choose files before choosing a category."); return; }
            if (batch.Running || batch.Results.Any(x => x.State != MotionBatchState.Pending)) { Say("This batch's category is fixed after saving starts. Edit custom tags in Library."); return; }
            category=(category+1)%Categories.Length; batch.SetCategory(Categories[category]);
        }
        public void Clear()
        {
            if (Busy) { batch?.Stop(); Say("Stop the batch before clearing its results."); return; }
            ClearSession(); Details="Animation collections\nChoose up to 128 GLB / VRM exports.\nPreviously saved motions remain in Library.\nSave batch confirms you may use the selected assets."; Say("Batch results cleared. Original files and saved motions are unchanged.");
        }
        void Refresh()
        {
            if (disposed || batch == null) return;
            var entries=batch.Results;
            if (batch.Running) { int active=Array.FindIndex(entries,x => x.State == MotionBatchState.Reading || x.State == MotionBatchState.Importing); if (active >= 0) selected=active; }
            var item=entries[selected];
            string text="Category: "+(batch.Category ?? "none — edit tags in Library")+"\n"+item.Name+"\n"+item.State+(item.State == MotionBatchState.Saved ? " · "+item.MotionIds.Length+" motions (existing copies reused)" : "")+"\n"+(item.Error ?? (item.State == MotionBatchState.Pending ? "Waiting for Save batch / Resume." : "Importing never plays or assigns an animation."));
            string[] lines=ModelText.Wrap(text,62); int pages=Math.Max(1,(lines.Length+4)/5); page%=pages;
            Details="Batch file "+(selected+1)+"/"+batch.Count+" · details "+(page+1)+"/"+pages+"\n"+batch.Saved+" saved · "+batch.Failed+" failed · "+batch.Pending+" waiting\n"+string.Join("\n",lines,page*5,Math.Min(5,lines.Length-page*5));
            Status=batch.Status; Changed?.Invoke();
        }
        void Say(string value) { if (disposed) return; Status=value; Changed?.Invoke(); }
        void ClearSession() { if (batch == null) return; batch.Changed-=Refresh; batch.Dispose(); batch=null; }
        void OnApplicationPause(bool value) { if (value) batch?.Stop(); }
        void OnApplicationFocus(bool value) { if (!value) batch?.Stop(); }
        void OnDisable() { batch?.Stop(); CloseSelection(); }
        void CloseSelection()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (picking) { picking=false; AndroidMotionBatchSource.ClosePicker(); }
#endif
        }
        void OnDestroy() { disposed=true; CloseSelection(); ClearSession(); }
    }
}
