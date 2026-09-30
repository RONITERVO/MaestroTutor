// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
 internal sealed class PublishProgramModuleCapability:CapabilityModule
 {
  public override string Id=>"program.module.publish";
  public override string Label=>"Save reusable module";
  public override string Description=>"Publish a snapshot of an existing saved behaviour as an immutable reusable module. Pass the exact rules revision and sequence ID, a name, and its exported local function names. Unity validates importability and returns the real SHA256 content ID. Version-2 source is normalized to version 3 without editing its behaviour. Identical content is deduplicated. Nothing starts, and existing imports keep their pins. Completion waits for the file write; Stop cannot undo an already dispatched write. Inspect library/receipt instead of retrying an uncertain result.";
  public override string Duration=>"completion";
  public override IReadOnlyList<string> Requirements=>new[]{"behaviour.current","moduleLibrary.ready","storage.writable"};
  public override JObject InputSchema {get {var s=Object(new JObject {["sequenceId"]=Text("^[a-f0-9]{32}$",32),["rulesRevision"]=Number(1,1000000,true),["name"]=Text("^.{1,64}$",64),["exports"]=List(Text("^[a-zA-Z0-9_]{1,32}$",32),1,16)});s["x-features"]=new JArray("moduleLibrary.v1");return s;}}
  public override JObject OutputSchema=>ModuleWrite.ResultSchema;
  public override JObject Example=>new() {["sequenceId"]=new string('0',32),["rulesRevision"]=1,["name"]="My module",["exports"]=new JArray("main")};
  static bool Prepare(CapabilityContext context,JObject args,out ProgramModuleLibrary library,out JObject module,out string error){
   library=null;module=null;error=null;var workshop=context.Editor?context.Editor.GetComponent<RuleWorkshop>():null;
   if(!workshop||workshop.Modules==null){error="Behaviour library is not ready";return false;}library=workshop.Modules;if(!library.CanWrite(out error))return false;
   if(workshop.Revision!=(int)args["rulesRevision"]){error="Behaviours changed; inspect the saved definition before publishing";return false;}
   var sequence=workshop.Snapshot().sequences.FirstOrDefault(s=>s.id==(string)args["sequenceId"]);if(sequence==null){error="Saved behaviour was removed";return false;}
   try {module=ProgramModuleLibrary.Definition(sequence.program,(string)args["name"],((JArray)args["exports"]).Values<string>().ToArray());return library.CanPublish(module,out error);}catch(Exception ex){error=ex.Message;return false;}
  }
  public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Prepare(context,args,out _,out _,out error);
  public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
   operation=null;if(!Prepare(context,args,out var library,out var module,out error))return false;
   try{operation=new ModuleWrite(library,library.Publish(module));return true;}catch(Exception ex){error=ex.Message;return false;}
  }
 }
 internal sealed class ImportProgramModuleCapability:CapabilityModule
 {
  public override string Id=>"program.module.import";
  public override string Label=>"Import reusable module";
  public override string Description=>"Save an exact portable module definition to the local library. Pass its complete definition object and SHA256 hash from an inspected module or Maestro module file. The hash is an identity, not proof of trust. Unity validates the entire program and pin before writing. Nothing starts, no object access is granted, and saved/running imports remain unchanged. Exact object/motion/model references are retained; missing dependencies are not supplied or remapped. A dispatched write can finish after Stop; inspect the library/receipt after uncertainty.";
  public override string Duration=>"completion";
  public override IReadOnlyList<string> Requirements=>new[]{"moduleLibrary.ready","storage.writable"};
  public override JObject InputSchema {get {var schema=Object(new JObject {["hash"]=Text("^[a-f0-9]{64}$",64),["definition"]=new JObject {["type"]="object",["format"]="programModule",["x-static"]=true}});schema["x-features"]=new JArray("moduleLibraryFiles.v1");return schema;}}
  public override JObject OutputSchema=>ModuleWrite.ResultSchema;
  public override JObject Example {get {var module=JObject.Parse("{\"version\":1,\"name\":\"Empty reusable program\",\"exports\":[\"main\"],\"program\":{\"version\":3,\"entry\":\"main\",\"resources\":[],\"state\":[],\"events\":[],\"functions\":[{\"name\":\"main\",\"returns\":\"void\",\"parameters\":[],\"locals\":[],\"body\":[]}]}}");return new JObject {["hash"]=ProgramModules.Hash(module),["definition"]=module};}}
  static bool Prepare(CapabilityContext context,JObject args,out ProgramModuleLibrary library,out JObject module,out string error){
   library=context.Editor?context.Editor.GetComponent<RuleWorkshop>()?.Modules:null;module=null;error="Behaviour library is not ready";
   if(library==null||!library.CanWrite(out error))return false;
   try{module=ProgramModuleLibrary.ImportDefinition((string)args["hash"],args["definition"] as JObject);return library.CanPublish(module,out error);}catch(Exception ex){error=ex.Message;return false;}
  }
  public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Prepare(context,args,out _,out _,out error);
  public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
   operation=null;if(!Prepare(context,args,out var library,out var module,out error))return false;
   try{operation=new ModuleWrite(library,library.Publish(module));return true;}catch(Exception ex){error=ex.Message;return false;}
  }
 }
 internal sealed class RemoveProgramModuleCapability:CapabilityModule
 {
  public override string Id=>"program.module.remove";
  public override string Label=>"Remove library module";
  public override string Description=>"Remove the exact content ID from the reusable module library. Saved behaviours keep their embedded copies and continue unchanged. This deletes the library copy, including a damaged entry. There is no library Undo. Completion waits for the write; Stop cannot retract a dispatched removal. Inspect the exact ID/receipt after uncertainty; do not replay.";
  public override string Duration=>"completion";
  public override IReadOnlyList<string> Requirements=>new[]{"moduleLibrary.ready","storage.writable"};
  public override JObject InputSchema {get {var s=Object(new JObject {["hash"]=Text("^[a-f0-9]{64}$",64)});s["x-features"]=new JArray("moduleLibrary.v1");return s;}}
  public override JObject OutputSchema=>ModuleWrite.ResultSchema;
  public override JObject Example=>new() {["hash"]=new string('0',64)};
  public override bool CanRun(CapabilityContext context,JObject args,out string error){var library=context.Editor?context.Editor.GetComponent<RuleWorkshop>()?.Modules:null;error="Behaviour library is not ready";return library!=null&&library.CanWrite(out error);}
  public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
   operation=null;if(!CanRun(context,args,out error))return false;var library=context.Editor.GetComponent<RuleWorkshop>().Modules;
   try{operation=new ModuleWrite(library,library.Remove((string)args["hash"]));return true;}catch(Exception ex){error=ex.Message;return false;}
  }
 }
 internal sealed class ModuleWrite:CapabilityOperation
 {
  readonly ProgramModuleLibrary library;readonly ProgramModuleLibrary.Write write;
  public ModuleWrite(ProgramModuleLibrary library,ProgramModuleLibrary.Write write){this.library=library;this.write=write;}
  public static JObject ResultSchema=>Object(new JObject {["hash"]=Text("^[a-f0-9]{64}$",64),["revision"]=Number(1,1000000,true),["changed"]=new JObject {["type"]="boolean"}});
  public override float Seconds=>0;
  public override RuleActionState State(out string error){library.Poll();error=write.Error;return write.Pending?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;}
  public override JObject Result=>new() {["hash"]=write.Hash,["revision"]=write.Revision,["changed"]=write.Changed};
  public override string InterruptionStatus=>"Stopped waiting. A dispatched library write may still finish; inspect the library and action receipt before continuing.";
 }
}
