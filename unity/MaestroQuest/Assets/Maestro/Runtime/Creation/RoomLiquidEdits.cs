// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal LiquidPouring Liquids {get;private set;}
        internal event Action<string,double,double,int,string> ContainerPoured;
        internal event Action<string,double,int,string> ContainerScooped;
        internal event Action<string,double> ContainerRainCollected;
        internal void RainCollected(string id,double amount)=>ContainerRainCollected?.Invoke(id,amount);
        internal bool FinishLiquidPour(out string error) {error=null;return !Liquids||Liquids.Finish(out error);}
        internal bool CommitLiquidPour(IReadOnlyDictionary<string,RoomContainer> original,IReadOnlyDictionary<string,RoomContainer> contents,out string error) {
            var replacements=new List<RoomObjectData>();error="A container changed while liquid was flowing; the unfinished liquid flow was reverted";
            foreach(var pair in contents){
                var data=Read(pair.Key);var item=Find(pair.Key);
                if(data==null||!item||!original.TryGetValue(pair.Key,out var before)||data.containers?.Length!=1||JsonUtility.ToJson(data.containers[0])!=JsonUtility.ToJson(before))return false;
                data=Pose(data,item.transform);data.containers=new[]{pair.Value.Copy()};replacements.Add(data);
            }
            var changes=replacements.ToArray();return CommitPersisted(changes,Array.Empty<string>(),"Liquid flow saved — Undo restores its quantities",false,out error,ComponentBefore(changes));
        }
        internal void Scooped(string destination,double amount,int donors,string liquid)=>ContainerScooped?.Invoke(destination,amount,donors,liquid);
        internal void Poured(string source,double received,double spilled,int receivers,string liquid)=>ContainerPoured?.Invoke(source,received,spilled,receivers,liquid);
    }
}
