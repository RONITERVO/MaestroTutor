// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Imports;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class SurfaceImageTests {
        [Test] public void InspectsRealEncodedPngAndJpegWithoutDecoding(){foreach(var bytes in new[]{ImageFiles.Png(),ImageFiles.Jpeg()}){var info=SurfaceImage.Inspect(bytes);Assert.AreEqual(3,info.Width);Assert.AreEqual(2,info.Height);Assert.AreEqual(1,info.Orientation);Assert.AreEqual(28,info.TextureBytes);}}
        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)]
        public void PhotoOrientationIsExplicitAndRemovedFromDecodeCopy(int orientation){foreach(bool png in new[]{false,true}){var original=png?ImageFiles.Png():ImageFiles.Jpeg();var bytes=png?ImageFiles.OrientPng(original,orientation):ImageFiles.OrientJpeg(original,orientation);var info=SurfaceImage.Inspect(bytes);Assert.AreEqual(orientation,info.Orientation);CollectionAssert.AreEqual(original,SurfaceImage.DecodeBytes(bytes,info));Assert.AreEqual(orientation>=5?2:3,info.DisplayWidth);var mapped=Enumerable.Range(0,6).Select(i=>SurfaceImage.Upright(i%3,i/3,3,2,orientation)).ToArray();Assert.AreEqual(6,mapped.Distinct().Count());Assert.IsTrue(mapped.All(p=>p.X>=0&&p.X<info.DisplayWidth&&p.Y>=0&&p.Y<info.DisplayHeight));}}
        [Test] public void RejectsTruncationDamageHugeDimensionsAnimationAndMalformedExif(){var png=ImageFiles.Png();Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(png.Take(png.Length-1).ToArray()));var damaged=(byte[])png.Clone();damaged[40]^=1;Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(damaged));var ihdr=png.Skip(16).Take(13).ToArray();ihdr[0]=0;ihdr[1]=1;ihdr[2]=0;ihdr[3]=0;var huge=png.Take(8).Concat(ImageFiles.PngChunk("IHDR",ihdr)).Concat(png.Skip(33)).ToArray();Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(huge));var animated=png.Take(33).Concat(ImageFiles.PngChunk("acTL",new byte[8])).Concat(png.Skip(33)).ToArray();Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(animated));Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(ImageFiles.OrientJpeg(ImageFiles.Jpeg(),9)));Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(ImageFiles.Jpeg().Concat(new byte[]{0}).ToArray()));}
        [Test] public void CancellationAndUnsupportedInputsNeverReachDecoder(){using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Catch<OperationCanceledException>(()=>SurfaceImage.Inspect(ImageFiles.Png(),cancel.Token));Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(new byte[40]));Assert.Throws<InvalidDataException>(()=>SurfaceImage.Inspect(new byte[SurfaceImage.MaximumBytes+1]));}
    }
}
