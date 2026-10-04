// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class DrawingSurfaceCapability:CapabilityModule
    {
        internal const string Feature="drawingSurfaces.v1";
        public override string Id=>"object.surface.edit";
        public override string Label=>"Edit a drawing surface";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Configure reusable plane, cylinder or sphere drawing patches on user-created objects or named recipe parts. This is an explicit configured surface, not projection onto collision boxes, imported mesh triangles, skin or the physical room. Position/rotation belong to the object or part; local X/Y are paper coordinates and the front faces local -Z. Width/height are .02–4 local metres. Use empty part for the object root. Existing marks stay in their patch coordinates; moving/reorienting a patch moves them deliberately and shrinking cannot crop them silently. enabled controls new physical/add strokes, not visibility. add supplies 2–64 planar points (z=0) and returns an exact stroke ID; empty stroke generates one. splice exposes up to 512 points per stroke in batches of 64. removeStroke, clear and removeSurface explicitly remove marks with one Undo. Four surfaces per object, 64 surfaces/128 strokes and 32768 drawing points per room including spatial strokes. Failed saves preserve ink. Temporary edits stay in the fork until Keep. Optional shape is plane (default), cylinder or sphere. Curves require curvatureRadius .01–4 local metres, width at most 2*pi*radius; spheres also require height at most .9*pi*radius to avoid poles. Coordinates are arc lengths at the equator: P=(r*sin(x/r), y, r*(1-cos(x/r))) for cylinders and P=(r*cos(y/r)*sin(x/r), r*sin(y/r), r*(1-cos(y/r)*cos(x/r))) for spheres. No seam wrapping: begin another stroke after crossing a patch edge. Plane radius is zero. Curved strokes tessellate at five-degree steps, at most 2048 rendered points each; these count against the shared room point budget. No inferred mesh UVs, skin deformation, physics or drawing-input start is added.";
        internal static JObject DefinitionSchema(){var schema=Object(new JObject {["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["position"]=DrawingData.Point(),["rotation"]=Vector(true),["width"]=Number(.02,4),["height"]=Number(.02,4),["enabled"]=new JObject {["type"]="boolean"},["shape"]=CurvedField(Choice("plane","cylinder","sphere"),"Surface shape"),["curvatureRadius"]=CurvedField(Number(0,4),"Curvature radius")},"shape","curvatureRadius");schema["format"]="drawingSurface";return schema;}
        static JObject CurvedField(JObject schema,string title){schema["title"]=title;schema["x-features"]=new JArray(DrawingSurfaceGeometry.Feature);return schema;}
        static JObject Variant(string op,string title,JObject fields=null){var props=new JObject {["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision(),["surface"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32)};props["operation"]["x-static"]=true;if(fields!=null)foreach(var p in fields.Properties())props[p.Name]=p.Value;var schema=CurrentInputs(Object(props),"object.surfaces","revision",new JObject {["target"]="target"});schema["title"]=title;schema["x-features"]=new JArray(Feature);return schema;}
        static JObject PlanarPoint()=>Object(new JObject {["x"]=Number(-2,2),["y"]=Number(-2,2),["z"]=Number(0,0)});
        static JObject Stroke(bool optional=false)=>Text(optional?"^([a-f0-9]{32})?$":"^[a-f0-9]{32}$",32);
        public override JObject InputSchema=>new() {["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            Variant("configure","Configure a drawing patch",new JObject {["definition"]=DefinitionSchema()}),
            Variant("add","Draw a surface stroke",new JObject {["stroke"]=Stroke(true),["red"]=Number(0,1),["green"]=Number(0,1),["blue"]=Number(0,1),["radius"]=Number(.001,.02),["points"]=List(PlanarPoint(),2,64)}),
            Variant("splice","Edit surface stroke points",new JObject {["stroke"]=Stroke(),["index"]=Number(0,512,true),["deleteCount"]=Number(0,512,true),["points"]=List(PlanarPoint(),0,64)}),
            Variant("removeStroke","Erase one stroke",new JObject {["stroke"]=Stroke()}),Variant("clear","Clear the surface"),Variant("removeSurface","Remove the patch and its marks"))};
        public override JObject OutputSchema=>Object(new JObject {["target"]=Text("^[a-f0-9]{32}$",32),["revision"]=Revision(),["surface"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32),["stroke"]=Stroke(true)});
        public override JObject Example=>new() {["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["surface"]="Front",["definition"]=new JObject {["part"]="",["position"]=new JObject {["x"]=0,["y"]=0,["z"]=-.066},["rotation"]=new JObject {["x"]=0,["y"]=0,["z"]=0,["w"]=1},["width"]=.12,["height"]=.12,["enabled"]=true}};
        public override bool Validate(JObject a,out string error){error="Use planar points and a normalized surface rotation";if((string)a["operation"]=="configure"){var d=JsonUtility.FromJson<DrawingSurface>(a["definition"].ToString());if(!MotionFrame.ValidRotation(d.rotation)||d.position.sqrMagnitude>100||!DrawingSurfaceGeometry.Valid(d))return false;}else if(a["points"] is JArray p&&DrawingData.Points(p).Any(v=>Mathf.Abs(v.z)>.000001f))return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.PrepareSurfaceEdit((string)a["target"],(int)a["revision"],a,out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditSurface((string)a["target"],(int)a["revision"],a,out var stroke,out error))return false;operation=new CompletedCapability(new JObject {["target"]=(string)a["target"],["revision"]=c.Editor.ObjectRevision((string)a["target"]),["surface"]=(string)a["surface"],["stroke"]=stroke});return true;}
    }
}
