// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        IDisposable replacing,ownership;string applicationData;bool servicesReady,destroying;
        readonly List<Task> retirements=new();
        internal bool Retiring=>(!ReferenceEquals(Current,null)&&!Current)||retirements.Any(task=>!task.IsCompleted);
        internal bool Ready=>servicesReady&&!destroying;
        RoomRuntimeGate gate;
        bool initialized;
        public WorkspaceImport Import {get;private set;}
        public WorkspaceExport Export {get;private set;}
        public WorkspaceRuntime Runtime {get;private set;}
        internal WorkspaceActivation Activation {get;private set;}
        internal WorkspaceReview Review {get;private set;}
        internal WorkspaceEvidence Evidence {get;private set;}
        internal WorkspaceHistory History {get;private set;}
        internal WorkspaceRecovery Recovery {get;private set;}
        internal WorkspaceSelection Selection {get;private set;}
        public WorkspaceContent Current {get;private set;}
        public bool Switching {get;private set;}
        public string Status {get;private set;}="Opening workspace";
        internal Task Retirement {get;private set;}=Task.CompletedTask;
        public bool ReviewRequired=>Selection?.Active.ReviewRequired==true;
        public event Action Changed;
        internal void Initialize(string applicationData,Transform contentOrigin,Action<WorkspaceContent,string,string,RoomRuntimeGate> factory,RoomAgent connection=null)
        {
            if(initialized)throw new InvalidOperationException("Workspace host is already initialized.");
            if(!contentOrigin||factory==null)throw new ArgumentException("Workspace shell is unavailable.");
            initialized=true;origin=contentOrigin;build=factory;agent=connection;store=new WorkspaceGenerationStore(applicationData);this.applicationData=applicationData;
            TryInitializeServices();
        }
        void TryInitializeServices()
        {
            if(!initialized||servicesReady||destroying)return;
            ownership??=WorkspaceHostOwnership.TryAcquire(applicationData);
            if(ownership==null){Status="Waiting for the previous workspace owner to finish";agent?.Bind(null,Status);return;}
            servicesReady=true;
            Import=gameObject.AddComponent<WorkspaceImport>();Import.Initialize(applicationData);
            Export=gameObject.AddComponent<WorkspaceExport>();Export.Initialize(null,null,null);
            Runtime=gameObject.AddComponent<WorkspaceRuntime>();Runtime.Initialize(this,System.IO.Path.Combine(applicationData,"workspace-maintenance.v1"));
            TryOpenSelected(out _);Activation=new WorkspaceActivation(this,applicationData);Review=new WorkspaceReview(this,applicationData);Recovery=new WorkspaceRecovery(this,applicationData);History=new WorkspaceHistory(this,applicationData);Evidence=new WorkspaceEvidence(this,applicationData);
        }
        internal bool TryOpenSelected(out string error)
        {
            error=null;if(!Ready||Retiring||Current||Switching){error="The current workspace is already open or changing.";return false;}
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
                retirements.Add(Current.Retire());Current=null;
                review?.Dispose();review=null;throw;
            }
        }
        internal bool ReplaceCommitted(WorkspaceSelection selected,WorkspaceEditHold held,out string error)
        {
            error="Preserve current accepted content before replacing its owners.";
            return Current&&held!=null&&held.Owns(Current.Editor)&&BeginReplacement(selected,held,out error);
        }
        internal bool ReplaceRecovered(WorkspaceSelection selected,WorkspaceRecoveryHold held,out string error)
        {
            error="Preserve and drain current owners before opening recovered content.";
            if(Current?(held==null||!held.Owns(Current.Editor)||!held.Completion.IsCompleted):held!=null)return false;
            return BeginReplacement(selected,held,out error);
        }
        bool BeginReplacement(WorkspaceSelection selected,IDisposable held,out string error)
        {
            error=null;
            if(!Ready||Retiring||Switching||selected==null||!selected.Active.ReviewRequired||(Current&&selected.Revision==Selection?.Revision)){error="Finish the current workspace transition before replacing its owners.";return false;}
            try {if(!JToken.DeepEquals(store.Load().Json(),selected.Json())){error="The workspace selection changed. Inspect it before continuing.";return false;}}
            catch(Exception){error="Workspace selection could not be verified. Current owners are preserved.";return false;}
            replacing=held;Switching=true;Status="Opening the selected workspace";agent?.Bind(null,Status);Notify();StartCoroutine(Replace(selected));return true;
        }
        IEnumerator Replace(WorkspaceSelection selected)
        {
            var previous=Current;Current=null;Export.Bind(null,null,null);
            if(!ReferenceEquals(previous,null))retirements.Add(previous.Retire(preserveBookLock:true));
            review?.Dispose();review=null;
            // Destruction alone is insufficient: accepted library writers and lifecycle saves must
            // finish before another owner opens the selected root or shared shell controls.
            yield return null;while(Retiring)yield return null;
            try {Open(selected);}
            catch(Exception){Status="The selected workspace could not be opened. Preserved files remain available for explicit recovery.";agent?.Bind(null,Status);}
            finally {replacing?.Dispose();replacing=null;Switching=false;Notify();}
        }
        async Task ReleaseAfterRetirement(IDisposable held,Task[] pending)
        {try{await Task.WhenAll(pending);}catch(Exception){}finally{held?.Dispose();}}
        void DrainReplacement()
        {
            var held=replacing;replacing=null;if(held!=null)retirements.Add(ReleaseAfterRetirement(held,retirements.ToArray()));
        }
        internal bool ApplyReviewedSelection(WorkspaceSelection selected,WorkspaceEditHold held,out string error)
        {
            error="The reviewed selection does not match the live workspace.";
            if(Switching||!Current||held==null||!held.Owns(Current.Editor)||selected==null||selected.Active.ReviewRequired||Selection==null||!Selection.Active.ReviewRequired||selected.Revision==Selection.Revision||selected.Active.Generation!=Selection.Active.Generation||selected.Active.ReceiptEpoch!=Selection.Active.ReceiptEpoch||!JToken.DeepEquals(selected.Previous?.Json(),Selection.Previous?.Json()))return false;
            try{if(!JToken.DeepEquals(store.Load().Json(),selected.Json()))return false;}catch(Exception){return false;}
            // The edit hold still owns an activity lease while review ownership is released.
            Selection=selected;review?.Dispose();review=null;Status="Workspace review completed. Start desired activity explicitly.";agent?.WorkspaceStatus(Status);Notify();error=null;return true;
        }
        void Update(){if(!ReferenceEquals(Current,null)&&!Current){retirements.Add(Current.Retire());Current=null;}if(!Ready){TryInitializeServices();return;}Activation?.Poll();Review?.Poll();Recovery?.Poll();History?.Poll();Evidence?.Poll();for(int i=retirements.Count-1;i>=0;i--)if(retirements[i].IsCompleted){_=retirements[i].Exception;retirements.RemoveAt(i);}}
        void OnApplicationPause(bool value){Activation?.Pause(value);Review?.Pause(value);Recovery?.Pause(value);History?.Pause(value);Evidence?.Pause(value);}
        void OnApplicationFocus(bool value){Activation?.Focus(value);Review?.Focus(value);Recovery?.Focus(value);History?.Focus(value);Evidence?.Focus(value);}
        void OnDisable()
        {
            Activation?.Disable();Review?.Disable();Recovery?.Disable();History?.Disable();Evidence?.Disable();
            if(!Switching)return;
            StopAllCoroutines();DrainReplacement();Switching=false;
            Status="Workspace opening paused. The committed selection is preserved.";agent?.Bind(null,Status);Notify();
        }
        void OnEnable(){if(initialized&&!Current&&!Switching)StartCoroutine(ReopenAfterDisable());}
        IEnumerator ReopenAfterDisable()
        {
            // Even an immediate disable/enable must let previous owners finish OnDestroy first.
            yield return null;while(Retiring)yield return null;if(Ready&&!Current&&!Switching)TryOpenSelected(out _);
        }
        void Notify()
        {
            // Observers cannot veto a committed selection or interrupt ownership cleanup.
            foreach(Action listener in Changed?.GetInvocationList()??Array.Empty<Delegate>())
                try{listener();}catch(Exception){Debug.LogWarning("A workspace status observer failed.");}
        }
        void OnDestroy()
        {
            destroying=true;Runtime?.Scheduler?.StopAll();agent?.Bind(null,"Closing workspace");
            if(Evidence!=null)retirements.Add(Evidence.Dispose());
            if(History!=null)retirements.Add(History.Dispose());
            if(Activation!=null)retirements.Add(Activation.Dispose());if(Review!=null)retirements.Add(Review.Dispose());if(Recovery!=null)retirements.Add(Recovery.Dispose());
            if(!ReferenceEquals(Import,null))retirements.Add(Import.Retire());if(!ReferenceEquals(Export,null))retirements.Add(Export.Retire());
            if(!ReferenceEquals(Current,null)){retirements.Add(Current.Retire());Current=null;}
            DrainReplacement();review?.Dispose();review=null;
            var lease=ownership;ownership=null;Retirement=ReleaseAfterRetirement(lease,retirements.ToArray());
        }
    }
}
