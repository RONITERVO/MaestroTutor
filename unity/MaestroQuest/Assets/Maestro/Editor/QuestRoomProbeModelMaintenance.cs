// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    /// <summary>One bounded synthetic document selection in a fresh offline Editor probe only.</summary>
    internal static class QuestRoomProbeModelMaintenance
    {
        const string Boundary="Editor-only fixture substitutes the OS document picker. Native selection, preview and acceptance are real; no Android picker or headset proof.";
        static readonly string[] OtherModes={"nativeRestart","actionInterruption","motionOnly","transportOnly","liveContinuity","firstLearner","identityOnly","steeringOnly"};
        internal static void Configure(GameObject root,string evidenceDirectory)
        {
            if(!Application.isBatchMode||!Application.isPlaying)return;
            Need(!string.IsNullOrEmpty(evidenceDirectory)&&Path.IsPathRooted(evidenceDirectory),"The model fixture requires an absolute probe directory.");
            string directory=Path.GetFullPath(evidenceDirectory);WorkspaceFileInventory.Parents(directory);
            var owner=ReadObject(Path.Combine(directory,"owner.json"),16384);
            // An absent provider flag is not proof of an offline invocation.
            if(owner["providerUsed"]?.Type!=JTokenType.Boolean||(bool)owner["providerUsed"]||!Empty(owner,"providerScenario")||!Empty(owner,"resumedFrom")||owner["restartOf"]!=null||owner["sparseObjectView"]?.Type!=JTokenType.Boolean||OtherModes.Any(key=>owner[key]?.Type!=JTokenType.Boolean||(bool)owner[key]))return;
            Need(Version(owner)&&GuidId((string)owner["id"]),"Invalid model fixture owner.");
            foreach(string name in new[]{"ready.json","state.json","terminal.json","learner-resume.json","model-maintenance-picker.json","model-maintenance-picker-cache"})
                Need(WorkspaceFileInventory.Kind(Path.Combine(directory,name))=="absent","Model fixture directories cannot be reused.");
            var host=root?root.GetComponent<WorkspaceHost>():null;var content=host?host.Current:null;var editor=content?content.Editor:null;
            Need(editor,"The initial probe workspace is unavailable.");
            var picker=new Picker(directory,owner,host,content,editor);picker.CheckWorkspace(false);
            var imports=editor.GetComponent<ImportWorkshop>();Need(imports,"The native model picker is unavailable.");imports.SetPickerForTests(picker);
        }
        sealed class Picker:IModelPicker
        {
            readonly string directory,workspace,ownerId,receipt;readonly JObject owner;
            readonly WorkspaceHost host;readonly WorkspaceContent content;readonly RoomEditor editor;
            JObject result;string requestId,modelHash;bool started,released,receiptWritten;
            public string CacheRoot {get;}
            internal Picker(string directory,JObject owner,WorkspaceHost host,WorkspaceContent content,RoomEditor editor)
            {
                this.directory=directory;this.owner=(JObject)owner.DeepClone();ownerId=(string)owner["id"];this.host=host;this.content=content;this.editor=editor;
                workspace=Path.Combine(directory,"workspace");receipt=Path.Combine(directory,"model-maintenance-picker.json");CacheRoot=Path.Combine(directory,"model-maintenance-picker-cache");
            }
            public bool ReadyToStart
            {
                get {if(started)return false;try{CheckWorkspace(true);ReadSource(out _,out _);return true;}catch(Exception){return false;}}
            }
            internal void CheckWorkspace(bool ready)
            {
                WorkspaceFileInventory.Parents(directory);WorkspaceFileInventory.Parents(Path.Combine(workspace,"room"));
                Need(JToken.DeepEquals(owner,ReadObject(Path.Combine(directory,"owner.json"),16384)),"The probe owner changed.");
                Need(host&&content&&editor&&host.Current==content&&content.Editor==editor&&!host.Switching&&host.Selection?.Revision=="initial"&&host.Selection.Active.Generation=="original"&&host.Selection.Active.ReceiptEpoch=="original","The initial probe workspace changed.");
                Need(SamePath(editor.SaveDirectory,Path.Combine(workspace,"room"))&&SamePath(editor.ReceiptDirectory,editor.SaveDirectory),"The model fixture workspace changed.");
                WorkspaceFileInventory.Parents(Path.Combine(workspace,"workspace-generations.v1"));
                Need(WorkspaceFileInventory.Kind(Path.Combine(workspace,"workspace-generations.v1","current.v1.json"))=="absent","The model fixture requires the original workspace.");
                Need(WorkspaceFileInventory.Kind(Path.Combine(directory,"learner-resume.json"))=="absent","Restored probes cannot use the model fixture.");
                if(ready){var value=ReadObject(Path.Combine(directory,"ready.json"),16384);Need(Version(value)&&(string)value["id"]==ownerId&&Empty(value,"restartOf")&&SamePath((string)value["workspaceDirectory"],workspace),"The probe ready receipt changed.");}
            }
            byte[] ReadSource(out string hash,out string name)
            {
                var marker=ReadObject(Path.Combine(directory,"model-maintenance-source.json"),4096);
                Need(marker.Properties().Count()==4&&Version(marker)&&(string)marker["ownerId"]==ownerId&&marker["modelHash"]?.Type==JTokenType.String&&marker["name"]?.Type==JTokenType.String,"Invalid model fixture marker.");
                hash=(string)marker["modelHash"];name=(string)marker["name"];
                Need(ModelLibrary.ValidHash(hash)&&!string.IsNullOrWhiteSpace(name)&&name==ModelLibrary.SafeName(name),"Invalid model fixture identity.");
                byte[] bytes=ReadFile(Path.Combine(directory,"model-maintenance-source.glb"),65535);
                Need(ModelLibrary.Hash(bytes)==hash,"The model fixture source changed.");
                var inspection=ModelInspection.Inspect(bytes);
                Need(inspection.Vertices==3&&inspection.Triangles==1&&inspection.Clips==0&&inspection.TexturePixels==0&&inspection.MorphVertices==0&&!inspection.IsAvatar,"The model fixture must be one untextured, unanimated triangle.");
                return bytes;
            }
            public void Start(string id)
            {
                Need(!started&&GuidId(id),"The model fixture permits one selection only.");CheckWorkspace(true);
                byte[] bytes=ReadSource(out var hash,out var name);
                Need(WorkspaceFileInventory.Kind(CacheRoot)=="absent"&&WorkspaceFileInventory.Kind(receipt)=="absent","The model fixture already has selection evidence.");
                // No caller-provided path is accepted. This is the same private UUID/UUID shape
                // that ImportWorkshop validates before reading an Android picker result.
                started=true;requestId=id;modelHash=hash;
                Directory.CreateDirectory(CacheRoot);WorkspaceFileInventory.Parents(CacheRoot);
                string parent=Path.Combine(CacheRoot,Guid.NewGuid().ToString("D"));Directory.CreateDirectory(parent);WorkspaceFileInventory.Parents(parent);
                string path=Path.Combine(parent,Guid.NewGuid().ToString("D"));
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                Need(ImportWorkshop.SelectedPath(path,CacheRoot)==path,"The private model copy was not admitted.");
                result=new JObject{["id"]=id,["phase"]="selected",["path"]=path,["name"]=name,["error"]=""};WriteReceipt("selected");
            }
            public JObject Read(string id)
            {
                if(!started||released||id!=requestId)return null;CheckWorkspace(true);return (JObject)result?.DeepClone();
            }
            public void Release(string id)
            {
                if(!started||released||id!=requestId)return;
                // ImportWorkshop calls this only after its selected-file read and native preview
                // drain. Keep fixture files as evidence; never delete external input paths.
                released=true;result=null;WriteReceipt("released");
            }
            void WriteReceipt(string phase)
            {
                WorkspaceFileInventory.Parents(directory);
                Need(JToken.DeepEquals(owner,ReadObject(Path.Combine(directory,"owner.json"),16384)),"The probe owner changed before the picker receipt.");
                if(receiptWritten){var prior=ReadObject(receipt,4096);Need(Version(prior)&&(string)prior["ownerId"]==ownerId&&(string)prior["requestId"]==requestId&&(string)prior["modelHash"]==modelHash&&(int?)prior["starts"]==1&&(int?)prior["releases"]==0&&(string)prior["phase"]=="selected"&&(bool?)prior["sparseObjectView"]==(bool?)owner["sparseObjectView"],"The picker receipt changed.");}
                else Need(WorkspaceFileInventory.Kind(receipt)=="absent","Picker evidence already exists.");
                var value=new JObject{["version"]=1,["ownerId"]=ownerId,["modelHash"]=modelHash,["requestId"]=requestId,["starts"]=1,["releases"]=released?1:0,["phase"]=phase,["sparseObjectView"]=(bool)owner["sparseObjectView"],["boundary"]=Boundary};
                byte[] bytes=new UTF8Encoding(false).GetBytes(value.ToString(Formatting.None));string pending=receipt+"."+Guid.NewGuid().ToString("N")+".pending";
                using(var file=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(receiptWritten)File.Replace(pending,receipt,null);else File.Move(pending,receipt);receiptWritten=true;
            }
        }
        static byte[] ReadFile(string path,int maximum)
        {
            WorkspaceFileInventory.Parents(Path.GetDirectoryName(path));Need(WorkspaceFileInventory.Kind(path)=="file","Missing regular model fixture file.");
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);Need(file.Length>0&&file.Length<=maximum,"Invalid model fixture size.");
            byte[] bytes=new byte[(int)file.Length];int offset=0,count;while(offset<bytes.Length&&(count=file.Read(bytes,offset,bytes.Length-offset))>0)offset+=count;
            Need(offset==bytes.Length&&file.ReadByte()==-1,"The model fixture changed while reading.");return bytes;
        }
        static JObject ReadObject(string path,int maximum)
        {
            using var reader=new JsonTextReader(new StringReader(new UTF8Encoding(false,true).GetString(ReadFile(path,maximum)))){MaxDepth=8,DateParseHandling=DateParseHandling.None};
            var value=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});Need(!reader.Read(),"Trailing model fixture JSON.");return value;
        }
        static bool Version(JObject value)=>value["version"]?.Type==JTokenType.Integer&&(int?)value["version"]==1;
        static bool Empty(JObject value,string key)=>value[key]==null||value[key].Type==JTokenType.Null||value[key].Type==JTokenType.String&&string.IsNullOrEmpty((string)value[key]);
        static bool GuidId(string value)=>Guid.TryParseExact(value,"N",out var parsed)&&parsed.ToString("N")==value;
        static bool SamePath(string left,string right)=>!string.IsNullOrEmpty(left)&&Path.IsPathRooted(left)&&string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase);
        static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    }
}
