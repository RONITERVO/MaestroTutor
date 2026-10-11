// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.Build;
namespace Maestro.Quest.Tests
{
    public sealed class QuestReleaseInputsTests
    {
        const string Fixture="Assets/Maestro/Tests/Fixtures/release-profile.json";
        JObject Profile()=>QuestReleaseInputs.Read(Fixture);
        [Test] public void AcceptsTheSharedPublicProfile()=>Assert.That((int)QuestReleaseInputs.ValidateProfile(Profile())["versionCode"],Is.EqualTo(1));
        [TestCase("package","com.maestro.quest.development")][TestCase("versionName","1.0")][TestCase("metaAppId","")][TestCase("secret","must-not-be-read")]
        public void RefusesBadProfile(string name,string value){var p=Profile();p[name]=value;Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.ValidateProfile(p));}
        [TestCase("questFirebaseAppId","1:123456:web:abcdef")][TestCase("questFirebaseAppId","1:999:web:fedcba")]
        [TestCase("questAccountLinkVerificationUrl","https://chatwithmaestro.com/quest-link.html?code=secret")]
        [TestCase("backendBaseUrl","http://example.invalid/api")][TestCase("firebaseAuthDomain","host/path")]
        [TestCase("firebaseAppCheckDebugToken","must-not-be-read")]
        public void RefusesWrongWebIdentityOrDebugValues(string name,string value){var p=Profile();p["web"][name]=value;Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.ValidateProfile(p));}
        [Test] public void RequiresPositiveIntegerVersionCode(){var p=Profile();p["versionCode"]=0;Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.ValidateProfile(p));}
        [Test] public void ChecksEveryBundledFileAndTheExactProfile()
        {
            string directory=Path.Combine(Path.GetTempPath(),"maestro-release-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                File.WriteAllText(Path.Combine(directory,"index.html"),"book");File.WriteAllText(Path.Combine(directory,"quest-link.html"),"approval");
                var files=new JArray();foreach(var name in new[]{"index.html","quest-link.html"})files.Add(new JObject { ["path"]=name,["sha256"]=QuestReleaseInputs.Hash(File.ReadAllBytes(Path.Combine(directory,name))) });
                var receipt=new JObject { ["version"]=1,["profileSha256"]=QuestReleaseInputs.Hash(File.ReadAllBytes(Fixture)),["files"]=files };
                void Save()=>File.WriteAllText(Path.Combine(directory,QuestReleaseInputs.ReceiptName),receipt.ToString());Save();
                QuestReleaseInputs.VerifyWeb(Fixture,directory);
                File.WriteAllText(Path.Combine(directory,"extra.js"),"unlisted");Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.VerifyWeb(Fixture,directory));File.Delete(Path.Combine(directory,"extra.js"));
                File.WriteAllText(Path.Combine(directory,"index.html"),"wrong build");Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.VerifyWeb(Fixture,directory));File.WriteAllText(Path.Combine(directory,"index.html"),"book");
                receipt["profileSha256"]=new string('0',64);Save();Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.VerifyWeb(Fixture,directory));
                receipt["profileSha256"]=QuestReleaseInputs.Hash(File.ReadAllBytes(Fixture));files[0]["path"]="../outside";Save();Assert.Throws<BuildFailedException>(()=>QuestReleaseInputs.VerifyWeb(Fixture,directory));
            } finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        }
    }
}
