// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Book;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public class BookCoordinatesTests
    {
        [Test]
        public void FacingPageEdgesCannotLeakClicksOntoTheOtherPage()
        {
            Assert.AreEqual((1023, 0), BookCoordinates.ToBrowserPixel(PageSide.Left, 1, 1, 2048, 1536));
            Assert.AreEqual((1024, 1535), BookCoordinates.ToBrowserPixel(PageSide.Right, 0, 0, 2048, 1536));
            Assert.AreEqual((2047, 768), BookCoordinates.ToBrowserPixel(PageSide.Right, 1.001, .5, 2048, 1536));
        }

        [Test]
        public void InvalidCoordinatesAreRejectedBeforeNativeInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BookCoordinates.ToBrowserPixel(PageSide.Left, double.NaN, 0, 2048, 1536));
            Assert.Throws<ArgumentOutOfRangeException>(() => BookCoordinates.ToBrowserPixel(PageSide.Left, 0, 0, 2047, 1536));
        }
    }
}
