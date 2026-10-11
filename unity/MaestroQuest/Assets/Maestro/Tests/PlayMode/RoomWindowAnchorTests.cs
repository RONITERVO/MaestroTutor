// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        [UnityTest]public IEnumerator PhysicalWindowKeepsExactAnchorAndHidesOnLostOrWrongRoom(){
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{WorldCoordinates=true,Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);string id=MakeLayer(out _);
            var args=new JObject{["operation"]="save",["target"]=id,["revision"]=editor.ObjectRevision(id),["window"]="Window",["surface"]="Canvas",["shape"]="ellipse",["reveal"]=.7};
            Assert.That(modeActions.Execute(SettingsRequest("object.window.edit",args),out var error),Is.True,error);var item=editor.Find(id);var view=item.GetComponent<RoomWindowView>();Assert.That(view.Requested,Is.True);
            var mask=item.GetComponentsInChildren<Renderer>().Single(RoomWindowView.IsMask);var physical=mask.transform.position;
            var motion=new RoomWorldMotion(root.transform,world);Assert.That(motion.SetPose(root.transform.position+Vector3.right,Quaternion.Euler(0,20,0),out error),Is.True,error);
            Assert.That(Vector3.Distance(mask.transform.position,physical),Is.LessThan(.001f),"Physical pose is preserved within the movement transaction");yield return null;
            Assert.That(Vector3.Distance(mask.transform.position,physical),Is.LessThan(.001f));Assert.That(editor.Read(id).windows[0].reveal,Is.EqualTo(.7f));
            string roomId=source.RoomId;source.RoomId=Guid.NewGuid().ToString("N");yield return null;yield return null;Assert.That(view.Requested,Is.False);Assert.That(item.gameObject.activeSelf,Is.False);
            source.RoomId=roomId;yield return null;yield return null;Assert.That(view.Requested,Is.True);Assert.That(editor.Read(id).windows[0].shape,Is.EqualTo("ellipse"));
            source.Entries=Array.Empty<ScannedSurface>();yield return null;yield return null;Assert.That(view.Requested,Is.False);Assert.That(editor.Read(id).windows.Length,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator FullVirtualBackdropKeepsUnderlayOnlyWhileAnOpeningRequestsIt(){
            var controls=SharedModes(out var view,out _);world.PausePhysics();
            // AR has no native subsystem in this desktop fixture. The saved enabled
            // intent can be checked, but renderingReady must remain false.
            var camera=viewer.GetComponent<Camera>();var ar=camera.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
            typeof(VirtualRoomView).GetField("passthrough",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(view,ar);
            var window=root.AddComponent<RoomWindowView>();Assert.That(ar.enabled,Is.True);Assert.That(view.SetPresentation(1,false),Is.True);Assert.That(ar.enabled,Is.False);
            view.SetWindowRequest(window,true);Assert.That(ar.enabled,Is.True);Assert.That(view.WindowRenderingReady,Is.False);Assert.That(view.BackdropOpacity,Is.EqualTo(1));
            view.SetWindowRequest(window,false);Assert.That(ar.enabled,Is.False);view.Exit();Assert.That(ar.enabled,Is.True);UnityEngine.Object.Destroy(window);yield return null;
        }
    }
}
