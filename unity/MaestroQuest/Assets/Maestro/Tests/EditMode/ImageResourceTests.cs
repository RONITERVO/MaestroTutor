// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ImageResourceTests
    {
        static RoomResourceOwner Owner()=>new(RoomWorldIdentity.Create(),null,"preview");
        static SurfaceImage.Info Info(int size=1,int orientation=1)=>new(){Format="png",Width=size,Height=size,Orientation=orientation};
        [Test] public void ImagePayloadCapacityRetainsCancelledWorkUntilItDrains(){
            var before=ImageResources.ObserveBudget().ToString();var held=new List<ImageResources.PayloadLease>();var task=new TaskCompletionSource<bool>();
            try{
                for(int i=0;i<ImageResources.MaximumPayloadReads;i++)held.Add(ImageResources.BeginPayload(Owner(),SurfaceImage.MaximumBytes));
                held[0].Hold(task.Task);held[0].Dispose();Assert.Throws<RoomResourceLimitException>(()=>ImageResources.BeginPayload(Owner(),1));
                Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(4));task.SetResult(true);using var retry=ImageResources.BeginPayload(Owner(),1);Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(4));
            }finally{task.TrySetResult(true);foreach(var lease in held)lease.Dispose();}
            Assert.That(ImageResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test] public void ImagePayloadByteLimitIsIndependentOfReadCount(){
            var before=ImageResources.ObserveBudget().ToString();using(var first=ImageResources.BeginPayload(Owner(),2L*SurfaceImage.MaximumBytes))using(var second=ImageResources.BeginPayload(Owner(),2L*SurfaceImage.MaximumBytes)){
                Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(2));Assert.Throws<RoomResourceLimitException>(()=>ImageResources.BeginPayload(Owner(),1));first.Dispose();using var retry=ImageResources.BeginPayload(Owner(),SurfaceImage.MaximumBytes);
            }Assert.That(ImageResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test] public void ImageTextureEntryLimitAndCancelledCandidatesReleaseExactly(){
            var before=ImageResources.ObserveBudget().ToString();var held=new List<ImageResources.TextureLease>();
            try{
                for(int i=0;i<ImageResources.MaximumTextureInstances;i++)held.Add(ImageResources.BeginTexture(Info(),24,Owner(),new string('a',64)));
                Assert.Throws<RoomResourceLimitException>(()=>ImageResources.BeginTexture(Info(),24,Owner(),new string('a',64)));held[0].Dispose();held[0].Dispose();using var retry=ImageResources.BeginTexture(Info(),24,Owner(),new string('a',64));Assert.That((int)ImageResources.ObserveBudget()["textureInstances"],Is.EqualTo(128));
            }finally{foreach(var lease in held)lease.Dispose();}Assert.That(ImageResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test] public void ImageOrientationScratchHasAnIndependentLimitBeforeAllocation(){
            var info=Info(2048,6);info.Metadata.Add((33,38));var before=ImageResources.ObserveBudget().ToString();
            using(var first=ImageResources.BeginTexture(info,SurfaceImage.MaximumBytes,Owner(),new string('b',64))){
                var used=ImageResources.ObserveBudget();Assert.That((long)used["textureBytes"]*2,Is.LessThan(ImageResources.MaximumTextureBytes));Assert.That((long)used["decodeScratchBytes"]*2,Is.GreaterThan(ImageResources.MaximumScratchBytes));
                Assert.Throws<RoomResourceLimitException>(()=>ImageResources.BeginTexture(info,SurfaceImage.MaximumBytes,Owner(),new string('c',64)));Assert.That(ImageResources.ObserveBudget().ToString(),Is.EqualTo(used.ToString()));
            }Assert.That(ImageResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test] public void ImageSourceCostsAreWideAndInvalidAdmissionCannotConsumeCapacity(){
            var before=ImageResources.ObserveBudget().ToString();Assert.That(ImageResources.TextureCost(2048,2048),Is.EqualTo(22369620));Assert.That(ImageResources.TextureCost(int.MaxValue,int.MaxValue),Is.EqualTo(long.MaxValue));
            Assert.Throws<ArgumentException>(()=>ImageResources.BeginTexture(Info(int.MaxValue),24,Owner(),new string('a',64)));Assert.Throws<ArgumentException>(()=>ImageResources.BeginPayload(new RoomResourceOwner(null,null,"object"),1));
            Assert.Throws<ArgumentException>(()=>ImageResources.BeginPayload(Owner(),2L*SurfaceImage.MaximumBytes+1));Assert.That(ImageResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
    }
}
