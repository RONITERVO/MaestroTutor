// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
namespace Maestro.Quest.Imports {
    // One accepted MP3 vocabulary for finite imports and live packet validation.
    internal static class Mp3Packets {
        internal readonly struct Header {
            internal readonly int Rate,Channels,Bytes,Samples;
            internal Header(int rate,int channels,int bytes,int samples){Rate=rate;Channels=channels;Bytes=bytes;Samples=samples;}
        }
        internal static int MetadataLength(byte[] b){
            if(b==null||b.Length<10||b[0]!='I'||b[1]!='D'||b[2]!='3'||b[3]<2||b[3]>4||b[4]==255||(b[6]|b[7]|b[8]|b[9])>=128)throw new InvalidDataException("Invalid MP3 metadata");
            return 10+(b[6]<<21|b[7]<<14|b[8]<<7|b[9])+(b[3]==4&&(b[5]&16)!=0?10:0);
        }
        internal static Header Read(byte[] b,int at){
            if(b==null||at<0||at>b.Length-4||b[at]!=255||(b[at+1]&224)!=224)throw new InvalidDataException("Expected a complete MP3 frame");
            int version=(b[at+1]>>3)&3,layer=(b[at+1]>>1)&3,bitrate=b[at+2]>>4,sampling=(b[at+2]>>2)&3;
            if(version==1||layer!=1||bitrate==0||bitrate==15||sampling==3)throw new InvalidDataException("Use Layer III MP3 with CBR or VBR, not free bitrate");
            int rate=Rates[sampling]/(version==3?1:version==2?2:4),channels=(b[at+3]>>6)==3?1:2;
            int length=(version==3?144000*High[bitrate]:72000*Low[bitrate])/rate+((b[at+2]>>1)&1);
            if(length<4||length>4096)throw new InvalidDataException("Invalid MP3 frame size");
            return new Header(rate,channels,length,version==3?1152:576);
        }
        static readonly int[] Rates={44100,48000,32000},High={0,32,40,48,56,64,80,96,112,128,160,192,224,256,320},Low={0,8,16,24,32,40,48,56,64,80,96,112,128,144,160};
    }
}
