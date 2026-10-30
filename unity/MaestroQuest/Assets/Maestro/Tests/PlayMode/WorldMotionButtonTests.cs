// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest]public IEnumerator WorldMovementKeepsMountedButtonsPhysicalAndRoomButtonsVirtual() {
            leftAnchor.transform.SetParent(null,true);rightAnchor.transform.SetParent(null,true);
            try {
                workshop.AddButton(ButtonMount.LeftController);workshop.AddButton(ButtonMount.Room);yield return null;Physics.SyncTransforms();
                var mounted=root.GetComponentsInChildren<RuleButton>().Single(x=>((IPhysicalRoomBinding)x).PhysicalFrame);
                var placed=root.GetComponentsInChildren<RuleButton>().Single(x=>!((IPhysicalRoomBinding)x).PhysicalFrame);
                var physical=mounted.transform.position;var local=placed.transform.localPosition;var motion=new RoomWorldMotion(root.transform,physics);
                Assert.That(motion.SetPose(new Vector3(4,0,-2),Quaternion.Euler(0,60,0),out var error),Is.True,error);
                Assert.That(Vector3.Distance(mounted.GetComponent<Rigidbody>().position,physical),Is.LessThan(.0001f),"Native collider stays physical before LateUpdate");
                Assert.That(Vector3.Distance(mounted.transform.position,physical),Is.LessThan(.0001f));Assert.That(Vector3.Distance(placed.transform.localPosition,local),Is.LessThan(.0001f));
                yield return null;Assert.That(Vector3.Distance(mounted.transform.position,physical),Is.LessThan(.0001f));
            } finally {leftAnchor.transform.SetParent(root.transform,true);rightAnchor.transform.SetParent(root.transform,true);}
        }
    }
}
