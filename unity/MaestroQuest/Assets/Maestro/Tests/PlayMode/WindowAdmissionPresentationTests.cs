// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        [UnityTest]public IEnumerator WindowAdmissionPrivateAndHiddenMasksNeverEnablePassthrough(){
            SharedModes(out var presentation,out _);world.PausePhysics();var ar=viewer.GetComponent<Camera>().gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();typeof(VirtualRoomView).GetField("passthrough",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(presentation,ar);Assert.That(presentation.SetPresentation(1,false),Is.True);Assert.That(ar.enabled,Is.False);
            var host=new GameObject("Prepared opening");host.transform.SetParent(root.transform,false);var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),surfaces=new[]{new DrawingSurface{id="Front"}},windows=new[]{new RoomWindow{surface="Front"}}};host.AddComponent<DrawingSurfaceView>().Apply(data.surfaces);var view=host.AddComponent<RoomWindowView>();
            using(var candidate=new PreparedRoomWindows(editor.WorldIdentity,data,view)){
                yield return null;Assert.That(presentation.WindowRequests,Is.Zero);Assert.That(ar.enabled,Is.False);Assert.That(view.Requested,Is.False);
                var hidden=new VisibilityState();hidden.Set(0,false);view.Apply(data,presentation,hidden,candidate);Assert.That(presentation.WindowRequests,Is.Zero);Assert.That(ar.enabled,Is.False,"Apply must use its accepted visibility before requesting passthrough");Assert.That(view.RenderingReady,Is.False);
                hidden.Set(1,false);view.Refresh();Assert.That(presentation.WindowRequests,Is.EqualTo(1));Assert.That(ar.enabled,Is.True);Assert.That(view.RenderingReady,Is.False,"No desktop subsystem is fabricated");host.SetActive(false);Assert.That(presentation.WindowRequests,Is.Zero);Assert.That(ar.enabled,Is.False);host.SetActive(true);Assert.That(presentation.WindowRequests,Is.EqualTo(1));
                var removed=data.Copy();removed.windows=Array.Empty<RoomWindow>();using var removal=new PreparedRoomWindows(editor.WorldIdentity,removed,view);view.Apply(removed,presentation,hidden,removal);Assert.That(presentation.WindowRequests,Is.Zero);Assert.That(ar.enabled,Is.False);
            }
            NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);yield return null;
        }
    }
}
