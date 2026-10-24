// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public static class ImageFiles {
        public static byte[] Png(int width=3,int height=2,int shade=0){var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);try{texture.SetPixels32(Enumerable.Range(0,width*height).Select(i=>new Color32((byte)(20+i*25+shade),(byte)(80+i*11),(byte)(140-i*15),(byte)(i==0?80:255))).ToArray());texture.Apply();return texture.EncodeToPNG();}finally{UnityEngine.Object.DestroyImmediate(texture);}}
        public static byte[] Jpeg(){var texture=new Texture2D(3,2,TextureFormat.RGB24,false);try{texture.SetPixels(Enumerable.Repeat(Color.red,6).ToArray());texture.Apply();return texture.EncodeToJPG();}finally{UnityEngine.Object.DestroyImmediate(texture);}}
        public static byte[] Exif(int orientation)=>new byte[]{73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,(byte)orientation,0,0,0,0,0,0,0};
        public static byte[] OrientJpeg(byte[] source,int orientation){using var result=new MemoryStream();result.Write(source,0,2);var exif=Exif(orientation);byte[] head={255,225,0,(byte)(exif.Length+8),69,120,105,102,0,0};result.Write(head,0,head.Length);result.Write(exif,0,exif.Length);result.Write(source,2,source.Length-2);return result.ToArray();}
        public static byte[] PngChunk(string kind,byte[] payload){using var stream=new MemoryStream();void U32(uint value){stream.WriteByte((byte)(value>>24));stream.WriteByte((byte)(value>>16));stream.WriteByte((byte)(value>>8));stream.WriteByte((byte)value);}U32((uint)payload.Length);var data=System.Text.Encoding.ASCII.GetBytes(kind).Concat(payload).ToArray();stream.Write(data,0,data.Length);uint crc=uint.MaxValue;foreach(byte b in data){crc^=b;for(int i=0;i<8;i++)crc=(crc&1)!=0?0xedb88320u^(crc>>1):crc>>1;}U32(crc^uint.MaxValue);return stream.ToArray();}
        public static byte[] OrientPng(byte[] source,int orientation)=>source.Take(33).Concat(PngChunk("eXIf",Exif(orientation))).Concat(source.Skip(33)).ToArray();
    }
}
