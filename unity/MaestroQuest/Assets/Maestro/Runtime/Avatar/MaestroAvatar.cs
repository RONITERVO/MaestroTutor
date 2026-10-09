// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using System;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    /// <summary>Presentation follows the existing tutor; never starts a second audio session.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class MaestroAvatar : MonoBehaviour
    {
        NativeBookBrowser browser;
        public NativeBookBrowser Browser
        {
            get => browser;
            set { browser = value; browser?.BindSpeechOutput(SpeechOutput); }
        }
        public bool ReducedMotion;
        RoomRuntimeGate runtimeGate;
        internal void ConfigureRuntime(RoomRuntimeGate gate){if(runtimeGate==gate)return;if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged;runtimeGate=gate;if(gate!=null)gate.Changed+=RuntimeChanged;RuntimeChanged();}
        void RuntimeChanged(){
            blockedSnapshot=Browser?Browser.Snapshot:observedSnapshot;observedActivity=null;
            if(runtimeGate?.Held!=true)return;
            SpeechOutput?.Stop();EndAmbient();gestureLayer?.Stop();walkMotion?.Stop();StopImportedClip();spatialWalking=false;
            if(animator)animator.speed=0;
        }
        Animator animator;
        string activity;
        float greetingUntil;
        JointPose[] savedPose;
        bool editing;
        bool spatialWalking;
        int importedClip = -1, walkClip = -1;
        float importedTime, importedSpeed = 1;
        bool importedLoop;
        MotionLibrary.Lease libraryMotion;
        AvatarAuthoredTravel authoredTravel;
        public string ImportedPlaybackError {get;private set;}
        public string ImportedMovement=>authoredTravel!=null?"authored":"inPlace";
        public bool CanPlayAuthored(out string error){error="Authored travel needs the room movement service";var spatial=GetComponent<AvatarSpatialMotion>();return spatial&&spatial.CanBeginAuthored(out error);}
        AvatarWalkMotion walkMotion;
        AvatarGestureLayer gestureLayer;
        public bool UpperBodyOwnedBy(string owner) => gestureLayer?.Owner == owner;
        public bool UpperBodyActive => gestureLayer?.Active == true;
        public bool BeginUpperBody(string owner,string gesture)
        {
            if(runtimeGate?.Held==true||ModelBusy || !PoseRig || gestureLayer == null || !gestureLayer.Begin(owner,gesture))return false;
            activityMotion?.Cancel(); greetingUntil=0; return true;
        }
        public void EndUpperBody(string owner) => gestureLayer?.End(owner);
        RoomOwnership ownership;
        RoomWorldIdentity modelWorld;
        RoomOwnership.Lease ambientLease;
        string ambientOwner;
        BehaviourCatalog.Claim[] ambientClaims;
        public void ConfigureOwnership(RoomOwnership service,string target,RoomWorldIdentity world=null) {
            modelWorld=world?.Copy();
            if(ownership!=null)EndAmbient();ownership=service;ambientOwner="ambient:"+target;
            ambientClaims=new[]{new BehaviourCatalog.Claim(target,"wholeTarget")};
        }
        void EndAmbient() {
            ambientLease?.Dispose();ambientLease=null;activityMotion?.Cancel();
            greetingUntil=0;activity=null;if(animator)animator.speed=0;
        }
        bool AmbientAllowed(bool eligible) {
            if(!eligible){if(ambientLease!=null)EndAmbient();return false;}
            if(ownership==null)return true; // Standalone preview prefabs have no room arbiter.
            if(ambientLease?.Held!=true&&!ownership.TryAcquire(ambientOwner,"Maestro tutor activity",RoomActorRole.Ambient,ambientClaims,
                _=>EndAmbient(),out ambientLease,out _))return false;
            return true;
        }
        AvatarActivityMotion activityMotion;
        bool activityPlayback,libraryOpen,paused,focused=true;
        BookSnapshot observedSnapshot,blockedSnapshot;
        string observedActivity;
        JointPose[] activityBlend;
        Vector3 blendHips;
        float blendElapsed;
        public string ActivityMotionId => activityPlayback ? libraryMotion?.Id : null;
        public string ActivityMotionStatus => activityMotion?.Status;
        public event Action ActivityMotionChanged;
        public void ConfigureActivityProfiles(AvatarActivityProfiles profiles,MotionLibrary library) => activityMotion.Configure(profiles,library);
        public void SetActivityLibraryOpen(bool value) { libraryOpen=value; if (value) activityMotion?.Cancel(); }
        public void ObserveTutorState(BookSnapshot snapshot)
        {
            observedSnapshot=snapshot;
            observedActivity=runtimeGate?.Held!=true&&!paused && focused && snapshot != null && !ReferenceEquals(snapshot,blockedSnapshot) && snapshot.version == 1 && !snapshot.audioPaused &&
                (snapshot.activity == "idle" || snapshot.activity == "listening" || snapshot.activity == "thinking" || snapshot.activity == "speaking") ? snapshot.activity : null;
        }
        GameObject included;
        ImportedModel custom;
        string requestedModel = "";
        int modelGeneration;
        bool disposed;
        public AvatarPoseRig PoseRig { get; private set; }
        public NativeSpeechOutput SpeechOutput { get; private set; }
        public string ModelHash { get; private set; } = "";
        public string ModelStatus { get; private set; } = "Included Maestro";
        public bool ModelBusy { get; private set; }
        public Task<bool> ModelLoad { get; private set; } = Task.FromResult(true);
        public ImportedModel CustomModel => custom;
        public bool IsImportedClipPlaying => importedClip >= 0 || libraryMotion != null;
        public int WalkClip => walkClip;
        public string WalkMotionId => walkMotion?.Id;
        public string LibraryMotionId => libraryMotion?.Id;
        public string WalkMotionStatus => walkMotion?.Status;
        public event Action WalkMotionChanged;
        public string WalkClipName => !string.IsNullOrEmpty(WalkMotionId) ? walkMotion.Name : custom && walkClip >= 0 && walkClip < custom.ClipCount ? custom.ClipName(walkClip) : "Included walk";
        public event Action ModelChanged;

        void Awake()
        {
            activityMotion=new AvatarActivityMotion(this); activityMotion.Changed+=() => ActivityMotionChanged?.Invoke();
            walkMotion = new AvatarWalkMotion(this); walkMotion.Changed += () => WalkMotionChanged?.Invoke();
            var prefab = Resources.Load<GameObject>("Avatars/DefaultMaestro");
            if (!prefab) { Debug.LogError("The included Maestro model is missing."); return; }
            var model = Instantiate(prefab, transform);
            included = model;
            model.name = "Maestro drawing";
            model.AddComponent<PencilModelStyle>().Apply();
            if (!model.TryGetComponent(out animator)) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Avatars/MaestroAnimations");
            animator.applyRootMotion = false;
            // The included skeleton continues driving the selected humanoid even
            // while its own meshes are hidden.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PoseRig = gameObject.AddComponent<AvatarPoseRig>(); PoseRig.Initialize(animator);
            var voice = new GameObject("Maestro mouth voice"); voice.transform.SetParent(transform, false);
            SpeechOutput = voice.AddComponent<NativeSpeechOutput>();
            SpeechOutput.ConfigureAnchor(PoseRig, transform, new Vector3(0, .08f, .08f), new Vector3(0, 1.4f, .08f));
            browser?.BindSpeechOutput(SpeechOutput);
            if (savedPose != null) { PoseRig.SetManual(true); PoseRig.Apply(savedPose); }
            if(animator.runtimeAnimatorController)gestureLayer=new AvatarGestureLayer(included,animator,PoseRig);
            greetingUntil = Time.unscaledTime + (ReducedMotion ? 0 : 2.5f);
            if (savedPose == null && !ReducedMotion && animator.runtimeAnimatorController) animator.Play("Greeting");
        }

        public Task<bool> SetModel(string hash, ModelLibrary library, bool retry = false)
        {
            hash ??= "";
            if (requestedModel == hash && (!retry || ModelBusy)) return ModelLoad;
            return BeginModel(hash,library,null,CancellationToken.None);
        }
        // Selection prepares a hidden candidate, then commits the room before
        // replacing the visible model. Restore still falls back to the included rig.
        internal Task<bool> SelectModel(string hash,ModelLibrary library,Func<bool> commit,CancellationToken cancellation)=>BeginModel(hash??"",library,commit,cancellation);
        Task<bool> BeginModel(string hash,ModelLibrary library,Func<bool> commit,CancellationToken cancellation)
        {
            EndAmbient();walkMotion?.Stop();StopImportedClip();gestureLayer?.Stop();
            string previous=requestedModel;requestedModel=hash;int generation=++modelGeneration;
            ModelBusy=true;ModelStatus=hash.Length==0?"Selecting included Maestro…":"Loading custom Maestro…";ModelChanged?.Invoke();
            return ModelLoad=LoadModel(hash,library,generation,previous,commit,cancellation);
        }
        async Task<bool> LoadModel(string hash,ModelLibrary library,int generation,string previous,Func<bool> commit,CancellationToken cancellation)
        {
            GameObject candidateRoot=null;ImportedModel candidate=null;bool accepted=false;
            try
            {
                HumanoidRetargeter retargeter=null;
                if(hash.Length!=0){
                    var asset=await library.ReadAsync(hash);
                    cancellation.ThrowIfCancellationRequested();if(!this||disposed||generation!=modelGeneration)return false;
                    candidateRoot=new GameObject("Custom Maestro");candidateRoot.SetActive(false);candidateRoot.transform.SetParent(transform,false);
                    candidate=candidateRoot.AddComponent<ImportedModel>();candidate.ConfigureResourceOwner(modelWorld,"maestro","avatar");await candidate.LoadAsync(asset);
                    cancellation.ThrowIfCancellationRequested();if(!this||disposed||generation!=modelGeneration)return false;
                    candidate.FitAsMaestro();retargeter=candidateRoot.AddComponent<HumanoidRetargeter>();retargeter.Initialize(PoseRig,candidate.Humanoid);
                }
                cancellation.ThrowIfCancellationRequested();if(!this||disposed||generation!=modelGeneration)return false;
                if(commit!=null&&!commit()){ModelStatus="Avatar selection was not saved; previous avatar kept.";return false;}
                accepted=true;UseIncluded();
                if(candidate){
                    custom=candidate;candidateRoot=null;PoseRig.SetDisplayRig(retargeter);
                    foreach(var renderer in included.GetComponentsInChildren<Renderer>())renderer.enabled=false;
                    custom.gameObject.SetActive(true);retargeter.ApplyPose();ModelHash=hash;
                }
                ModelStatus=hash.Length==0?"Included Maestro":"Custom Maestro ready — gestures, poses and recordings retained";return true;
            }
            catch(Exception error)
            {
                if(this&&!disposed&&generation==modelGeneration){
                    if(commit==null)UseIncluded();
                    string reason=error is OperationCanceledException?"Avatar selection cancelled.":error is ModelImportException?error.Message:"The custom avatar could not load.";
                    ModelStatus=reason+(commit==null?" Using included Maestro.":" Previous avatar kept.");
                }
                return false;
            }
            finally
            {
                if(!accepted)candidate?.Dispose();
                if(candidateRoot)Destroy(candidateRoot);
                if(this&&!disposed&&generation==modelGeneration){
                    if(commit!=null&&!accepted)requestedModel=previous;
                    ModelBusy=false;ModelChanged?.Invoke();
                }
            }
        }
        void UseIncluded()
        {
            StopImportedClip();
            if (PoseRig) PoseRig.SetDisplayRig(null);
            if (custom) { custom.gameObject.SetActive(false); Destroy(custom.gameObject); custom = null; }
            if (included) foreach (var renderer in included.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            ModelHash = "";
        }

        void Update()
        {
            if(runtimeGate?.Held==true){if(animator)animator.speed=0;return;}
            gestureLayer?.RestoreBase();
            if (Browser) ObserveTutorState(Browser.Snapshot);
            bool ambient=AmbientAllowed(!UpperBodyActive&&!paused&&focused&&!ReducedMotion&&!editing&&!spatialWalking&&savedPose==null&&!libraryOpen&&!ModelBusy&&(!IsImportedClipPlaying||activityPlayback));
            if (activityMotion.Apply(observedActivity,ambient && custom && (observedActivity != "idle" || Time.unscaledTime >= greetingUntil),Time.unscaledTime)) return;
            if(ownership!=null&&!ambient){if(!editing&&animator)animator.speed=0;return;}
            if (paused || !focused || editing || savedPose != null || IsImportedClipPlaying) return;
            if (!animator || !animator.runtimeAnimatorController || Time.unscaledTime < greetingUntil) return;
            string state = ReducedMotion ? "Idle" : observedActivity switch
            {
                "speaking" => "Speaking",
                "listening" => "Listening",
                "thinking" => "Listening",
                _ => "Idle"
            };
            if (activity != state) { activity = state; animator.CrossFadeInFixedTime(state, .25f); }
            animator.speed = ReducedMotion ? 0 : 1;
        }

        public void SetSavedPose(JointPose[] pose)
        {
            if (pose != null) activityMotion?.Cancel();
            savedPose = MotionFrame.CopyJoints(pose);
            if (!PoseRig || editing) return;
            PoseRig.SetManual(savedPose != null || IsImportedClipPlaying); PoseRig.Apply(savedPose);
            if (savedPose == null) activity = null;
        }
        public void SetEditing(bool value,bool preserveUpperBody=false)
        {
            if(value)EndAmbient();
            if(!preserveUpperBody)gestureLayer?.Stop();
            walkMotion?.Stop(); StopImportedClip();
            editing = value;
            if (!PoseRig) return;
            PoseRig.SetManual(value || savedPose != null);
            if (!value) { PoseRig.SetPosing(false); PoseRig.Apply(savedPose); activity = null; spatialWalking = false; animator.speed = runtimeGate?.Held==true?0:1; }
        }
        public void SpatialWalk(float metresPerSecond)
        {
            if(runtimeGate?.Held==true)return;
            bool walking = metresPerSecond > .025f && !ReducedMotion;
            float rate = walking ? Mathf.Clamp(metresPerSecond/(.65f*transform.lossyScale.y),.25f,2) : 1;
            if (!walking) walkMotion?.Stop();
            if (walking && walkMotion != null && walkMotion.Apply(rate)) { spatialWalking = true; activity = "spatial"; return; }
            if (walking && custom && walkClip >= 0 && walkClip < custom.ClipCount && custom.ClipDuration(walkClip) >= .1f)
            {
                if (!spatialWalking || activity != "spatial" || importedClip != walkClip) PlayImportedClip(walkClip,true);
                importedSpeed = rate; spatialWalking = true; activity = "spatial"; return;
            }
            StopImportedClip();
            PoseRig.SetManual(false);
            if (walking != spatialWalking || activity != "spatial") animator.CrossFadeInFixedTime(walking ? "Walk" : "Idle",.15f);
            spatialWalking = walking; activity = "spatial";
            animator.speed = ReducedMotion ? 0 : rate;
        }
        public void SetWalkClip(int index) { walkClip = index; walkMotion?.Configure(null,null); }
        public void SetWalkReference(int index,string id,MotionLibrary library) { walkClip = index; walkMotion?.Configure(library,id); }
        public void SetImportedPlaybackRate(float rate) { if (float.IsFinite(rate)) importedSpeed = Mathf.Clamp(rate,.25f,2); }
        public bool PlayImportedClip(int index, bool loop,bool authored=false)
        {
            if (runtimeGate?.Held==true||ModelBusy || !custom || index < 0 || index >= custom.ClipCount || custom.ClipDuration(index) <= 0) return false;
            if(authored&&!CanPlayAuthored(out _))return false;
            StopImportedClip(); custom.Stop();ImportedPlaybackError=null;
            importedClip = index; importedLoop = loop; importedTime = 0; importedSpeed = 1;
            PoseRig.SetManual(true); activity = "imported";
            return BeginImportedSample(authored);
        }
        // Ownership of the lease transfers only on success; Stop always releases it.
        public bool PlayLibraryMotion(MotionLibrary.Lease motion,bool loop,bool authored=false)
        {
            activityMotion?.Cancel(); return StartLibraryMotion(motion,loop,false,authored);
        }
        internal bool PlayActivityMotion(MotionLibrary.Lease motion,bool loop)
        {
            if(runtimeGate?.Held==true)return false;BeginActivityBlend(); return StartLibraryMotion(motion,loop,true);
        }
        bool StartLibraryMotion(MotionLibrary.Lease motion,bool loop,bool ambient,bool authored=false)
        {
            if (runtimeGate?.Held==true||ModelBusy || !custom || motion == null || !motion.Clip || motion.RigHash != custom.MotionRigHash) return false;
            if(authored&&!CanPlayAuthored(out _))return false;
            StopClip(); custom.Stop();ImportedPlaybackError=null; libraryMotion = motion; activityPlayback=ambient; importedLoop = loop; importedTime = 0; importedSpeed = 1;
            PoseRig.SetManual(true); activity = "imported";
            if(BeginImportedSample(authored))return true;
            // A failed start leaves disposal to the caller.
            libraryMotion=null;return false;
        }
        bool BeginImportedSample(bool authored)
        {
            if(authored)authoredTravel=new AvatarAuthoredTravel(this,custom,GetComponent<AvatarSpatialMotion>(),SampleRawImported,ImportedDuration,importedLoop);
            if(SampleImportedAt(0))return true;
            authoredTravel?.End();authoredTravel=null;custom.Stop();importedClip=-1;return false;
        }
        bool SampleRawImported(float time)=>libraryMotion!=null?custom.SampleMotion(libraryMotion,time,false):custom.SampleClip(importedClip,time,false);
        internal bool SampleImportedAt(float time)
        {
            if(!IsImportedClipPlaying||!custom||!float.IsFinite(time)||time<0)return false;
            if(authoredTravel!=null){if(authoredTravel.Sample(time))return true;ImportedPlaybackError=authoredTravel.Error;return false;}
            if(!SampleRawImported(importedLoop?time%ImportedDuration:Mathf.Min(time,ImportedDuration)))return false;
            PoseRig.CaptureImportedPose();return true;
        }
        internal bool FinishImportedAt(float time,out string error){if(authoredTravel!=null&&ImportedPlaybackError==null)SampleImportedAt(time);error=ImportedPlaybackError;return error==null;}
        float ImportedDuration => libraryMotion != null ? (libraryMotion.Clip ? libraryMotion.Clip.length : 0) : custom.ClipDuration(importedClip);
        public void StopImportedClip() { activityMotion?.Cancel(); activityBlend=null; StopClip(); }
        internal void StopActivityMotion(bool blend)
        {
            if (!activityPlayback) { if (!blend) activityBlend=null; return; }
            if (blend) BeginActivityBlend(); else activityBlend=null;
            StopClip();
        }
        void BeginActivityBlend()
        {
            if (!PoseRig || ReducedMotion) { activityBlend=null; return; }
            activityBlend=PoseRig.Capture(); blendHips=PoseRig.CanonicalBone(PoseJoint.Hips).localPosition; blendElapsed=0;
        }
        void BlendActivity()
        {
            if (activityBlend == null || !PoseRig) return;
            blendElapsed+=Mathf.Min(Time.deltaTime,.05f); float t=Mathf.Clamp01(blendElapsed/.25f);
            foreach (var from in activityBlend) { var bone=PoseRig.CanonicalBone(from.joint); if (bone) bone.localRotation=Quaternion.Slerp(from.rotation,bone.localRotation,t); }
            var hips=PoseRig.CanonicalBone(PoseJoint.Hips); hips.localPosition=Vector3.Lerp(blendHips,hips.localPosition,t);
            if (t >= 1) activityBlend=null;
        }
        void StopClip()
        {
            authoredTravel?.End();authoredTravel=null;
            activityPlayback=false;
            if (!IsImportedClipPlaying) return;
            libraryMotion?.Dispose(); libraryMotion = null;
            importedClip = -1; if (custom) custom.Stop();
            if (PoseRig) PoseRig.SetManual(editing || savedPose != null);
        }
        void LateUpdate()
        {
            if(runtimeGate?.Held==true)return;
            if (!IsImportedClipPlaying || !custom) BlendActivity();
            else {
                if(ImportedPlaybackError!=null)return;
                importedTime += Mathf.Min(Time.deltaTime,.05f)*importedSpeed;
                if(ImportedDuration<=0)StopClip();
                else {
                    bool sampled=SampleImportedAt(importedTime);
                    if(sampled&&!importedLoop&&importedTime>=ImportedDuration){if(activityPlayback)BeginActivityBlend();StopClip();}
                }
                if (activityPlayback) BlendActivity();
            }
            gestureLayer?.Apply(Time.deltaTime,ReducedMotion);
        }
        public void Gesture(string name)
        {
            if (runtimeGate?.Held==true||!animator || !animator.runtimeAnimatorController || (name != "Greeting" && name != "Pointing" && name != "Listening" && name != "Speaking" && name != "Idle" && name != "Walk")) return;
            StopImportedClip();
            PoseRig.SetManual(false); animator.speed=1; animator.Play(name,0,0); animator.Update(0);
            // A gesture can be sampled into a pose while authoring; live tutor activity
            // resumes when authoring ends and no saved static pose is active.
        }
        void OnApplicationPause(bool value) { paused=value; if (value) { blockedSnapshot=observedSnapshot; observedActivity=null; } if (value) { EndAmbient();gestureLayer?.Stop(); walkMotion?.Stop(); StopImportedClip(); } }
        void OnApplicationFocus(bool value) { focused=value; if (!value) { blockedSnapshot=observedSnapshot; observedActivity=null; } if (!value) { EndAmbient();gestureLayer?.Stop(); walkMotion?.Stop(); StopImportedClip(); } }
        void OnDisable() { EndAmbient();gestureLayer?.Stop(); walkMotion?.Stop(); StopImportedClip(); }
        void OnDestroy() {if(runtimeGate!=null)runtimeGate.Changed-=RuntimeChanged; EndAmbient();gestureLayer?.Dispose(); walkMotion?.Stop(); StopImportedClip(); activityMotion?.Dispose(); disposed = true; modelGeneration++; }
    }
}
