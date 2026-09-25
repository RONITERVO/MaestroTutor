// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class GestureOwnershipTests
    {
        [Test]
        public void PinchCannotChangeFromPageToObjectBeforeRelease()
        {
            var gesture = new GestureOwnership();
            gesture.Update(false, GestureTarget.Page);
            Assert.That(gesture.Update(true, GestureTarget.Page), Is.EqualTo(GestureTarget.Page));
            Assert.That(gesture.Update(true, GestureTarget.Object), Is.EqualTo(GestureTarget.Page));
            gesture.Update(false, GestureTarget.Object);
            Assert.That(gesture.Update(true, GestureTarget.Object), Is.EqualTo(GestureTarget.Object));
        }

        [Test]
        public void RecoveringTrackingWhilePinchingDoesNotActivateAnything()
        {
            var gesture = new GestureOwnership();
            gesture.Update(false, GestureTarget.Object);
            gesture.Update(true, GestureTarget.Object);
            gesture.Cancel();
            Assert.That(gesture.Update(true, GestureTarget.Page), Is.EqualTo(GestureTarget.None));
            gesture.Update(false, GestureTarget.Page);
            Assert.That(gesture.Update(true, GestureTarget.Page), Is.EqualTo(GestureTarget.Page));
        }

        [Test]
        public void PinchingEmptySpaceThenPointingAtPageDoesNotClick()
        {
            var gesture = new GestureOwnership();
            gesture.Update(false, GestureTarget.None);
            gesture.Update(true, GestureTarget.None);
            Assert.That(gesture.Update(true, GestureTarget.Page), Is.EqualTo(GestureTarget.None));
        }
    }
}
