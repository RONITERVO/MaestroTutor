// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using System.Linq;
namespace Maestro.Quest.Rules
{
    public sealed class RuleStorage
    {
        readonly VersionedRoomFile<RuleDocument> file;
        readonly string directory;
        RuleDocument accepted=new();
        readonly System.Collections.Generic.HashSet<string> preservedUnavailable=new();
        public bool ReadOnly => file.ReadOnly;
        public RuleStorage(string directory) { this.directory=System.IO.Path.GetFullPath(directory);file = new VersionedRoomFile<RuleDocument>(directory,"behaviours",512*1024,x => x.Validate(out _,true),x => x.Copy(),null,_=>{},2,null,json=>json["sequences"] is Newtonsoft.Json.Linq.JArray sequences && sequences.All(RuleSequence.ValidWire),minimumVersion:2); }
        public bool RetainsMotion(string id,out bool uncertain,bool force=false) => file.Retains(x => x.sequences.SelectMany(sequence => sequence.MotionIds()),id,out uncertain,force,x=>x.sequences.Any(sequence=>sequence.Compile(out _)==null));
        public RuleDocument Load(out string message)
        {
            var value=file.Load(out message);
            if(value==null && !ReadOnly && (System.IO.File.Exists(System.IO.Path.Combine(directory,"behaviours.v1.json")) || Enumerable.Range(1,5).Any(version=>System.IO.File.Exists(System.IO.Path.Combine(directory,"rules.v"+version+".json")))))
                message="Development behaviours were reset for the new program format. Create a behaviour in the book.";
            accepted=(value??new RuleDocument()).Copy();preservedUnavailable.Clear();
            foreach(var sequence in accepted.sequences)
                if(accepted.ProgramError(sequence)!=null)preservedUnavailable.Add(UnityEngine.JsonUtility.ToJson(sequence));
            if(value!=null&&preservedUnavailable.Count>0)
                message=(string.IsNullOrEmpty(message)?"":message+" ")+preservedUnavailable.Count+" behaviour(s) are unavailable. Their sources are preserved; inspect them in the book. Other behaviours remain usable.";
            return accepted.Copy();
        }
        public bool Save(RuleDocument document,out string error)
        {
            error="Invalid behaviour data";
            if(document==null||!document.ValidateEdit(accepted,out error,sequence=>preservedUnavailable.Contains(UnityEngine.JsonUtility.ToJson(sequence))))return false;
            if(!file.Save(document,out error))return false;
            accepted=document.Copy();return true;
        }
    }
}
