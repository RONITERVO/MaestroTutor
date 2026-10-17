// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    // Bounded, detached file identities for preservation and explicitly requested maintenance.
    internal static class WorkspaceFileInventory
    {
        internal static string Hash(byte[] bytes){using var sha=SHA256.Create();return ConvertHash(sha.ComputeHash(bytes));}
        static string ConvertHash(byte[] hash)=>BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();
        internal static string Kind(string path)
        {
            try {var attributes=File.GetAttributes(path);if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked history paths are not supported.");return (attributes&FileAttributes.Directory)!=0?"directory":"file";}
            catch(FileNotFoundException){return "absent";}catch(DirectoryNotFoundException){return "absent";}
        }
        // Check every existing parent, including the application root. Never traverse a reparse point.
        internal static void Parents(string path){for(string p=path;p!=null;p=Path.GetDirectoryName(p)){string kind=Kind(p);if(kind=="file")throw new IOException("A history parent is not a directory.");}}
        internal static void Scan(string path,string relative,JArray entries,ref int files,ref long bytes,long maxBytes,int maxEntries,bool hash,CancellationToken token,int depth=0,int maxDepth=6)
        {
            token.ThrowIfCancellationRequested();if(depth>maxDepth||relative.Length>512||entries.Count>=maxEntries)throw new InvalidDataException("History inventory exceeds its preservation limit.");
            string kind=Kind(path);var entry=new JObject {["path"]=relative,["kind"]=kind};entries.Add(entry);
            if(kind=="file"){
                using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long length=input.Length;
                bytes=checked(bytes+length);if(bytes>maxBytes)throw new InvalidDataException("History bytes exceed their preservation limit.");files++;entry["bytes"]=length;
                if(hash){using var sha=SHA256.Create();var buffer=new byte[65536];long read=0;int count;while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();read+=count;if(read>length)throw new IOException("History changed during inspection.");sha.TransformBlock(buffer,0,count,buffer,0);}sha.TransformFinalBlock(Array.Empty<byte>(),0,0);if(read!=length)throw new IOException("History changed during inspection.");entry["sha256"]=ConvertHash(sha.Hash);}
            }else if(kind=="directory"){
                var children=Directory.EnumerateFileSystemEntries(path).Take(maxEntries+1).OrderBy(p=>Path.GetFileName(p),StringComparer.Ordinal).ToArray();
                if(children.Length>maxEntries)throw new InvalidDataException("History inventory exceeds its preservation limit.");
                foreach(string child in children)Scan(child,relative==""?Path.GetFileName(child):relative+"/"+Path.GetFileName(child),entries,ref files,ref bytes,maxBytes,maxEntries,hash,token,depth+1,maxDepth);
            }
        }
    }
}
