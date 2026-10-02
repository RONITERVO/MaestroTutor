// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Persistence
{
    // Fresh recovery uses the same built-in poses as scene creation, with no user programs/assets.
    internal static class WorkspaceDefaults
    {
        internal static Vector3 BookPosition=>new(0,1.16f,.65f);
        internal static Quaternion BookRotation=>Quaternion.Euler(24,0,0);
        internal static Vector3 MaestroPosition=>new(-.78f,0,1.4f);
        internal static Quaternion MaestroRotation=>Quaternion.Euler(0,160,0);
        internal static WorkspaceArchiveSnapshot Snapshot(BundledAvatar includedAvatar=null,BundledMotions includedMotions=null)
        {
            byte[] Document(object value)=>new UTF8Encoding(false,true).GetBytes(JsonUtility.ToJson(value));
            var room=new RoomDocument {version=2,objects=new[]{
                new RoomObjectData {id="book",kind=RoomObjectKind.Book,position=BookPosition,rotation=BookRotation},
                new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro,modelHash=includedAvatar?.Hash,walkClip=(includedAvatar?.WalkClipIndex??-1)+1,position=MaestroPosition,rotation=MaestroRotation}}};
            var documents=new Dictionary<string,byte[]> {
                ["room.v2.json"]=Document(room),["behaviours.v2.json"]=Document(new RuleDocument()),
                ["controls.v2.json"]=Document(new ControllerPreferences()),["avatar-activities.v2.json"]=Document(new AvatarActivityDocument()),
                ["motions/motions.v2.json"]=Document(includedMotions?.Catalogue??new MotionCatalogue())};
            var assets=new Dictionary<string,Func<Stream>>();
            if(includedAvatar!=null){
                var model=includedAvatar.Read();
                assets.Add("models/"+model.Hash+".glb",()=>new MemoryStream(model.Bytes,false));
                documents.Add("models/"+model.Hash+".txt",Encoding.UTF8.GetBytes(model.Name+"\nSHA256: "+model.Hash+"\n\n"+model.Inspection.Attribution+"\n\n"+includedAvatar.Attribution));
            }
            if(includedMotions!=null)foreach(var entry in includedMotions.Catalogue.entries){string hash=entry.hash;assets.Add("motions/"+hash+".motion.glb",()=>new MemoryStream(includedMotions.Read(hash),false));}
            return new WorkspaceArchiveSnapshot(documents,assets);
        }
    }
}
