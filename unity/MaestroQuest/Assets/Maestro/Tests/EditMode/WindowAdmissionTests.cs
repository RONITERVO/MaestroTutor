// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WindowAdmissionTests
    {
        [Test]public void WindowAdmissionHasIndependentMaskAndBundleLimitsAcrossWorlds(){
            string before=WindowResources.ObserveBudget().ToString();var held=new List<WindowResources.Lease>();var first=new RoomResourceOwner(RoomWorldIdentity.Create(),"first","window");var second=new RoomResourceOwner(RoomWorldIdentity.Create(),"second","window");
            try{
                for(int i=0;i<WindowResources.MaximumMasks;i++)held.Add(WindowResources.Mask(i%2==0?first:second,"Window"));
                Assert.Throws<RoomResourceLimitException>(()=>WindowResources.Mask(second,"Overflow"));Assert.That((int)WindowResources.ObserveBudget()["bundles"],Is.Zero);
                for(int i=0;i<WindowResources.MaximumBundles;i++)held.Add(WindowResources.Bundle(i%2==0?first:second));
                Assert.Throws<RoomResourceLimitException>(()=>WindowResources.Bundle(second));held[0].Dispose();held[0].Dispose();using var retry=WindowResources.Mask(first,"Retry");Assert.That((int)WindowResources.ObserveBudget()["masks"],Is.EqualTo(32));Assert.That((int)WindowResources.ObserveBudget()["bundles"],Is.EqualTo(32));
            }finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(WindowResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        static RoomObjectData Data()=>new(){id=Guid.NewGuid().ToString("N"),surfaces=new[]{new DrawingSurface{id="Front"}},windows=new[]{new RoomWindow{surface="Front"}}};
        [Test]public void WindowAdmissionNeverActiveOwnerReleasesSharedNativeGeometry(){
            string before=WindowResources.ObserveBudget().ToString();var host=new GameObject("never active window");host.SetActive(false);var data=Data();host.AddComponent<DrawingSurfaceView>().Apply(data.surfaces);var view=host.AddComponent<RoomWindowView>();
            using(var candidate=new PreparedRoomWindows(null,data,view))view.Apply(data,null,null,candidate);
            var bundle=view.ReusableBundle;var mesh=bundle.Mesh;var material=bundle.Material;UnityEngine.Object.DestroyImmediate(host);WindowResources.Collect();
            Assert.That(!mesh&&!material,Is.True);Assert.That(WindowResources.ObserveBudget().ToString(),Is.EqualTo(before));InkResources.Collect();
        }
        [Test]public void WindowAdmissionRequiresTheExactOwnerSourceAndSurvivingAnchor(){
            string before=WindowResources.ObserveBudget().ToString();var host=new GameObject("window source");var other=new GameObject("other owner");
            try{
                var data=Data();var surfaces=host.AddComponent<DrawingSurfaceView>();surfaces.Apply(data.surfaces);var view=host.AddComponent<RoomWindowView>();var otherView=other.AddComponent<RoomWindowView>();
                using var candidate=new PreparedRoomWindows(null,data,view);
                Assert.Throws<InvalidOperationException>(()=>candidate.Take(otherView,data));var changed=data.Copy();changed.surfaces[0].width=.7f;Assert.Throws<InvalidOperationException>(()=>candidate.Validate(changed));changed=data.Copy();changed.windows[0].reveal=.3f;Assert.Throws<InvalidOperationException>(()=>candidate.Validate(changed));changed=data.Copy();changed.id=Guid.NewGuid().ToString("N");Assert.Throws<InvalidOperationException>(()=>candidate.Validate(changed));
                UnityEngine.Object.DestroyImmediate(surfaces.Surface("Front").gameObject);Assert.Throws<InvalidOperationException>(()=>candidate.Take(view,data));Assert.That(view.Requested,Is.False);
            }finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(other);}
            Assert.That(WindowResources.ObserveBudget().ToString(),Is.EqualTo(before));InkResources.Collect();
        }
    }
}
