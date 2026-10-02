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
    public sealed partial class ImportBatchWorkshop : MonoBehaviour
    {
        static readonly string[] Categories={ null,"idle","listening","thinking","speaking","walking","gesture","action","dance" };
        ImportWorkshop imports;
        RoomEditor editor;
        MotionBatch batch;
        bool picking,disposed;
        int selected,page,category;
        public bool Busy => picking || running!=null || batch?.Running == true;
        public MotionBatch Batch => batch;
        public string Status { get; private set; } = "Choose files, then Save batch to import their motions.";
        public string Details { get; private set; } = "Animation collections\nChoose a ZIP (2 GB, 1,024 models) or up to 128 GLB / VRM exports.\nEach model must be 64 MB or smaller.\nOnly motions are saved; your original files stay unchanged.\nPick a category before saving, or edit tags in Library.";
        public event Action Changed;
        public void Initialize(RoomEditor room,ImportWorkshop owner) { editor=room; imports=owner; InitializeShared(); }
        public void ChooseFiles()
        {
            if(!CanSelect(false,out var error)){Say(error);return;}
#if UNITY_EDITOR
            string path=UnityEditor.EditorUtility.OpenFolderPanel("Choose a folder of animated GLB / VRM exports","","");
            if(string.IsNullOrEmpty(path)){Say("File selection cancelled.");return;}
            try{
                string[] paths=Directory.EnumerateFiles(path).Where(x=>string.Equals(Path.GetExtension(x),".glb",StringComparison.OrdinalIgnoreCase)||string.Equals(Path.GetExtension(x),".vrm",StringComparison.OrdinalIgnoreCase)).Take(MotionBatch.MaximumFiles+1).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
                Prepare(new LocalMotionBatchSource(paths));
            }catch(Exception){Say("The selected folder could not be read. Choose local files again.");}
#else
            if(!CanSelect(true,out error)){Say(error);return;}SelectFiles();
#endif
        }
        public bool Prepare(IMotionBatchSource source)
        {
            if(!CanSelect(false,out var error)){source?.Dispose();Say(error);return false;}
            BeginSession();return Adopt(source);
        }
        void Update()=>PollShared();
        public async void Save()=>await SaveAsync();
        public async void Retry()=>await SaveAsync(true);
        public Task SaveAsync(bool retryFailed=false)
        {
            if(!CanChange(sessionId,version,retryFailed?"retry":"start",out var error)){Say(error);return Task.CompletedTask;}
            return StartShared(retryFailed);
        }
        public void StopBatch(){if(!CanChange(sessionId,0,"stop",out var error)){Say(error);return;}StopShared();}
        public void NextResult() => Move(1);
        public void PreviousResult() => Move(-1);
        void Move(int direction) { if (batch == null) return; selected=(selected+direction+batch.Count)%batch.Count; page=0; Refresh(); }
        public void MoreInfo() { page++; Refresh(); }
        public void NextCategory()
        {
            if(!CanChange(sessionId,version,"category",out var error)){Say(error);return;}
            category=(category+1)%Categories.Length;SetCategory(Categories[category]);
        }
        public void Clear()
        {
            if(!CanChange(sessionId,0,"clear",out var error)){Say(error);return;}ClearShared();
        }
        void Refresh()
        {
            if (disposed || batch == null) return;
            if (batch.Running) { int active=batch.ActiveIndex; if (active >= 0) selected=active; }
            var item=batch.Result(selected);
            string text="Category: "+(batch.Category ?? "none — edit tags in Library")+"\n"+item.Name+"\n"+item.State+(item.State == MotionBatchState.Saved ? " · "+item.MotionIds.Length+" motions (existing copies reused)" : "")+"\n"+(item.Error ?? (item.State == MotionBatchState.Pending ? "Waiting for Save batch / Resume." : "Importing never plays or assigns an animation."));
            string[] lines=ModelText.Wrap(text,62); int pages=Math.Max(1,(lines.Length+4)/5); page%=pages;
            Details="Batch file "+(selected+1)+"/"+batch.Count+" · details "+(page+1)+"/"+pages+"\n"+batch.Saved+" saved · "+batch.Failed+" failed · "+batch.Pending+" waiting\n"+string.Join("\n",lines,page*5,Math.Min(5,lines.Length-page*5));
            Status=batch.Status; Changed?.Invoke();
        }
        void Say(string value) { if (disposed) return; Status=value; Changed?.Invoke(); }
        void OnApplicationPause(bool value){paused=value;if(value&&!picking)StopShared();}
        void OnApplicationFocus(bool value){focused=value;if(!value&&!picking)StopShared();}
        void OnDisable()=>CloseShared();
        void OnDestroy(){disposed=true;CloseShared();if(editor)editor.RuntimeGate.Changed-=RuntimeChanged;}
    }
}
