// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Creation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UniGLTF;
namespace Maestro.Quest.Editor
{
    /// <summary>Fail packaging before an invalid or unpaired included avatar reaches an APK.</summary>
    public sealed class QuestIncludedAvatar:IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)=>Validate();
        public static void Validate()
        {
            var included=BundledAvatar.FromApplication();
            bool payload=File.Exists(Path.Combine(Application.streamingAssetsPath,BundledAvatar.RelativePath));
            if(included==null){if(payload)throw new BuildFailedException("Included avatar payload has no manifest.");return;}
            var asset=included.Read();var root=new GameObject("Included avatar validation");
            try {
                var canonical=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Avatars/DefaultMaestro"),root.transform);
                if(!canonical.TryGetComponent<Animator>(out var animator))animator=canonical.AddComponent<Animator>();animator.enabled=false;
                var rig=root.AddComponent<AvatarPoseRig>();rig.Initialize(animator);
                var modelRoot=new GameObject("Included candidate");modelRoot.transform.SetParent(root.transform,false);
                var model=modelRoot.AddComponent<ImportedModel>();model.LoadAsync(asset,new ImmediateCaller()).GetAwaiter().GetResult();model.FitAsMaestro();
                var retargeter=modelRoot.AddComponent<HumanoidRetargeter>();retargeter.Initialize(rig,model.Humanoid);retargeter.ApplyPose();
                if(included.WalkClipIndex>=0&&(included.WalkClipIndex>=model.ClipCount||model.ClipDuration(included.WalkClipIndex)<.1f))throw new BuildFailedException("The declared included walk clip is unavailable.");
                if(!model.Ready||!model.IsHumanoid||rig.Capture().Length!=17||!ModelLibrary.ValidHash(model.MotionRigHash)||model.IsPlaying)
                    throw new BuildFailedException("Included avatar must support the canonical pose rig without starting playback.");
                var motions=BundledMotions.FromApplication();
                if(motions==null||motions.AvatarHash!=included.Hash||motions.RigHash!=model.MotionRigHash)throw new BuildFailedException("Included animations must match the exact shipped avatar and rig.");
                motions.Verify();Debug.Log("MAESTRO_INCLUDED_MOTIONS_VERIFIED manifest="+motions.Hash+" motions="+motions.Count+" bytes="+motions.Bytes);
                Debug.Log("MAESTRO_INCLUDED_AVATAR_VERIFIED sha256="+asset.Hash+" rig="+model.MotionRigHash+" vertices="+asset.Inspection.Vertices+" triangles="+asset.Inspection.Triangles+" pixels="+asset.Inspection.TexturePixels+" clips="+model.ClipCount);
            } finally {UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
