// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    /// <summary>Detached views used by the existing tray controls. New modules need no numeric identity.</summary>
    public static class LegacyCapabilityAdapters
    {
        static readonly IReadOnlyDictionary<RuleActionKind,string> ids=new Dictionary<RuleActionKind,string> {
            [RuleActionKind.RecordedAnimation]="animation.recording.play",
            [RuleActionKind.Gesture]="avatar.gesture.play",
            [RuleActionKind.UpperBodyGesture]="avatar.gesture.upperBody",
            [RuleActionKind.Wait]="time.wait",
            [RuleActionKind.CreatePrimitive]="object.create.primitive",
            [RuleActionKind.CreateRecipe]="object.create.recipe",
            [RuleActionKind.MoveObject]="object.position.set",
            [RuleActionKind.ResizeObject]="object.scale.set",
            [RuleActionKind.PaintObject]="object.color.set",
            [RuleActionKind.DeleteObject]="object.delete",
            [RuleActionKind.PhysicsImpulse]="object.physics.impulse",
            [RuleActionKind.PhysicsStop]="object.physics.stop",
            [RuleActionKind.ThrowRecording]="object.recording.throw",
            [RuleActionKind.LookAtUser]="avatar.look.user",
            [RuleActionKind.FollowUser]="avatar.follow.user",
            [RuleActionKind.ImportedClip]="animation.embedded.play",
            [RuleActionKind.LibraryMotion]="animation.library.play",
            [RuleActionKind.RecipeAnimation]="animation.recipe.play",
        };
        public static string Id(RuleActionKind kind)=>ids.TryGetValue(kind,out var id)?id:null;
        public static RuleActionKind? Kind(string id) {foreach(var pair in ids)if(pair.Value==id)return pair.Key;return null;}
        public static string[] ActionIds=>ids.OrderBy(x=>(int)x.Key).Select(x=>x.Value).ToArray();
        public static bool TryStep(this CapabilityCall call,out RuleStep step,out string error) {
            step=null;error="Use the book's capability blocks for this action";
            var source=call.Definition.Id=="animation.play"?AnimationPlayCapability.Find(call.Arguments):null;
            var kind=LegacyCapabilityAdapters.Kind(source?.Provider.Id??call.Definition.Id);
            if(!kind.HasValue||!CapabilityArguments.TryStep(kind.Value,source==null?call.Arguments:source.Native(call.Arguments),out step,out error))return false;
            step.id=call.NodeId;return true;
        }
        public static bool TryCall(RuleStep step,out CapabilityCall call,out string error) {
            call=null;if(!RuleDocument.ValidStep(step,out error))return false;
            var source=AnimationPlayCapability.Legacy(Id(step.action));var args=CapabilityArguments.FromStep(step);
            if(!BehaviourCatalog.TryCall(source==null?Id(step.action):"animation.play",1,source==null?args:source.Public(args),out call,out error))return false;
            call.NodeId=step.id;return true;
        }
    }
}
