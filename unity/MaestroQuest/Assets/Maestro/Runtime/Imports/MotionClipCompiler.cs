// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Newtonsoft.Json.Linq;
using UniGLTF;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    /// <summary>Creates only a clip. No model, renderer, texture or duplicate rig is instantiated.</summary>
    public static class MotionClipCompiler
    {
        public static async Task<AnimationClip> CompileAsync(MotionPack pack,Func<bool> cancelled = null,IAwaitCaller awaitCaller = null)
        {
            var clip = new AnimationClip { name = "Library motion",legacy = true,wrapMode = WrapMode.Once };
            var caller = awaitCaller ?? new RuntimeOnlyAwaitCaller();
            try
            {
                var root = pack.Document; var nodes = (JArray)root["nodes"];
                var parents = Enumerable.Repeat(-1,nodes.Count).ToArray();
                for (int i=0;i<nodes.Count;i++) foreach (int child in (JArray)nodes[i]["children"] ?? new JArray()) parents[child] = i;
                string Path(int node) { string path = (string)nodes[node]["name"]; for (int p=parents[node];p>=0;p=parents[p]) path = (string)nodes[p]["name"]+"/"+path; return path; }
                var animation = root["animations"][0]; var inverter = (pack.ReverseX ? Axes.X : Axes.Z).Create();
                // Unity's quaternion continuity pass also adjusts the curve cache.
                // Match the existing importer for linear/step rotations, then add
                // cubic rotations last so that pass cannot rewrite their tangents.
                var channels = ((JArray)animation["channels"]).OrderBy(channel => (string)channel["target"]["path"] == "rotation" && (string)animation["samplers"][(int)channel["sampler"]]["interpolation"] == "CUBICSPLINE" ? 1 : 0);
                foreach (var channel in channels)
                {
                    if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                    int node = (int)channel["target"]["node"]; string path = (string)channel["target"]["path"];
                    var sampler = animation["samplers"][(int)channel["sampler"]];
                    var times = pack.Values((int)sampler["input"]); var values = pack.Values((int)sampler["output"]);
                    string interpolation = (string)sampler["interpolation"] ?? "LINEAR"; bool cubic = interpolation == "CUBICSPLINE";
                    string[] properties; Type type = typeof(Transform);
                    if (path == "weights")
                    {
                        properties = ((JArray)root["meshes"][(int)nodes[node]["mesh"]]["extras"]["targetNames"]).Select(x => "blendShape."+(string)x).ToArray();
                        type = typeof(SkinnedMeshRenderer); for (int i=0;i<values.Length;i++) values[i] *= 100;
                    }
                    else if (path == "rotation")
                    {
                        properties = new[] { "localRotation.x","localRotation.y","localRotation.z","localRotation.w" };
                        var last = Quaternion.identity;
                        for (int i=0;i<values.Length;i+=4)
                        {
                            var value = new Quaternion(values[i],values[i+1],values[i+2],values[i+3]);
                            // UniGLTF's quaternion inverter goes through angle/axis,
                            // which normalizes and cannot transform derivative vectors.
                            // Reflection is linear on quaternion components and tangents.
                            if (cubic) value = pack.ReverseX ? new Quaternion(value.x,-value.y,-value.z,value.w) : new Quaternion(-value.x,-value.y,value.z,value.w);
                            else value = inverter.InvertQuaternion(value);
                            // Cubic tangents undergo the same axis conversion as values. Do
                            // not flip individual cubic values independently of their tangents.
                            if (!cubic) { value = AnimationImporterUtil.GetShortest(last,value); last = value; }
                            values[i] = value.x; values[i+1] = value.y; values[i+2] = value.z; values[i+3] = value.w;
                        }
                    }
                    else
                    {
                        string property = path == "translation" ? "localPosition" : "localScale";
                        properties = new[] { property+".x",property+".y",property+".z" };
                        if (path == "translation") for (int i=0;i<values.Length;i+=3)
                        {
                            var value = inverter.InvertVector3(new Vector3(values[i],values[i+1],values[i+2]));
                            values[i] = value.x; values[i+1] = value.y; values[i+2] = value.z;
                        }
                    }
                    AnimationImporterUtil.SetAnimationCurve(clip,Path(node),properties,times,values,interpolation,type,(current,last) => current);
                    if (path == "rotation" && !cubic) clip.EnsureQuaternionContinuity();
                    await caller.NextFrameIfTimedOut();
                }
                if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                return clip;
            }
            catch { ArtResources.Release(clip); throw; }
        }
    }
}
