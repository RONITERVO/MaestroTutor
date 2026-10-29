// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Newtonsoft.Json;
namespace Maestro.Quest.Creation
{
    internal sealed partial class VersionedRoomFile<T> where T:class
    {
        // Pure decoding of a separately bounded, owned retained-file snapshot.
        // No Load, migration publication, cache reuse, or recovery is permitted.
        internal bool InspectRetainedReferences(byte[] bytes,int expected,Func<T,bool> references,out bool uncertain,Func<T,bool> unknown=null)
        {
            uncertain=true;
            if(bytes==null||bytes.Length<1||bytes.Length>maximum||expected<minimumVersion||expected>version)return false;
            try{
                if(!Decode(bytes,expected,out var document,out _))return false;
                uncertain=unknown?.Invoke(document)==true;return references(document);
            }catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ArgumentException||error is JsonException||error is OverflowException){uncertain=true;return false;}
        }
    }
}
