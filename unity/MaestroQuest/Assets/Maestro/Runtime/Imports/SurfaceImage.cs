// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace Maestro.Quest.Imports {
    /// <summary>Bound encoded input and dimensions before invoking Unity's native decoder.
    /// Original bytes identify the asset; EXIF is removed only from the decode copy,
    /// so orientation is applied exactly once by the renderer.</summary>
    internal static class SurfaceImage {
        internal const int MaximumBytes=16*1024*1024,MaximumSide=2048;
        internal sealed class Info {
            internal string Format;internal int Width,Height,Orientation=1;
            internal readonly List<(int Start,int Length)> Metadata=new();
            internal int DisplayWidth=>Orientation>=5?Height:Width;
            internal int DisplayHeight=>Orientation>=5?Width:Height;
            internal long TextureBytes {get {long bytes=0;int w=Width,h=Height;do{bytes+=(long)w*h*4;if(w==1&&h==1)break;w=Math.Max(1,w/2);h=Math.Max(1,h/2);}while(true);return bytes;}}
        }
        static void Need(bool test,string error="The image is incomplete or unsupported"){if(!test)throw new InvalidDataException(error);}
        static ushort Be16(byte[] b,int i)=>(ushort)((b[i]<<8)|b[i+1]);
        static uint Be32(byte[] b,int i)=>((uint)b[i]<<24)|((uint)b[i+1]<<16)|((uint)b[i+2]<<8)|b[i+3];
        static void Dimensions(Info info){Need(info.Width>0&&info.Height>0&&info.Width<=MaximumSide&&info.Height<=MaximumSide,"Choose an image no larger than 2048 pixels on either side.");}
        internal static Info Inspect(byte[] bytes,CancellationToken cancel=default){
            Need(bytes!=null&&bytes.Length>=24&&bytes.Length<=MaximumBytes,"Choose a PNG or JPEG up to 16 MB.");cancel.ThrowIfCancellationRequested();
            if(bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return Png(bytes,cancel);
            if(bytes[0]==255&&bytes[1]==216)return Jpeg(bytes,cancel);
            throw new InvalidDataException("Choose a static 8-bit PNG or JPEG image.");
        }
        static readonly uint[] CrcTable=Enumerable.Range(0,256).Select(n=>{uint c=(uint)n;for(int k=0;k<8;k++)c=(c&1)!=0?0xedb88320u^(c>>1):c>>1;return c;}).ToArray();
        static uint Crc(byte[] b,int start,int count,CancellationToken cancel){uint c=uint.MaxValue;for(int i=start;i<start+count;i++){if((i&65535)==0)cancel.ThrowIfCancellationRequested();c=CrcTable[(c^b[i])&255]^(c>>8);}return c^uint.MaxValue;}
        static Info Png(byte[] b,CancellationToken cancel){
            var info=new Info{Format="png"};int at=8,chunks=0;bool header=false,data=false,dataEnded=false,palette=false,exif=false;long compressed=0;
            while(at<b.Length){cancel.ThrowIfCancellationRequested();Need(++chunks<=2048&&b.Length-at>=12);uint length=Be32(b,at);Need(length<=b.Length-at-12);int n=(int)length,p=at+8;
                string kind=System.Text.Encoding.ASCII.GetString(b,at+4,4);Need(kind.All(c=>c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'));
                Need(Crc(b,at+4,n+4,cancel)==Be32(b,p+n),"The PNG checksum is damaged.");
                Need(header||kind=="IHDR");
                if(kind=="IHDR") {Need(!header&&at==8&&n==13);Need(Be32(b,p)<=int.MaxValue&&Be32(b,p+4)<=int.MaxValue);info.Width=(int)Be32(b,p);info.Height=(int)Be32(b,p+4);Dimensions(info);Need(b[p+8]==8&&b[p+9] is 0 or 2 or 3 or 4 or 6&&b[p+10]==0&&b[p+11]==0&&b[p+12]<=1,"Use a static 8-bit PNG.");header=true;}
                else if(kind=="PLTE"){Need(!data&&!palette&&n>0&&n<=768&&n%3==0);palette=true;}
                else if(kind=="IDAT"){Need(!dataEnded);data=true;compressed+=n;}
                else if(kind=="IEND"){Need(n==0&&data&&compressed>0&&at+12==b.Length);Need(b[25]!=3||palette);return info;}
                else {if(data)dataEnded=true;Need(kind is not ("acTL" or "fcTL" or "fdAT"),"Animated PNGs are not supported; choose a still image.");Need((b[at+4]&32)!=0,"This PNG uses an unsupported critical chunk.");
                    if(kind=="eXIf"){Need(!exif);info.Orientation=Exif(b,p,n);exif=true;info.Metadata.Add((at,n+12));}}
                at=p+n+4;
            }
            throw new InvalidDataException("The PNG has no complete end marker.");
        }
        static Info Jpeg(byte[] b,CancellationToken cancel){
            var info=new Info{Format="jpeg"};int at=2,segments=0;bool frame=false,scan=false,exif=false;
            while(at<b.Length){cancel.ThrowIfCancellationRequested();Need(++segments<=4096&&b[at++]==255);while(at<b.Length&&b[at]==255)at++;Need(at<b.Length);int marker=b[at++];
                if(marker==217){Need(frame&&scan&&at==b.Length);return info;}
                Need(marker!=0&&marker!=216&&marker is not (>=208 and <=215)&&b.Length-at>=2);int n=Be16(b,at);Need(n>=2&&n<=b.Length-at);int p=at+2,end=at+n;
                if(marker is >=192 and <=207&&marker is not (196 or 200 or 204)){
                    Need(marker is 192 or 194&&!frame&&n>=8,"Choose a baseline or progressive 8-bit JPEG.");Need(b[p]==8);info.Height=Be16(b,p+1);info.Width=Be16(b,p+3);Dimensions(info);Need(b[p+5] is 1 or 3&&n==8+3*b[p+5],"Use a grayscale or RGB JPEG.");frame=true;
                }
                if(marker==225&&n>=8&&b.Skip(p).Take(6).SequenceEqual(new byte[]{69,120,105,102,0,0})) {Need(!exif);info.Orientation=Exif(b,p+6,n-8);exif=true;info.Metadata.Add((at-2,n+2));}
                at=end;
                if(marker==218){Need(frame&&n>=6);scan=true;bool found=false;while(at<b.Length){if((at&65535)==0)cancel.ThrowIfCancellationRequested();if(b[at++]!=255)continue;Need(at<b.Length);int next=b[at];if(next==0||next is >=208 and <=215){at++;continue;}at--;found=true;break;}Need(found);}
            }
            throw new InvalidDataException("The JPEG has no complete end marker.");
        }
        static int Exif(byte[] b,int start,int length){
            Need(length>=8);bool little=b[start]==73&&b[start+1]==73;Need(little||b[start]==77&&b[start+1]==77);
            void Bounds(int i,int n)=>Need(i>=0&&n>=0&&i<=length-n,"The image orientation metadata is damaged.");
            ushort U16(int i){Bounds(i,2);return little?(ushort)(b[start+i]|b[start+i+1]<<8):Be16(b,start+i);}
            uint U32(int i){Bounds(i,4);return little?(uint)(b[start+i]|b[start+i+1]<<8|b[start+i+2]<<16|b[start+i+3]<<24):Be32(b,start+i);}
            Need(U16(2)==42);uint offset=U32(4);Need(offset<=int.MaxValue);int at=(int)offset;int count=U16(at);Need(count<=1024);Bounds(at+2,count*12+4);int orientation=1;bool found=false;
            for(int k=0;k<count;k++){int entry=at+2+12*k;if(U16(entry)!=274)continue;Need(!found&&U16(entry+2)==3&&U32(entry+4)==1);orientation=U16(entry+8);Need(orientation>=1&&orientation<=8);found=true;}return orientation;
        }
        internal static byte[] DecodeBytes(byte[] original,Info info){
            if(info.Metadata.Count==0)return original;using var output=new MemoryStream(original.Length);int at=0;
            foreach(var span in info.Metadata){output.Write(original,at,span.Start-at);at=span.Start+span.Length;}output.Write(original,at,original.Length-at);return output.ToArray();
        }
        // Top-left source pixel -> upright top-left destination. Unity texture rows
        // are bottom-up; the renderer performs that conversion around this mapping.
        internal static (int X,int Y) Upright(int x,int y,int w,int h,int orientation)=>orientation switch {
            1=>(x,y),2=>(w-1-x,y),3=>(w-1-x,h-1-y),4=>(x,h-1-y),5=>(y,x),6=>(h-1-y,x),7=>(h-1-y,w-1-x),8=>(y,w-1-x),_=>throw new ArgumentOutOfRangeException(nameof(orientation))};
    }
}
