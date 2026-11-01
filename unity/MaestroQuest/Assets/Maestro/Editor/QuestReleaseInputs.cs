// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Maestro.Quest.Book;
using Newtonsoft.Json.Linq;
using UnityEditor.Build;

namespace Maestro.Quest.Editor
{
    /// <summary>Public release inputs and exact web files, checked again inside Unity.</summary>
    public static class QuestReleaseInputs
    {
        public const string ProfileVariable = "MAESTRO_QUEST_RELEASE_PROFILE";
        public const string ReceiptName = "quest-release.json";
        public static string Hash(byte[] value) { using var sha=SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(value)).Replace("-","").ToLowerInvariant(); }
        public static JObject Read(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length>65536) throw new BuildFailedException("Missing or oversized Quest release JSON.");
            try { return JObject.Parse(File.ReadAllText(path),new JsonLoadSettings { DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error }); }
            catch { throw new BuildFailedException("Invalid Quest release JSON."); }
        }
        static void Need(bool valid,string field) { if(!valid)throw new BuildFailedException("Quest release configuration: check "+field+"."); }
        static void Keys(JObject value,params string[] names) { Need(value!=null&&value.Properties().Select(p=>p.Name).OrderBy(n=>n,StringComparer.Ordinal).SequenceEqual(names.OrderBy(n=>n,StringComparer.Ordinal)),"public profile fields"); }
        static string Text(JObject value,string field)
        {
            Need(value?[field]?.Type==JTokenType.String,field);string text=(string)value[field];
            Need(!string.IsNullOrWhiteSpace(text)&&text==text.Trim()&&text.Length<=2048&&!text.Any(char.IsControl),field);return text;
        }
        public static JObject ValidateProfile(JObject profile)
        {
            Keys(profile,"version","package","versionName","versionCode","metaAppId","signingCertificateSha256","web");
            Need(profile["version"]?.Type==JTokenType.Integer&&(long)profile["version"]==1,"version");
            string package=Text(profile,"package");Need(package.Length<=150&&Regex.IsMatch(package,@"^[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+$")&&!Regex.IsMatch(package,@"(?:^|\.)(development|debug|example)(?:\.|$)"),"package");
            Need(Regex.IsMatch(Text(profile,"versionName"),@"^\d{1,3}\.\d{1,3}\.\d{1,3}$"),"versionName");
            Need(profile["versionCode"]?.Type==JTokenType.Integer&&(long)profile["versionCode"]>=1&&(long)profile["versionCode"]<=2100000000,"versionCode");
            Need(Regex.IsMatch(Text(profile,"metaAppId"),@"^[1-9][0-9]{5,24}$"),"metaAppId");
            string certificate=Text(profile,"signingCertificateSha256");Need(Regex.IsMatch(certificate,"^[a-fA-F0-9]{64}$")&&certificate.Distinct().Count()>1,"signingCertificateSha256");
            var web=profile["web"] as JObject;
            Keys(web,"firebaseApiKey","firebaseAuthDomain","firebaseProjectId","firebaseAppId","questFirebaseAppId","firebaseAppCheckSiteKey","backendBaseUrl","questAttestationUrl","questAccountLinkUrl","questAccountLinkVerificationUrl");
            foreach(var p in web.Properties())Text(web,p.Name);
            var original=Regex.Match((string)web["firebaseAppId"],@"^1:([0-9]+):web:[a-f0-9]+$");
            var quest=Regex.Match((string)web["questFirebaseAppId"],@"^1:([0-9]+):(web|android):[a-f0-9]+$");
            Need(original.Success&&quest.Success&&original.Groups[1].Value==quest.Groups[1].Value&&(string)web["firebaseAppId"]!=(string)web["questFirebaseAppId"],"distinct same-project Firebase registrations");
            Need(Regex.IsMatch((string)web["firebaseProjectId"],@"^[a-z][a-z0-9-]{4,28}[a-z0-9]$"),"firebaseProjectId");
            Need(Host((string)web["firebaseAuthDomain"]),"firebaseAuthDomain");
            foreach(var key in new[]{"backendBaseUrl","questAttestationUrl","questAccountLinkUrl","questAccountLinkVerificationUrl"}) {
                Need(Uri.TryCreate((string)web[key],UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.UserInfo.Length==0&&uri.Query.Length==0&&uri.Fragment.Length==0&&uri.IsDefaultPort&&Host(uri.Host),key);
            }
            Need(new[]{(string)web["backendBaseUrl"],(string)web["questAttestationUrl"],(string)web["questAccountLinkUrl"]}.Distinct().Count()==3,"separate endpoints");
            Need((string)web["questAccountLinkVerificationUrl"]==BookExternalLinks.AccountLinkUrl,"native approval URL");return profile;
        }
        static bool Host(string value)=>Regex.IsMatch(value,@"^(?:[a-z0-9-]+\.)+[a-z]{2,}$",RegexOptions.IgnoreCase)&&!value.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase);
        public static void VerifyWeb(string profilePath,string directory)
        {
            ValidateProfile(Read(profilePath));var receipt=Read(Path.Combine(directory,ReceiptName));
            Need((int?)receipt["version"]==1&&(string)receipt["profileSha256"]==Hash(File.ReadAllBytes(profilePath)),"web profile hash");
            var files=receipt["files"] as JArray;Need(files!=null&&files.Count>=2&&files.Count<=10000,"web inventory");
            var actual=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(p)!=ReceiptName).Select(p=>Path.GetRelativePath(directory,p).Replace('\\','/')).ToHashSet(StringComparer.Ordinal);
            var seen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var file in files) {
                string relative=(string)file["path"],digest=(string)file["sha256"];
                Need(!string.IsNullOrEmpty(relative)&&!relative.Contains("\\")&&!relative.Contains(":")&&relative.Split('/').All(s=>s.Length>0&&s!="."&&s!="..")&&seen.Add(relative),"web inventory path");
                var path=Path.GetFullPath(Path.Combine(directory,relative));var root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                Need(path.StartsWith(root,StringComparison.Ordinal)&&File.Exists(path),"web inventory file");
                var current=new FileInfo(path) as FileSystemInfo;
                while(current!=null&&current.FullName.Length>=root.Length-1){Need((current.Attributes&FileAttributes.ReparsePoint)==0,"web symbolic link");current=current is FileInfo f?f.Directory:((DirectoryInfo)current).Parent;}
                Need(digest==Hash(File.ReadAllBytes(path)),"web file hash");
            }
            Need(actual.SetEquals(seen)&&seen.Contains("index.html")&&seen.Contains("quest-link.html"),"complete web inventory");
        }
    }
}
