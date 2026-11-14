// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Text;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Tests
{
    public static class RestModelFixture
    {
        public static byte[] Edit(byte[] source,Action<JObject,byte[]> change){
            int length=BitConverter.ToInt32(source,12);var root=JObject.Parse(Encoding.UTF8.GetString(source,20,length));
            var binary=new byte[(int)root["buffers"][0]["byteLength"]];Array.Copy(source,28+length,binary,0,binary.Length);change(root,binary);return ModelFixture.Pack(root,binary);
        }
        public static int Offset(JObject root,int accessor){var a=root["accessors"][accessor];return ((int?)root["bufferViews"][(int)a["bufferView"]]["byteOffset"]??0)+((int?)a["byteOffset"]??0);}
        public static void Float(byte[] bytes,int at,float value)=>Array.Copy(BitConverter.GetBytes(value),0,bytes,at,4);
        public static byte[] Skin(bool missingBind=false,bool quantized=false)=>Edit(ModelFixture.Mixamo(),(root,binary)=>{
            var attributes=root["meshes"][0]["primitives"][0]["attributes"];
            int ja=(int)attributes["JOINTS_0"],wa=(int)attributes["WEIGHTS_0"],j=Offset(root,ja),w=Offset(root,wa);
            if(quantized){root["accessors"][wa]["componentType"]=5123;root["accessors"][wa]["normalized"]=true;root["bufferViews"][(int)root["accessors"][wa]["bufferView"]]["byteStride"]=16;}
            for(int i=0;i<3;i++){
                Array.Copy(BitConverter.GetBytes((ushort)0),0,binary,j+i*8,2);Array.Copy(BitConverter.GetBytes((ushort)4),0,binary,j+i*8+2,2);
                for(int c=0;c<4;c++)if(quantized)Array.Copy(BitConverter.GetBytes((ushort)(c==0?16384:c==1?49151:0)),0,binary,w+i*16+c*2,2);
                else Float(binary,w+i*16+c*4,c==0?1:c==1?3:0);
            }
            root["nodes"][1]["translation"]=new JArray(.25,1.2,-.4);
            root["nodes"][5]["translation"]=new JArray(.2,.8,.3);
            root["nodes"][0]["translation"]=new JArray(.7,-.2,.4);
            if(missingBind)((JObject)root["skins"][0]).Remove("inverseBindMatrices");
            else {int bind=Offset(root,(int)root["skins"][0]["inverseBindMatrices"]);Float(binary,bind+12*4,.15f);Float(binary,bind+4*64+14*4,.2f);}
        });
        public static byte[] Morph(float meshWeight=.4f,float? nodeWeight=null,bool multiple=false)=>Edit(ModelFixture.MorphMotion(),(root,binary)=>{
            root["meshes"][0]["weights"]=new JArray(meshWeight);
            if(nodeWeight.HasValue)root["nodes"][0]["weights"]=new JArray(nodeWeight.Value);
            if(multiple){((JArray)root["nodes"]).Add(new JObject{["mesh"]=0,["translation"]=new JArray(2,1,-1),["weights"]=new JArray(-.75)});((JArray)root["scenes"][0]["nodes"]).Add(1);}
        });
    }
}
