// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed class HeadsetBookCameraLifecycleTests {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator ComponentActivationNeverSelectsACameraAndUnsupportedCaptureFailsClosed(){
            root=new GameObject("Unselected headset camera");
            var component=root.AddComponent<HeadsetBookCamera>();
            IBookCameraFeed feed=component;
            yield return null; // Run actual Unity lifecycle dispatch, including its Start lookup.
            Assert.That(feed.SourceId,Is.EqualTo(BookCameraSession.HeadsetSource));
            Assert.That(feed.Available,Is.False,"Desktop Editor must not pretend to expose a Quest sensor");
            Assert.That(feed.Frame(out var image,out var error),Is.False);
            Assert.That(image,Is.Null);Assert.That(error,Is.Null);
            Assert.That(root.transform.childCount,Is.Zero,"Activation cannot create a sensor without source selection");
            Assert.That(feed.StartCapture(out error),Is.False);Assert.That(error,Is.EqualTo("camera-unavailable"));
            Assert.That(root.transform.childCount,Is.Zero);
            component.enabled=false;yield return null;component.enabled=true;yield return null;
            root.SetActive(false);yield return null;root.SetActive(true);yield return null;
            root.SendMessage("OnApplicationPause",true);root.SendMessage("OnApplicationPause",false);
            root.SendMessage("OnApplicationFocus",false);root.SendMessage("OnApplicationFocus",true);
            yield return null;
            Assert.That(feed.Frame(out image,out error),Is.False);Assert.That(image,Is.Null);Assert.That(error,Is.Null);
            Assert.That(root.transform.childCount,Is.Zero,"Enable, resume and focus cannot implicitly select a camera");
            Assert.That(feed.StartCapture(out error),Is.False);Assert.That(error,Is.EqualTo("camera-unavailable"));
            feed.Stop();feed.Stop();LogAssert.NoUnexpectedReceived();
        }
    }
}
