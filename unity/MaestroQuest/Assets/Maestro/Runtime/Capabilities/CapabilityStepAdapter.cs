// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    /// <summary>Private authoring projection. Generated field paths also drive the web's old simple controls.</summary>
    internal sealed class CapabilityStepAdapter
    {
        public readonly CapabilityModule Provider;
        public readonly string CapabilityId;
        readonly JObject selectors,fields;
        public CapabilityStepAdapter(string capabilityId,CapabilityModule provider,JObject selectors,params (string field,string path)[] moved) {
            CapabilityId=capabilityId;Provider=provider;this.selectors=(JObject)selectors.DeepClone();
            fields=new JObject(((JObject)provider.InputSchema["properties"]).Properties().Select(p=>new JProperty(p.Name,p.Name)));
            foreach(var pair in moved)fields[pair.field]=pair.path;
        }
        static void Put(JObject obj,string path,JToken value) {
            var parts=path.Split('.');foreach(string part in parts.Take(parts.Length-1)) {
                if(obj[part] is not JObject)obj[part]=new JObject();obj=(JObject)obj[part];
            }obj[parts[^1]]=value.DeepClone();
        }
        public bool Matches(JObject args)=>selectors.Properties().All(p=>JToken.DeepEquals(CapabilitySchema.Value(args,p.Name),p.Value));
        public JObject Public(JObject flat) {
            var args=new JObject();foreach(var field in fields.Properties())if(flat[field.Name]!=null)Put(args,(string)field.Value,flat[field.Name]);
            foreach(var selector in selectors.Properties())Put(args,selector.Name,selector.Value);return args;
        }
        public JObject Native(JObject args) {
            var flat=new JObject();foreach(var field in fields.Properties()) {var value=CapabilitySchema.Value(args,(string)field.Value);if(value!=null)flat[field.Name]=value.DeepClone();}return flat;
        }
        public JObject ToJson()=>new() {["id"]=Provider.Id,["capability"]=CapabilityId,["selectors"]=selectors.DeepClone(),["fields"]=fields.DeepClone()};
    }
}
