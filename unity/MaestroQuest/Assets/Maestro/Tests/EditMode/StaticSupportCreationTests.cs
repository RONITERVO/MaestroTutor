// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class StaticSupportCreationTests
    {
        static CreationTemplates.Entry Template(string id)=>CreationTemplates.All.Single(t=>t.Id==id);
        static CreationPiece Piece(string id,int x)=>new(){slot="Slot"+x,name="",position=Vector3.right*x,source=new(){kind="template",templateHash=Template(id).Hash}};
        [Test] public void SupportStartersUseMatchingFixedPrimaryBoxesWithinExistingStepAndLibraryLimits()
        {
            Assert.That(CreationTemplates.All.Count,Is.EqualTo(28));Assert.That(CreationTemplates.All.Count,Is.LessThanOrEqualTo(32));
            foreach(var entry in CreationTemplates.All){bool support=entry.Id is "platform" or "stairs";Assert.That(entry.StaticSupport.enabled,Is.EqualTo(support),entry.Id);entry.StaticSupport.enabled=!support;Assert.That(entry.StaticSupport.enabled,Is.EqualTo(support));if(!support)continue;
                Assert.That(entry.Physics.mode,Is.EqualTo("fixed"));Assert.That(entry.Physics.shape,Is.EqualTo("automatic"));Assert.That(entry.Recipe.playing,Is.False);Assert.That(entry.Recipe.tracks,Is.Empty);
                Assert.That(entry.Recipe.parts.Length,Is.EqualTo(entry.Collision.shapes.Length));
                foreach(var pair in entry.Recipe.parts.Zip(entry.Collision.shapes,(visual,solid)=>(visual,solid))){Assert.That(pair.visual.shape,Is.EqualTo("box"));Assert.That(pair.solid.shape,Is.EqualTo("box"));Assert.That(pair.visual.position,Is.EqualTo(pair.solid.position));Assert.That(pair.visual.size,Is.EqualTo(pair.solid.size));Assert.That(pair.visual.rotation,Is.EqualTo(pair.solid.rotation));}
                Assert.That(RoomEditor.PrepareRecipeObject(entry.Name,Vector3.zero,1,entry.Recipe,entry.Collision,entry.Physics,out var data,out var error,staticSupport:entry.StaticSupport),Is.True,error);Assert.That(RoomStaticSupport.Validate(data,out error),Is.True,error);
            }
            float previous=0;foreach(var tread in Template("stairs").Collision.shapes.OrderBy(s=>s.position.z)){float top=tread.position.y+tread.size.y*.5f;Assert.That((top-previous)*4,Is.InRange(.0001f,.100001f),"Rise stays within 10 cm throughout allowed scale 0.1–4; this does not promise route reachability");previous=top;}
        }
        [Test] public void BatchAndSinglePreparationCarryExplicitIntentWithoutDerivingItFromNames()
        {
            var batch=new CreationBatch{blueprint=new(){pieces=new[]{Piece("platform",0),Piece("stairs",1),Piece("brick",2)}}};batch.blueprint.pieces[2].name="Walkable platform stairs";
            Assert.That(batch.Prepare(out var first,out var error),Is.True,error);Assert.That(batch.Prepare(out var second,out error),Is.True,error);
            Assert.That(first.Select(o=>o.staticSupport.enabled),Is.EqualTo(new[]{true,true,false}));first[0].staticSupport.enabled=false;Assert.That(second[0].staticSupport.enabled,Is.True);Assert.That(Template("platform").StaticSupport.enabled,Is.True);
            var entry=Template("platform");Assert.That(RoomEditor.PrepareRecipeObject("Platform",Vector3.zero,1,entry.Recipe,entry.Collision,entry.Physics,out var absent,out error),Is.True,error);Assert.That(absent.staticSupport.Default,Is.True);
        }
        [Test] public void InlineRecipeAndBatchSchemasShareSupportCompatibilityChecksAndOmissionDefaultsOff()
        {
            var entry=Template("platform");var args=(JObject)new CreateRecipeCapability().Example.DeepClone();args["recipe"]=entry.Source["definition"]["recipe"];args["collision"]=entry.Source["definition"]["collision"];args["physics"]=entry.Source["definition"]["physics"];args["staticSupport"]=new JObject{["version"]=1,["enabled"]=true};
            Assert.That(CapabilityArguments.Validate(args,new CreateRecipeCapability().InputSchema,out var error),Is.True,error);
            args["physics"]["mode"]="solid";Assert.That(CapabilityArguments.Validate(args,new CreateRecipeCapability().InputSchema,out _),Is.False);args["physics"]["mode"]="fixed";args["physics"]["shape"]="sphere";Assert.That(CapabilityArguments.Validate(args,new CreateRecipeCapability().InputSchema,out _),Is.False);args["physics"]["shape"]="automatic";
            var batch=(JObject)new BatchCreationCapability().Example.DeepClone();batch["blueprint"]["pieces"]=new JArray(((JArray)batch["blueprint"]["pieces"]).Take(1));var source=new JObject{["kind"]="recipe",["recipe"]=args["recipe"].DeepClone(),["collision"]=args["collision"].DeepClone(),["physics"]=args["physics"].DeepClone(),["staticSupport"]=args["staticSupport"].DeepClone()};batch["blueprint"]["pieces"][0]["source"]=source;
            Assert.That(CapabilityArguments.Validate(batch,new BatchCreationCapability().InputSchema,out error),Is.True,error);Assert.That(CreationBatch.Read(batch).Prepare(out var values,out error),Is.True,error);Assert.That(values[0].staticSupport.enabled,Is.True);
            source["physics"]["mode"]="solid";Assert.That(CapabilityArguments.Validate(batch,new BatchCreationCapability().InputSchema,out _),Is.False);source.Remove("staticSupport");Assert.That(CapabilityArguments.Validate(batch,new BatchCreationCapability().InputSchema,out error),Is.True,error);Assert.That(CreationBatch.Read(batch).Prepare(out values,out error),Is.True,error);Assert.That(values[0].staticSupport.Default,Is.True);
            args.Remove("staticSupport");Assert.That(CapabilityArguments.Validate(args,new CreateRecipeCapability().InputSchema,out error),Is.True,error);
        }
        [Test] public void BundledSupportDefinitionsRefuseMalformedComponentsAndIncompatiblePhysics()
        {
            var source=Template("platform").Source;source["definition"]["physics"]["mode"]="solid";Assert.Throws<InvalidOperationException>(()=>new CreationTemplates.Entry(Encoding.UTF8.GetBytes(source.ToString())));
            source=Template("platform").Source;source["definition"]["staticSupport"]["future"]=true;Assert.Throws<InvalidOperationException>(()=>new CreationTemplates.Entry(Encoding.UTF8.GetBytes(source.ToString())));
        }
    }
}
