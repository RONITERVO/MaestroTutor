// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Editor {
    /// <summary>Explicit fresh-session test asset; no selected or captured user media.</summary>
    internal static class QuestRoomProbeSound {
        internal static JObject Seed(GameObject root){
            var editor=root.GetComponent<WorkspaceHost>()?.Current?.Editor;
            if(!editor)throw new InvalidOperationException("The probe workspace must open before the synthetic sound is seeded.");
            using var data=new MemoryStream();using var writer=new BinaryWriter(data,Encoding.ASCII,true);
            const int rate=24000;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+rate*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(rate*2);
            for(int i=0;i<rate;i++){double t=i/(double)rate,envelope=Math.Min(1,t/.01)*Math.Min(1,(1-t)/.04)*Math.Exp(-3*t);writer.Write((short)(Math.Sin(t*2*Math.PI*880)*envelope*8000));}writer.Flush();
            var asset=AudioLibrary.Inspect("Little bell.wav",data.ToArray());
            // SaveAsync does its file work without the Unity synchronization context.
            // This bounded, editor-only fixture finishes before publishing ready.json.
            editor.Sounds.SaveAsync(asset).GetAwaiter().GetResult();
            return new JObject {["hash"]=asset.Hash,["name"]=asset.Name,["seconds"]=asset.Inspection.Seconds,["boundary"]="Synthetic WAV saved through the real private AudioLibrary. No picker interaction or user recording."};
        }
    }
}
