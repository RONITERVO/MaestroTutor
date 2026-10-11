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
            if(!ReferenceEquals(nativeJournal,journal)){CancelNativeActivation();dormantNative.Clear();nativeJournal=journal;}
            foreach(var id in dormantNative.Keys.Where(id=>!saved.Contains(id)).ToArray())dormantNative.Remove(id);
        }
        internal bool NativeActivationPending=>nativeActivation!=null||nativeAcquisitions.Count>0;
        bool NativeTransitionAvailable(out string error,Func<bool> actionAdmission=null)
        {
            if(!CanEditStructures(out error))return false;
            error="Wait for the room's current interaction or action to finish before loading or unloading objects";
            if(!isActiveAndEnabled||applying||AnyHeld||DrawingMode||NativeActivationPending||(actionAdmission!=null?!actionAdmission():GetComponent<RoomRules>()?.Scheduler?.HasOtherWork("")==true))return false;
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
            error=null;return !loading||NativeAcquisitionAvailable(out error);
        }
        async Task<string> ActivateNativeTargets(string[] closure,CancellationToken cancellation,Func<bool> actionAdmission)
        {
            var targets=closure.Where(NativeEntityDormant).ToArray();if(targets.Length==0)return null;
            string error;
            var entries=targets.ToDictionary(id=>id,id=>dormantNative[id],StringComparer.Ordinal);
            var inputCurrent=CaptureNativeClosureInput(closure);
            var document=journal.Snapshot();var activating=targets.ToHashSet(StringComparer.Ordinal);
            var values=document.objects.Where(data=>activating.Contains(data.id)).ToArray();
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(cancellation);nativeActivation=cancel;
            using var write=WriteGate.TryWrite(out error);if(write==null){nativeActivation=null;return error;}
            var roots=new List<GameObject>();RoomEditPreparation prepared=null;GameObject staging=null;
            void RuntimeChanged(){if(RuntimeGate.Held)cancel.Cancel();}
            RuntimeGate.Changed+=RuntimeChanged;
            foreach(var entry in entries.Values){entry.Phase=NativeEntityPhase.Preparing;entry.Error=null;}
            bool Current()=>inputCurrent()&&!RuntimeGate.Held&&!WriteGate.Frozen&&(actionAdmission==null||actionAdmission());
            void Check(){cancel.Token.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException("The room changed while activating the area");}
            try {
                // Unity destruction and asynchronous native resource retirement are
                // distinct. Existing reservation ledgers remain charged while an
                // importer/decoder drains; new admissions obey those same budgets.
                while(entries.Values.Any(entry=>entry.RetiringRoot)){Check();await Task.Yield();}
                Check();var budget=new RoomPreparationBudget(Check,nativePreparationWindow);prepared=await RoomEditPreparation.PrepareNativeAsync(this,values,budget);
                foreach(var data in values.Where(data=>data.kind==RoomObjectKind.ImportedModel)){
                    var asset=await Models.ReadAsync(data.modelHash);Check();
                    var model=await PreparedImportedModel.Load(asset,data,WorldIdentity,cancel.Token,Current);
                    try{Check();prepared.EnlistModel(model,data);}catch{model.Dispose();throw;}
                }
                Check();document=journal.Snapshot();SynchronizeVisibility(document);
                staging=new GameObject("Preparing authored area");staging.SetActive(false);staging.transform.SetParent(transform,false);
                var candidates=new Dictionary<string,Maestro.Quest.Interaction.RoomItem>(StringComparer.Ordinal);int slot=0;
                foreach(var data in values){
                    await budget.Step();
                    var root=new GameObject(data.kind.ToString());root.transform.SetParent(staging.transform,false);roots.Add(root);
                    var item=root.AddComponent<CreatedRoomObject>().BuildPrepared(data,Models,RuntimeGate,WorldIdentity,prepared);
                    ApplyPose(item,NativeActivationPose(data));ConfigureNativeObject(data,item,document,prepared,true,slot++);RestoreNativeRecipe(data,item);
                    candidates.Add(data.id,item);
                }
                Check();
                // Refresh physical bindings after preparation's frame boundaries.
                // The complete set stays hidden and absent from both registries.
                foreach(var item in candidates.Values)item.GetComponent<ScannedDrawingView>()?.Sync();
                Check();applying=true;
                try{
                    // No awaits after publication begins. All IDs exist before
                    // any connection resolves a peer or any root becomes active.
                    foreach(var data in values){var item=candidates[data.id];AddIdentity(data.id,item);room.Register(item);if(data.kind==RoomObjectKind.ImportedModel)regionModels[data.id]=item.GetComponent<CreatedRoomObject>();}
                    foreach(var data in values){var item=candidates[data.id];var hinge=item.GetComponent<Maestro.Quest.Interaction.RoomConnectionView>();if(!hinge&&data.connections.Length>0)hinge=item.gameObject.AddComponent<Maestro.Quest.Interaction.RoomConnectionView>();if(hinge){hinge.Apply(this,data.connections);RestoreNativeConnection(data,hinge);}}
                    Check();
                    // Keep component-local visibility, including missing-anchor ink.
                    foreach(var root in roots)if(root)root.transform.SetParent(transform,false);
                    Check();
                }finally{applying=false;}
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
        void CancelNativeActivation(){nativeGeneration++;foreach(var job in nativeAcquisitions.ToArray())job.Cancel();nativeActivation?.Cancel();}
    }
}
