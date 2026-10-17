// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Tests
{
    public static class BundledAvatarFixture
    {
        public static string Manifest(byte[] bytes)=>new JObject {["version"]=1,["walkClipIndex"]=-1,["sha256"]=ModelLibrary.Hash(bytes),["name"]="Included test Maestro",["bytes"]=bytes.Length,["attribution"]="Synthetic project test model"}.ToString();
        public static BundledAvatar Write(string directory,byte[] bytes)
        {
            string path=Path.Combine(directory,BundledAvatar.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,bytes);
            return BundledAvatar.FromDirectory(Manifest(bytes),directory);
        }
    }
}
