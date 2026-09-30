// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
 public sealed class ProgramModuleLibraryTests
 {
  string directory;ProgramModuleLibrary library;
  const string Source="{\"version\":3,\"entry\":\"main\",\"resources\":[],\"state\":[{\"name\":\"date\",\"initial\":\"2026-09-30T12:34:56Z\"}],\"events\":[],\"functions\":[{\"name\":\"main\",\"returns\":\"void\",\"parameters\":[],\"locals\":[],\"body\":[]}]}";
  [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-module-library-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);library=new ProgramModuleLibrary(directory);library.Flush();}
  [TearDown] public void Cleanup(){library.Flush();if(Directory.Exists(directory))Directory.Delete(directory,true);}
  static JObject Definition(string name="Counter")=>ProgramModuleLibrary.Definition(Source,name,new[]{"main"});
  [Test] public async Task ImmutablePublishingDeduplicatesAndReloadsWithExactIdentity(){
   var definition=Definition();string hash=ProgramModules.Hash(definition);var write=library.Publish(definition);definition["name"]="Changed outside";library.Flush();
   Assert.That(write.Error,Is.Null);Assert.That(write.Changed,Is.True);Assert.That(write.Hash,Is.EqualTo(hash));Assert.That(library.Revision,Is.EqualTo(2));
   var copy=library.Inspect(hash).ReadDefinition();copy["name"]="Changed readback";Assert.That(library.Inspect(hash).Name,Is.EqualTo("Counter"));Assert.That((string)library.Inspect(hash).ReadDefinition()["program"]["state"][0]["initial"],Is.EqualTo("2026-09-30T12:34:56Z"));
   var duplicate=library.Publish(Definition());library.Flush();Assert.That(duplicate.Changed,Is.False);Assert.That(duplicate.Error,Is.Null);Assert.That(library.Count,Is.EqualTo(1));Assert.That(library.Revision,Is.EqualTo(2));
   var reloaded=new ProgramModuleLibrary(directory);await Task.Run(reloaded.Flush);Assert.That(ProgramModules.Hash(reloaded.Inspect(hash).ReadDefinition()),Is.EqualTo(hash));
  }
  [Test] public void DamagedEntryIsIsolatedPreservedAndExplicitlyRemovable(){
   var write=library.Publish(Definition());library.Flush();string corrupt=new string('a',64),path=Path.Combine(directory,"program-modules.v1",corrupt+".json");File.WriteAllText(path,"{broken");
   library=new ProgramModuleLibrary(directory);library.Flush();Assert.That(library.Count,Is.EqualTo(2));Assert.That(library.Inspect(write.Hash).Error,Is.Null);Assert.That(library.Inspect(corrupt).Error,Is.Not.Null);Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));
   Assert.That(library.Retains(new string('f',32),out bool uncertain),Is.False);Assert.That(uncertain,Is.True);
   var remove=library.Remove(corrupt);library.Flush();Assert.That(remove.Error,Is.Null);Assert.That(File.Exists(path),Is.False);Assert.That(library.Count,Is.EqualTo(1));
   Assert.That(library.Retains(new string('f',32),out uncertain),Is.False);Assert.That(uncertain,Is.False);
  }
  [Test] public void StorageFailureDoesNotCreateAVisibleEntryOrOverwriteAnything(){
   string path=Path.Combine(directory,"program-modules.v1");File.WriteAllText(path,"preserve this file");var write=library.Publish(Definition());library.Flush();
   Assert.That(write.Error,Is.Not.Null);Assert.That(write.Changed,Is.False);Assert.That(library.Count,Is.Zero);Assert.That(File.ReadAllText(path),Is.EqualTo("preserve this file"));Assert.That(library.Revision,Is.EqualTo(1));
  }
  [Test] public async Task SavedModuleReferencesRemainProtectedUntilTheRemovalFinishes(){
   string id=new string('b',32);var source=JObject.Parse(Source);source["state"][0]["initial"]=id;var module=ProgramModuleLibrary.Definition(source.ToString(),"Retains motion",new[]{"main"});var write=library.Publish(module);library.Flush();
   var result=await Task.Run(()=>{bool retained=library.Retains(id,out bool uncertain);return (retained,uncertain);});Assert.That(result.retained,Is.True);Assert.That(result.uncertain,Is.False);
   var removed=library.Remove(write.Hash);Assert.That(library.Retains(id,out bool pending),Is.True);Assert.That(pending,Is.True);library.Flush();Assert.That(removed.Error,Is.Null);Assert.That(library.Retains(id,out pending),Is.False);Assert.That(pending,Is.False);
  }
  [Test] public void PortableImportsValidateExactIdentityAndCompileBeforeStorage(){
   var module=Definition();string hash=ProgramModules.Hash(module);
   var imported=ProgramModuleLibrary.ImportDefinition(hash,module);Assert.That(JToken.DeepEquals(module,imported),Is.True);
   imported["name"]="Edited after validation";Assert.That((string)module["name"],Is.EqualTo("Counter"));
   Assert.Throws<ProgramFault>(()=>ProgramModuleLibrary.ImportDefinition(new string('0',64),module));
   var invalid=(JObject)module.DeepClone();invalid["exports"]=new JArray("missing");Assert.That(ProgramModuleLibrary.ValidRecord(invalid),Is.True,"Wire shape is separate from compilation");
   Assert.Throws<ProgramFault>(()=>ProgramModuleLibrary.ImportDefinition(ProgramModules.Hash(invalid),invalid));
   foreach(var version in new JToken[]{new JValue(1.5),new JValue("1"),new JValue(2)}){invalid=(JObject)module.DeepClone();invalid["version"]=version;Assert.That(ProgramModuleLibrary.ValidRecord(invalid),Is.False);}
   Assert.That(Directory.GetFiles(directory,"*",SearchOption.AllDirectories),Is.Empty);
  }
  [Test] public void InvalidExportsAndTraversalIdsNeverTouchStorage(){
   Assert.Throws<ProgramFault>(()=>ProgramModuleLibrary.Definition(Source,"Counter",new[]{"missing"}));Assert.Throws<ProgramFault>(()=>ProgramModuleLibrary.Definition(Source,"Counter",Array.Empty<string>()));
   Assert.Throws<ProgramFault>(()=>library.Remove("../outside"));Assert.That(Directory.GetFiles(directory,"*",SearchOption.AllDirectories),Is.Empty);
  }
 }
}
