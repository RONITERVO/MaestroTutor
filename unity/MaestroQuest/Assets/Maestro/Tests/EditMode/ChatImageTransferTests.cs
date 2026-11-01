// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class ChatImageTransferTests {
        internal static JObject Offers(byte[] bytes,string set=null)=>new(){["offerSet"]=set??new string('a',32),["offers"]=new JArray(new JObject{["imageHash"]=ModelLibrary.Hash(bytes),["name"]="Generated tiles",["messageId"]="image1",["origin"]="generated",["bytes"]=bytes.Length})};
        internal static JObject Chunk(JObject wire,JObject request,byte[] bytes){var result=(JObject)wire.DeepClone();int offset=(int)request["offset"];result["chunk"]=new JObject{["requestId"]=(string)request["requestId"],["imageHash"]=(string)request["imageHash"],["offset"]=offset,["data"]=Convert.ToBase64String(bytes.Skip(offset).Take(ChatImageTransfer.ChunkBytes).ToArray())};return result;}
        [Test] public void SelectedBytesAreOrderedBoundedAndDuplicateChunksAreIdempotent(){
            var bytes=Enumerable.Range(0,ChatImageTransfer.ChunkBytes*2+29).Select(i=>(byte)(i%251)).ToArray();var wire=Offers(bytes);var transfer=new ChatImageTransfer();
            transfer.Receive("room",wire,0);Assert.IsNull(transfer.Request());transfer.Start(new string('b',32),(string)wire["offerSet"],ModelLibrary.Hash(bytes),0);
            while(transfer.Request()!=null){var chunk=Chunk(wire,transfer.Request(),bytes);transfer.Receive("room",chunk,1);transfer.Receive("room",chunk,1);}
            CollectionAssert.AreEqual(bytes,transfer.TakeComplete(1));Assert.IsNull(transfer.TakeComplete(1));
        }
        [Test] public void ChangingRoomOrOffersRevokesPendingBytes(){
            var bytes=new byte[60];var wire=Offers(bytes);var transfer=new ChatImageTransfer();transfer.Receive("room",wire,0);transfer.Start(new string('b',32),(string)wire["offerSet"],ModelLibrary.Hash(bytes),0);var late=Chunk(wire,transfer.Request(),bytes);
            transfer.Receive("newroom",late,1);Assert.IsNull(transfer.Request());Assert.IsNull(transfer.TakeComplete(1));
            transfer.Start(new string('c',32),(string)wire["offerSet"],ModelLibrary.Hash(bytes),1);var replacement=Offers(bytes,new string('d',32));transfer.Receive("newroom",replacement,2);Assert.IsNull(transfer.Request());Assert.IsNotEmpty(transfer.Error);
        }
        [Test] public void InvalidOffsetsAndChangedMetadataCannotCompleteTransfer(){
            var bytes=new byte[60];var wire=Offers(bytes);var transfer=new ChatImageTransfer();transfer.Receive("room",wire,0);transfer.Start(new string('b',32),(string)wire["offerSet"],ModelLibrary.Hash(bytes),0);
            var chunk=Chunk(wire,transfer.Request(),bytes);chunk["chunk"]["offset"]=1;Assert.Throws<InvalidDataException>(()=>transfer.Receive("room",chunk,1));
            var changed=(JObject)wire.DeepClone();changed["offers"][0]["name"]="Changed";Assert.Throws<InvalidDataException>(()=>transfer.Receive("room",changed,1));
            Assert.IsNull(transfer.TakeComplete(16));Assert.IsNotEmpty(transfer.Error);
        }
        [Test] public void OversizedOrDuplicateOffersCannotAllocateBuffers(){
            var wire=Offers(new byte[60]);var transfer=new ChatImageTransfer();wire["offers"][0]["bytes"]=SurfaceImage.MaximumBytes+1;Assert.Throws<InvalidDataException>(()=>transfer.Receive("room",wire,0));
            wire=Offers(new byte[60]);((JArray)wire["offers"]).Add(wire["offers"][0].DeepClone());Assert.Throws<InvalidDataException>(()=>transfer.Receive("room",wire,0));
            Assert.IsNull(transfer.Request());
        }
    }
}
