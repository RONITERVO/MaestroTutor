// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using System.Threading;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Imports
{
    // Only an explicit save of verified image/sound bytes may repair its name
    // sidecar. Passive reads keep their existing strict, non-mutating behavior.
    internal static class ImportedAssetSidecar
    {
        const int MaximumBytes=1024;
        enum State { Missing,Valid,Corrupt }
        static State Inspect(string path,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();FileAttributes attributes;
            try{attributes=File.GetAttributes(path);}
            catch(FileNotFoundException){return State.Missing;}
            catch(DirectoryNotFoundException){return State.Missing;}
            // A link, inaccessible file or wrong path kind is not corrupt text.
            if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked asset metadata paths are not supported.");
            if((attributes&FileAttributes.Directory)!=0)throw new IOException("Asset metadata is not a regular file.");
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length>MaximumBytes)return State.Corrupt;
            var bytes=new byte[(int)input.Length];int at=0;
            while(at<bytes.Length){cancel.ThrowIfCancellationRequested();int read=input.Read(bytes,at,bytes.Length-at);if(read==0)throw new EndOfStreamException("Asset metadata changed while reading.");at+=read;}
            if(input.ReadByte()!=-1)throw new IOException("Asset metadata changed while reading.");
            cancel.ThrowIfCancellationRequested();
            // Match the current readers' strict UTF-8 default and BOM detection,
            // including their supported UTF-16 files. Never rewrite valid bytes.
            using var reader=new StreamReader(new MemoryStream(bytes,false),new UTF8Encoding(false,true),true);
            try{reader.ReadToEnd();}catch(DecoderFallbackException){return State.Corrupt;}
            cancel.ThrowIfCancellationRequested();return State.Valid;
        }
        internal static void SaveAccepted(string path,string name,CancellationToken cancel,Action beforePublish=null)
        {
            if(Inspect(path,cancel)==State.Valid)return;
            byte[] bytes=Encoding.UTF8.GetBytes(name+"\n");if(bytes.Length>MaximumBytes)throw new ArgumentException("Accepted asset metadata exceeds its limit.");
            string staged=path+"."+Guid.NewGuid().ToString("N")+".part";
            try {
                using(var output=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                beforePublish?.Invoke();cancel.ThrowIfCancellationRequested();
                // A newly valid sidecar also wins if another owner repaired it
                // during preparation. Recheck links and I/O errors before commit.
                var state=Inspect(path,cancel);if(state==State.Valid)return;
                cancel.ThrowIfCancellationRequested();
                if(state==State.Missing)File.Move(staged,path);else FilePublication.Replace(staged,path,null);
            }finally{if(File.Exists(staged))File.Delete(staged);}
        }
    }
}
