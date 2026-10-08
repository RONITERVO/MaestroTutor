// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class CreationResourcesSchema
    {
        internal const string Feature="constructionResources.v1";
        internal static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        static JObject Id()=>Text("^[a-f0-9]{32}$",32);
        internal static JObject Bindings()=>Featured(List(AppearanceCapability.BindingSchema(),0,33));
        internal static JObject Emitters()=>Featured(List(Object(new JObject{
            ["version"]=Number(1,1,true),["id"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24),["source"]=Id(),
            ["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["joint"]=Choice(""),["role"]=Choice("effects","media","ambience"),
            ["position"]=Object(new JObject{["x"]=Number(-10,10),["y"]=Number(-10,10),["z"]=Number(-10,10)}),
            ["spatial"]=new JObject{["type"]="boolean"},["gain"]=Number(0,1),["minDistance"]=Number(.1,10),["maxDistance"]=Number(.1,50)}),0,4));
        internal static JObject Schema() {var fields=new JObject{
            ["version"]=Number(1,2,true),
            ["appearances"]=List(Object(new JObject{["version"]=Number(1,1,true),["id"]=Id(),["name"]=Text("^.{1,80}$",80),["style"]=AppearanceCapability.StyleSchema()}),0,64),
            ["audioSources"]=List(Object(new JObject{["version"]=Number(1,1,true),["id"]=Id(),["name"]=Text("^.{0,80}$",80),["kind"]=Choice("tone"),["wave"]=Choice("sine","triangle","noise"),
                ["frequency"]=Number(20,10000),["endFrequency"]=Number(20,10000),["seconds"]=Number(.03,30),["attack"]=Number(.005,2),["release"]=Number(.005,2),["seed"]=Number(1,int.MaxValue,true)}),0,32),
            ["environmentProfiles"]=List(Object(new JObject{["version"]=Number(1,1,true),["id"]=Id(),["name"]=Text("^.{1,80}$",80),["realCollisions"]=new JObject{["type"]="boolean"}}),0,16)};
            fields["visibilityLayers"]=VisibilityLayerCapability.Featured(List(Object(new JObject{["version"]=Number(1,1,true),["id"]=Id(),["name"]=Text("^.{1,80}$",80),["opacity"]=Number(0,1),["realDepth"]=new JObject{["type"]="boolean"}}),0,16));
            return Featured(Object(fields,"visibilityLayers"));
        }
        internal static JObject Encode(Creation.CreationResources resources) {var value=JObject.Parse(UnityEngine.JsonUtility.ToJson(resources));if(resources.version==1)value.Remove("visibilityLayers");return value;}
    }
}
