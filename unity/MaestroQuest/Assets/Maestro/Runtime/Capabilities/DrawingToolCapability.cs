// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class DrawingToolCapability:CapabilityModule
    {
        public override string Id=>"drawing.tool.set";
        public override string Label=>"Choose the physical pencil";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"drawing.idle","room.active"};
        public override string Description=>"Select off, free-space pencil or flat-surface pencil or surfaceErase, RGB ink and local radius for the next physical trigger/pinch stroke. Surface mode only draws on explicitly configured enabled patches within 25 cm and never falls back to free-space ink. surfaceErase removes the nearest complete stroke within twice the local radius (minimum 1 cm) per tap, with one Undo. It never deletes the object. New captures require releasing the surface object first. No marks are created, targets moved or behaviours started by selecting the tool. Tool preferences are session-local; saved stroke colour/thickness do not change. Active and retained captures must finish or be discarded before switching. The tray uses the same configuration path.";
        public override JObject InputSchema {get {var s=Object(new JObject {["mode"]=Choice("off","space","surface","surfaceErase"),["red"]=Number(0,1),["green"]=Number(0,1),["blue"]=Number(0,1),["radius"]=Number(.001,.02)});s["x-features"]=new JArray(DrawingSurfaceCapability.Feature);return s;}}
        public override JObject Example=>new() {["mode"]="surface",["red"]=1,["green"]=1,["blue"]=1,["radius"]=.003};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.CanConfigureDrawing(out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.ConfigureDrawing((string)a["mode"],new Color((float)a["red"],(float)a["green"],(float)a["blue"]),(float)a["radius"],out error))return false;operation=new CompletedCapability();return true;}
        public static BehaviourCatalog.FactDefinition Fact()=>new("drawing.tool",ProgramDataType.Read(JObject.Parse("{\"record\":{\"mode\":\"text\",\"red\":\"number\",\"green\":\"number\",\"blue\":\"number\",\"radius\":\"number\"}}")),"Current physical pencil","Session-local mode and next-stroke ink, shared with the physical tray. Reads do not capture geometry, change colour or enable input.",null,null,(c,a)=>!c.Editor?null:ProgramValue.Literal(new JObject {["mode"]=!c.Editor.DrawingMode?"off":c.Editor.SurfaceErasing?"surfaceErase":c.Editor.DrawingOnSurfaces?"surface":"space",["red"]=c.Editor.Paint.r,["green"]=c.Editor.Paint.g,["blue"]=c.Editor.Paint.b,["radius"]=c.Editor.DrawingRadius}),features:new[]{DrawingSurfaceCapability.Feature});
    }
}
