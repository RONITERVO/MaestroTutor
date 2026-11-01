// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class CompressedAudioTests {
  static byte[] Fixture(string name)=>File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/CompressedAudio",name+".bytes"));
  [TestCase("stereo-vbr.mp3")][TestCase("mono-cbr.mp3")][TestCase("mono-low.mp3")][TestCase("stereo.ogg")][TestCase("mono.ogg")][TestCase("high.ogg")]
  public void CompressedAudioNormalizesRealEncodedTonesAndKeepsCanonicalLibrary(string name){
   var source=Fixture(name);string original=ModelLibrary.Hash(source);var asset=AudioLibrary.ImportChatAsync(name,source,original,CancellationToken.None).GetAwaiter().GetResult();
   Assert.AreNotEqual(original,asset.Hash);Assert.AreEqual(24000,asset.Inspection.Rate);Assert.AreEqual(1,asset.Inspection.Channels);Assert.AreEqual(16,asset.Inspection.Bits);
   Assert.That(asset.Inspection.Seconds,Is.EqualTo(.6).Within(.001));var pcm=WaveAudio.Decode(asset.Content);
   double rms=Math.Sqrt(pcm.Skip(2400).Take(7200).Average(x=>(double)x*x));Assert.That(rms,Is.InRange(1700,5400));
   double sine=0,cosine=0;for(int i=2400;i<9600;i++){sine+=pcm[i]*Math.Sin(i*2*Math.PI*440/24000);cosine+=pcm[i]*Math.Cos(i*2*Math.PI*440/24000);}Assert.That(Math.Sqrt(sine*sine+cosine*cosine)/7200,Is.GreaterThan(1000));
   string dir=Path.Combine(Path.GetTempPath(),"MaestroCompressed-"+Guid.NewGuid().ToString("N"));
   try{var library=new AudioLibrary(dir);library.SaveAsync(asset).GetAwaiter().GetResult();var saved=library.ReadAsync(asset.Hash).GetAwaiter().GetResult();CollectionAssert.AreEqual(asset.Content,saved.Content);CollectionAssert.AreEqual(pcm,library.DecodeAsync(asset.Hash).GetAwaiter().GetResult());Assert.That(Directory.GetFiles(dir,"*.wav"),Has.Length.EqualTo(1));Assert.IsTrue(library.TryCaptureArchive(out var archive));using(archive){var docs=new Dictionary<string,byte[]>();var payload=new Dictionary<string,Func<Stream>>();archive.Collect(docs,payload);using var copy=payload["audio/"+asset.Hash+".wav"]();Assert.AreEqual(asset.Content.Length,copy.Length);}}
   finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
  }
  [TestCase("stereo-vbr.mp3")][TestCase("mono-cbr.mp3")][TestCase("mono-low.mp3")]
  public void CompressedAudioMp3PreservesBeginningAndEndingAgainstIndependentDecoder(string name){
   var actual=WaveAudio.Decode(CompressedAudio.Normalize(Fixture(name)));var expected=WaveAudio.Decode(Fixture(name+".reference.wav"));Assert.AreEqual(expected.Length,actual.Length);
   double rmse=Math.Sqrt(actual.Zip(expected,(a,b)=>Math.Pow(a-b,2)).Average());Assert.That(rmse,Is.LessThan(8),"Decoder silence, lost ending or wrong short-block region");
   foreach(int start in new[]{0,actual.Length-480}){double a=Math.Sqrt(actual.Skip(start).Take(480).Average(x=>(double)x*x)),b=Math.Sqrt(expected.Skip(start).Take(480).Average(x=>(double)x*x));Assert.That(a/b,Is.InRange(.98,1.02));}
  }
  [Test] public void CompressedAudioWavPreservesBytesAndWrongOriginalHashFailsBeforeConversion(){
   var wav=WaveAudioTests.File();Assert.AreSame(wav,CompressedAudio.Normalize(wav));
   var mp3=Fixture("stereo-vbr.mp3");Assert.Throws<InvalidDataException>(()=>AudioLibrary.ImportChatAsync("sound",mp3,new string('0',64),CancellationToken.None).GetAwaiter().GetResult());
   using var stop=new CancellationTokenSource();stop.Cancel();Assert.Catch<OperationCanceledException>(()=>CompressedAudio.Normalize(mp3,stop.Token));
  }
  [TestCase("stereo-vbr.mp3")][TestCase("stereo.ogg")]
  public void CompressedAudioRejectsTruncationAndJunk(string name){var b=Fixture(name);Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(b.Take(b.Length-12).ToArray()));Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(b.Concat(new byte[]{1,2,3}).ToArray()));}
  [Test] public void CompressedAudioOggRejectsDamagedCrcChainingAndOtherCodecs(){
   var b=Fixture("stereo.ogg");var damaged=(byte[])b.Clone();damaged[^1]^=1;Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(damaged));
   Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(b.Concat(b).ToArray()));
   var packets=Packets(b);packets[0]=Encoding.ASCII.GetBytes("OpusHead"+new string(' ',22));Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(Container(packets)));
  }
  [TestCase(33,1)][TestCase(1,65537)][TestCase(32,65536)]
  public void CompressedAudioRejectsOversizedCodebookBeforeAllocation(int dimensions,int entries){
   var bits=new Bits();bits.Put(0,8);Book(bits,dimensions,entries);var error=Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(WithSetup(bits.Bytes())));Assert.That(error.Message,Does.Contain("Vorbis"));
  }
  [Test,Timeout(5000)] public void CompressedAudioOrderedEmptyCodebookTerminates(){
   var bits=new Bits();bits.Put(0,8);Book(bits,1,4);bits.Put(1,1);bits.Put(0,5);bits.Put(0,32);Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(WithSetup(bits.Bytes())));
  }
  [Test,Timeout(5000)] public void CompressedAudioCodebookBudgetIsSharedAcrossSetup(){
   var bits=new Bits();bits.Put(1,8);Book(bits,1,65536);bits.Put(1,1);bits.Put(15,5);bits.Put(65536,17);bits.Put(0,4);Book(bits,1,65536);
   var error=Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(WithSetup(bits.Bytes())));Assert.That(error.Message,Does.Contain("complexity"));
  }
  [Test] public void CompressedAudioMetadataCannotRequestHugeAllocation(){
   var packets=Packets(Fixture("stereo.ogg"));packets[1]=Encoding.ASCII.GetBytes("\x03vorbis").Concat(new byte[]{255,255,255,127}).ToArray();Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(Container(packets)));
  }
  [Test] public void CompressedAudioRejectsOversizedSelectionAndInvalidPcm(){
   Assert.Throws<InvalidDataException>(()=>CompressedAudio.Normalize(new byte[WaveAudio.MaximumBytes+1]));
   Assert.Throws<InvalidDataException>(()=>CompressedAudio.Convert((b,o,n)=>{b[0]=float.NaN;return 1;},24000,1,()=>{}));
   using var stop=new CancellationTokenSource();int reads=0;
   Assert.Catch<OperationCanceledException>(()=>CompressedAudio.Convert((b,o,n)=>{if(++reads==2)stop.Cancel();return n;},48000,2,()=>stop.Token.ThrowIfCancellationRequested()));Assert.AreEqual(2,reads);
  }
  [Test] public void CompressedAudioStreamingResamplerMatchesWholeWaveAcrossChunkBoundaries(){
   const int rate=48000,frames=21001;int position=0;double Signal(int i,int c)=>Math.Sin(i*2*Math.PI*(c==0?440:18000)/rate)*.2;
   var wav=WaveAudioTests.File(rate:rate,channels:2,seconds:(double)frames/rate,sample:Signal);var expected=WaveAudio.Decode(wav);
   var normalized=CompressedAudio.Convert((b,o,n)=>{int count=Math.Min(n/2,frames-position);for(int i=0;i<count;i++)for(int c=0;c<2;c++)b[o+i*2+c]=(float)((short)Math.Round(Signal(position+i,c)*32768)/32768d);position+=count;return count*2;},rate,2,()=>{});
   var actual=WaveAudio.Decode(normalized);Assert.AreEqual(expected.Length,actual.Length);Assert.That(expected.Zip(actual,(a,b)=>Math.Abs(a-b)).Max(),Is.LessThanOrEqualTo(2));
  }
  static void Book(Bits b,int dimensions,int entries){b.Put(0x564342,24);b.Put((uint)dimensions,16);b.Put((uint)entries,24);}
  sealed class Bits {readonly List<byte> data=new();int at;internal void Put(uint value,int count){for(int i=0;i<count;i++){if(at%8==0)data.Add(0);if(((value>>i)&1)!=0)data[^1]|=(byte)(1<<(at%8));at++;}}internal byte[] Bytes()=>data.ToArray();}
  static byte[] WithSetup(byte[] payload){var packets=Packets(Fixture("stereo.ogg"));packets[2]=Encoding.ASCII.GetBytes("\x05vorbis").Concat(payload).ToArray();return Container(packets);}
  static List<byte[]> Packets(byte[] b){var result=new List<byte[]>();using var packet=new MemoryStream();for(int at=0;at<b.Length;){int n=b[at+26],offset=at+27+n;for(int i=0;i<n;i++){int len=b[at+27+i];packet.Write(b,offset,len);offset+=len;if(len<255){result.Add(packet.ToArray());packet.SetLength(0);}}at=offset;}return result;}
  static byte[] Container(List<byte[]> packets){using var result=new MemoryStream();for(int i=0;i<packets.Count;i++){var p=packets[i];int n=p.Length/255+1;Assert.LessOrEqual(n,255);var page=new byte[27+n+p.Length];Encoding.ASCII.GetBytes("OggS").CopyTo(page,0);page[5]=(byte)((i==0?2:0)|(i==packets.Count-1?4:0));BitConverter.GetBytes(19).CopyTo(page,14);BitConverter.GetBytes(i).CopyTo(page,18);page[26]=(byte)n;for(int j=0;j<n;j++)page[27+j]=(byte)Math.Min(255,p.Length-j*255);p.CopyTo(page,27+n);uint crc=0;foreach(byte value in page){crc^=(uint)value<<24;for(int j=0;j<8;j++)crc=(crc<<1)^((crc&0x80000000)!=0?0x04c11db7u:0);}BitConverter.GetBytes(crc).CopyTo(page,22);result.Write(page,0,page.Length);}return result.ToArray();}
 }
}
