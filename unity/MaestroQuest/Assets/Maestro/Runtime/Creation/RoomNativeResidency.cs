// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Native lifetime is independent of saved membership and visibility. These
    // transitions are internal admission primitives; observation/physics demand
    // must be supplied before an automatic regional loader can use them.
    internal enum NativeEntityPhase { Resident,Retiring,Dormant,Preparing,Failed }
    public sealed partial class RoomEditor
    {
        sealed class DormantEntity
        {
            internal Vector3 SavedPosition;internal Quaternion SavedRotation;internal float SavedScale;
            internal string ConnectionSource;
            internal Vector3 Position;internal Quaternion Rotation;internal float Scale;
            internal GameObject RetiringRoot;
            internal RecipeObject.IdleState Recipe;internal bool ConnectionBroken;
            internal NativeEntityPhase Phase;
            internal string Error;
        }
        readonly Dictionary<string,DormantEntity> dormantNative=new(StringComparer.Ordinal);
        RoomJournal nativeJournal;
        CancellationTokenSource nativeActivation;
        int nativeGeneration;
        internal bool NativeEntityDormant(string target)=>target!=null&&dormantNative.ContainsKey(target);
        internal NativeEntityPhase NativePhase(string target)
        {
            if(!dormantNative.TryGetValue(target,out var entry))return NativeEntityPhase.Resident;
            if(entry.Phase==NativeEntityPhase.Retiring&&!entry.RetiringRoot)entry.Phase=NativeEntityPhase.Dormant;
            return entry.Phase;
        }
        internal string NativeActivationError(string target)=>dormantNative.TryGetValue(target,out var entry)?entry.Error:null;
        void SynchronizeNativeResidency(HashSet<string> saved)
        {
            if(!ReferenceEquals(nativeJournal,journal)){nativeActivation?.Cancel();nativeGeneration++;dormantNative.Clear();nativeJournal=journal;}
            foreach(var id in dormantNative.Keys.Where(id=>!saved.Contains(id)).ToArray())dormantNative.Remove(id);
        }
        internal bool NativeActivationPending=>nativeActivation!=null;
        // Task completion is not effect admission: another Unity callback can edit
        // or replace the journal before the scheduler's next frame.
        internal Func<bool> CaptureNativeActionInput()
        {
            var source=journal;int revision=Revision,generation=nativeGeneration;
            return ()=>this&&isActiveAndEnabled&&ReferenceEquals(journal,source)&&Revision==revision&&nativeGeneration==generation;
        }
        bool NativeTransitionAvailable(out string error,Func<bool> actionAdmission=null)
        {
            if(!CanEditStructures(out error))return false;
            error="Wait for the room's current interaction or action to finish before loading or unloading objects";
            if(!isActiveAndEnabled||applying||AnyHeld||DrawingMode||nativeActivation!=null||(actionAdmission!=null?!actionAdmission():GetComponent<RoomRules>()?.Scheduler?.HasOtherWork("")==true))return false;
            error=null;return true;
        }
        // The caller owns observation demand. Existing native ownership, audio,
        // animation, physical links and collision admission cannot be bypassed.
        // Every connected area is retired together; a peer never loses its joint.
        internal bool RetireNativeArea(string area,out string error)
        {
            if(!NativeTransitionAvailable(out error))return false;
            var graph=ReadRetention();var members=graph?.Members(area??"");error="The authored area is unavailable";
            if(members==null)return false;var targets=graph.Closure(members);
            error="The area or a connected area is still required by the running room";
            if(targets.Any(id=>graph.Reasons(id)!=RoomRetentionReason.None))return false;
            var snapshots=new Dictionary<string,DormantEntity>(StringComparer.Ordinal);
            foreach(var id in targets){
                if(NativeEntityDormant(id))continue;
                var data=Read(id);var item=Find(id);
                error="The area's native placement is unavailable";
                if(data==null||data.IsBuiltIn||!item||!item.isActiveAndEnabled||!Frame.Read(item.transform,out var position,out var rotation,out var scale))return false;
                snapshots.Add(id,new DormantEntity{Position=position,Rotation=rotation,Scale=scale,RetiringRoot=item.gameObject,Phase=NativeEntityPhase.Retiring,ConnectionSource=JsonUtility.ToJson(data.connections.FirstOrDefault()),Recipe=item.GetComponent<RecipeObject>()?.CaptureIdleState(),ConnectionBroken=item.GetComponent<Maestro.Quest.Interaction.RoomConnectionView>()?.Broken==true});
            }
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            // Dynamic placement uses the existing non-Undo capture policy. Other
            // transient poses are cached without rewriting authored rest frames.
            CapturePhysicsPlacements();
            foreach(var pair in snapshots){var saved=Read(pair.Key);pair.Value.SavedPosition=saved.position;pair.Value.SavedRotation=saved.rotation;pair.Value.SavedScale=saved.scale;dormantNative.Add(pair.Key,pair.Value);}
            foreach(var pair in snapshots){var item=Find(pair.Key);DetachNativeIdentity(pair.Key,item);regionModels.Remove(pair.Key);item.gameObject.SetActive(false);Destroy(item.gameObject);}
            RefreshRegionCollision();UpdateSelection();error=null;return true;
        }
        internal Task<string> ActivateNativeArea(string area,CancellationToken cancellation=default)
        {
            if(!NativeTransitionAvailable(out var error))return Task.FromResult(error);
            var graph=ReadRetention();var members=graph?.Members(area??"");
            return members==null?Task.FromResult("The authored area is unavailable"):ActivateNativeTargets(graph.Closure(members),cancellation,null);
        }
        internal bool CanAcquireNativeEntities(string[] targets,out bool loading,out string error)
        {
            loading=false;error="A required saved object is missing";var graph=ReadRetention();
            if(graph==null||targets.Any(id=>!graph.Contains(id)))return false;
            // Acquisition proves instance presence. Each module decides whether an
            // inactive instance can be repaired, edited or removed by its action.
            foreach(var id in targets)if(!NativeEntityDormant(id)&&!TryGetNativeObject(id,out _,out error))return false;
            loading=graph.Closure(targets).Any(NativeEntityDormant);
            error=null;return !loading||NativeTransitionAvailable(out error);
        }
        internal Task<string> AcquireNativeEntities(string[] targets,Func<bool> actionAdmission,CancellationToken cancellation)
        {
            var graph=ReadRetention();
            if(graph==null||targets.Any(id=>!graph.Contains(id)))return Task.FromResult("A required saved object is missing");
            var closure=graph.Closure(targets);
            // Resident calls preserve existing concurrency. Only actual activation
            // needs exclusive scheduler admission while geometry is assembled.
            if(!closure.Any(NativeEntityDormant))return Task.FromResult<string>(null);
            if(actionAdmission==null)return Task.FromResult("Native activation has no action owner");
            if(!NativeTransitionAvailable(out var error,actionAdmission))return Task.FromResult(error);
            return ActivateNativeTargets(closure,cancellation,actionAdmission);
        }
        async Task<string> ActivateNativeTargets(string[] closure,CancellationToken cancellation,Func<bool> actionAdmission)
        {
            var targets=closure.Where(NativeEntityDormant).ToArray();if(targets.Length==0)return null;
            string error;
            var entries=targets.ToDictionary(id=>id,id=>dormantNative[id],StringComparer.Ordinal);
            var source=journal;int revision=Revision,generation=nativeGeneration;
            var values=targets.Select(Read).ToArray();
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(cancellation);nativeActivation=cancel;
            using var write=WriteGate.TryWrite(out error);if(write==null){nativeActivation=null;return error;}
            var roots=new List<GameObject>();RoomEditPreparation prepared=null;GameObject staging=null;
            void RuntimeChanged(){if(RuntimeGate.Held)cancel.Cancel();}
            RuntimeGate.Changed+=RuntimeChanged;
            foreach(var entry in entries.Values){entry.Phase=NativeEntityPhase.Preparing;entry.Error=null;}
            bool Current()=>this&&isActiveAndEnabled&&ReferenceEquals(journal,source)&&Revision==revision&&nativeGeneration==generation&&!RuntimeGate.Held&&!WriteGate.Frozen&&(actionAdmission==null||actionAdmission());
            void Check(){cancel.Token.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException("The room changed while activating the area");}
            try {
                // Unity destruction and asynchronous native resource retirement are
                // distinct. Existing reservation ledgers remain charged while an
                // importer/decoder drains; new admissions obey those same budgets.
                while(entries.Values.Any(entry=>entry.RetiringRoot)){Check();await Task.Yield();}
                Check();prepared=RoomEditPreparation.PrepareNative(this,values);
                foreach(var data in values.Where(data=>data.kind==RoomObjectKind.ImportedModel)){
                    var asset=await Models.ReadAsync(data.modelHash);Check();
                    var model=await PreparedImportedModel.Load(asset,data,WorldIdentity,cancel.Token,Current);
                    try{Check();prepared.EnlistModel(model,data);}catch{model.Dispose();throw;}
                }
                Check();var activating=targets.ToHashSet(StringComparer.Ordinal);
                // Publish only after every required imported model and synchronous
                // geometry candidate is ready. Reuse the ordinary native assembly.
                staging=new GameObject("Preparing authored area");staging.SetActive(false);staging.transform.SetParent(transform,false);
                Reconcile(activating,preparation:prepared,activating:activating,activationRoots:roots,activationParent:staging.transform);
                Check();
                // The inactive staging parent also contains views which deliberately
                // change activeSelf (for example a scanned drawing surface).
                // Keep each view's chosen visibility when publishing the complete set.
                foreach(var root in roots)if(root)root.transform.SetParent(transform,false);
                Liquids?.Synchronize(journal.Snapshot());
                foreach(var id in targets)dormantNative.Remove(id);
            }
            catch(Exception failure){
                foreach(var id in targets){var item=Find(id);if(item&&roots.Contains(item.gameObject)){DetachNativeIdentity(id,item);regionModels.Remove(id);}}
                foreach(var root in roots)if(root){root.SetActive(false);Art.NativeResourceLifetime.Release(root);Destroy(root);}
                while(roots.Any(root=>root))await Task.Yield();
                bool cancelled=failure is OperationCanceledException;
                error=cancelled?"Area activation was cancelled or its saved input changed":failure is ModelImportException?failure.Message:"The area's native content could not be prepared";
                foreach(var pair in entries)if(dormantNative.TryGetValue(pair.Key,out var current)&&ReferenceEquals(current,pair.Value)){current.Phase=cancelled?(current.RetiringRoot?NativeEntityPhase.Retiring:NativeEntityPhase.Dormant):NativeEntityPhase.Failed;current.Error=error;}
                if(this){RefreshRegionCollision();UpdateSelection();}return error;
            }
            finally{RuntimeGate.Changed-=RuntimeChanged;prepared?.Dispose();if(staging)Destroy(staging);if(ReferenceEquals(nativeActivation,cancel))nativeActivation=null;}
            RefreshRegionCollision();UpdateSelection();return null;
        }
        RoomObjectData NativeActivationPose(RoomObjectData data)
        {
            if(dormantNative.TryGetValue(data.id,out var entry)&&entry.SavedPosition==data.position&&entry.SavedRotation==data.rotation&&entry.SavedScale==data.scale){
                // Journal snapshots are copies. Never write an idle runtime pose to
                // the authored rest frame merely because the native view returned.
                data.position=entry.Position;data.rotation=entry.Rotation;data.scale=entry.Scale;
            }
            return data;
        }
        void RestoreNativeRecipe(RoomObjectData data,Maestro.Quest.Interaction.RoomItem item)
        {
            if(dormantNative.TryGetValue(data.id,out var entry))
                item.GetComponent<RecipeObject>()?.RestoreIdleState(entry.Recipe);
        }
        void RestoreNativeConnection(RoomObjectData data,Maestro.Quest.Interaction.RoomConnectionView view)
        {
            if(dormantNative.TryGetValue(data.id,out var entry)&&entry.ConnectionSource==JsonUtility.ToJson(data.connections.FirstOrDefault()))view.RestoreBrokenState(entry.ConnectionBroken);
        }
        void CancelNativeActivation(){nativeGeneration++;nativeActivation?.Cancel();}
    }
}
