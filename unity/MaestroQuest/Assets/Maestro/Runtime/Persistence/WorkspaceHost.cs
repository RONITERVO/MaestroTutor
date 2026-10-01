// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    /// <summary>Persistent shell owner. Storage selection precedes content creation; a replacement
    /// consumes an accepted-edit hold only after the exact selected pointer is durably committed.</summary>
    public sealed class WorkspaceHost:MonoBehaviour
    {
        WorkspaceGenerationStore store;Transform origin;RoomAgent agent;
        Action<WorkspaceContent,string,string,RoomRuntimeGate> build;
        IDisposable review;
        WorkspaceEditHold replacing;
        RoomRuntimeGate gate;
        bool initialized;
        public WorkspaceImport Import {get;private set;}
        public WorkspaceExport Export {get;private set;}
        public WorkspaceRuntime Runtime {get;private set;}
        internal WorkspaceSelection Selection {get;private set;}
        public WorkspaceContent Current {get;private set;}
        public bool Switching {get;private set;}
        public string Status {get;private set;}="Opening workspace";
        public bool ReviewRequired=>Selection?.Active.ReviewRequired==true;
        public event Action Changed;
        internal void Initialize(string applicationData,Transform contentOrigin,Action<WorkspaceContent,string,string,RoomRuntimeGate> factory,RoomAgent connection=null)
        {
            if(initialized)throw new InvalidOperationException("Workspace host is already initialized.");
            if(!contentOrigin||factory==null)throw new ArgumentException("Workspace shell is unavailable.");
            initialized=true;origin=contentOrigin;build=factory;agent=connection;store=new WorkspaceGenerationStore(applicationData);
            Import=gameObject.AddComponent<WorkspaceImport>();Import.Initialize(applicationData);
            Export=gameObject.AddComponent<WorkspaceExport>();Export.Initialize(null,null,null);
            Runtime=gameObject.AddComponent<WorkspaceRuntime>();Runtime.Initialize(this,System.IO.Path.Combine(applicationData,"workspace-maintenance.v1"));
            TryOpenSelected(out _);
        }
        internal bool TryOpenSelected(out string error)
        {
            error=null;if(!initialized||Current||Switching){error="The current workspace is already open or changing.";return false;}
            try {Open(store.Load());return true;}
            catch(Exception){error="The selected workspace could not be opened. Saved files are preserved; recovery is required.";Status=error;agent?.Bind(null,Status);Notify();return false;}
        }
        void Open(WorkspaceSelection selected)
        {
            // Resolve both paths before creating any content owner. No fallback to the original root.
            string data=store.DataDirectory(selected.Active),receipts=store.ReceiptDirectory(selected.Active);
            Selection=selected;gate=new RoomRuntimeGate();
            if(selected.Active.ReviewRequired)review=gate.Hold("Review the selected workspace before starting activity");
            var root=new GameObject("Selected workspace content");root.transform.SetParent(origin,false);Current=root.AddComponent<WorkspaceContent>();
            try {
                build(Current,data,receipts,gate);
                if(!Current.Editor||!Current.Rules||!Current.Controls)
                    throw new InvalidOperationException("Required workspace owners are unavailable.");
                // Ordinary stores already preserve damaged/unsupported documents independently.
                // A bad controls file must not hide a healthy room or its other tools.
                bool partial=!Current.Editor.CanSaveRoom||Current.Rules.ReadOnly||Current.Editor.ActivityProfiles.ReadOnly||!Current.Controls.ArchiveReady;
                Export.Bind(Current.Editor,Current.Rules,Current.Controls);
                Status=partial?"Workspace opened. Some saved data is unavailable; affected tools preserve its original files.":selected.Active.ReviewRequired?"Workspace opened. Review its contents before starting activity.":"Workspace ready";agent?.Bind(Current.Editor,Status);Notify();
            }catch {
                // Initialization can fail after some owners were created. Stop them before any frame.
                using(gate.Hold("Workspace initialization failed")){Current.Detach();root.SetActive(false);Destroy(root);Current=null;}
                review?.Dispose();review=null;throw;
            }
        }
        internal bool ReplaceCommitted(WorkspaceSelection selected,WorkspaceEditHold held,out string error)
        {
            error=null;
            if(Switching||!Current||held==null||!held.Owns(Current.Editor)||selected==null||!selected.Active.ReviewRequired||selected.Revision==Selection?.Revision){error="Preserve the current workspace and commit the reviewed selection before replacing its owners.";return false;}
            try {if(!JToken.DeepEquals(store.Load().Json(),selected.Json())){error="The workspace selection changed. Inspect it before continuing.";return false;}}
            catch(Exception){error="Workspace selection could not be verified. Current owners are preserved.";return false;}
            replacing=held;Switching=true;Status="Opening the selected workspace";agent?.Bind(null,Status);Notify();StartCoroutine(Replace(selected));return true;
        }
        IEnumerator Replace(WorkspaceSelection selected)
        {
            var previous=Current;Current=null;Export.Bind(null,null,null);
            // Keep the old book edit lock through the destruction frame. The new editor binds its
            // gate before input resumes; old subscriptions are gone before the new owners exist.
            previous.Detach(preserveBookLock:true);previous.gameObject.SetActive(false);Destroy(previous.gameObject);
            review?.Dispose();review=null;
            yield return null;
            try {Open(selected);}
            catch(Exception){Status="The selected workspace could not be opened. The retained previous workspace is available for recovery.";agent?.Bind(null,Status);}
            finally {replacing?.Dispose();replacing=null;Switching=false;Notify();}
        }
        void OnDisable()
        {
            if(!Switching)return;
            StopAllCoroutines();replacing?.Dispose();replacing=null;Switching=false;
            Status="Workspace opening paused. The committed selection is preserved.";agent?.Bind(null,Status);Notify();
        }
        void OnEnable(){if(initialized&&!Current&&!Switching)StartCoroutine(ReopenAfterDisable());}
        IEnumerator ReopenAfterDisable()
        {
            // Even an immediate disable/enable must let previous owners finish OnDestroy first.
            yield return null;if(initialized&&!Current&&!Switching)TryOpenSelected(out _);
        }
        void Notify()
        {
            // Observers cannot veto a committed selection or interrupt ownership cleanup.
            foreach(Action listener in Changed?.GetInvocationList()??Array.Empty<Delegate>())
                try{listener();}catch(Exception){Debug.LogWarning("A workspace status observer failed.");}
        }
        void OnDestroy()
        {
            if(Current){Current.Detach();Current.gameObject.SetActive(false);Destroy(Current.gameObject);Current=null;}
            replacing?.Dispose();replacing=null;review?.Dispose();review=null;
        }
    }
}
