// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Original minimal skinned glTF: travelling hips, animated leg/head, 17 bones.
import {writeFileSync} from 'node:fs';
const chunks=[],views=[],accessors=[];
let bytes=0;
function floats(type,size,values){const data=Buffer.alloc(values.length*4);values.forEach((v,i)=>data.writeFloatLE(v,i*4));views.push({buffer:0,byteOffset:bytes,byteLength:data.length});bytes+=data.length;chunks.push(data);accessors.push({bufferView:views.length-1,componentType:5126,count:values.length/size,type});return accessors.length-1;}
const positions=floats('VEC3',3,[-.5,0,0,.5,0,0,0,1,0]);accessors[positions].min=[-.5,0,0];accessors[positions].max=[.5,1,0];
const normals=floats('VEC3',3,[0,0,1,0,0,1,0,0,1]),times=floats('SCALAR',1,[0,1]),rotations=floats('VEC4',4,[0,0,0,1,0,0,Math.SQRT1_2,Math.SQRT1_2]);
const names=['Hips','Spine','Spine1','Neck','Head','LeftArm','LeftForeArm','LeftHand','RightArm','RightForeArm','RightHand','LeftUpLeg','LeftLeg','LeftFoot','RightUpLeg','RightLeg','RightFoot'];
const parents=[-1,0,1,2,3,2,5,6,2,8,9,0,11,12,0,14,15];
const translations=[[0,1,0],[0,.15,0],[0,.15,0],[0,.2,0],[0,.1,0],[.15,.15,0],[.25,0,0],[.25,0,0],[-.15,.15,0],[-.25,0,0],[-.25,0,0],[.1,-.1,0],[0,-.4,0],[0,-.4,.05],[-.1,-.1,0],[0,-.4,0],[0,-.4,.05]];
const nodes=[{name:'Animated triangle',mesh:0,skin:0},...names.map((name,i)=>({name:'mixamorig:'+name,translation:translations[i]}))];
parents.forEach((p,i)=>{if(p>=0)(nodes[p+1].children??=[]).push(i+1);});
const jointsData=Buffer.alloc(24);[0,0,0,0,0,0,0,0,4,0,0,0].forEach((v,i)=>jointsData.writeUInt16LE(v,i*2));views.push({buffer:0,byteOffset:bytes,byteLength:24});bytes+=24;chunks.push(jointsData);accessors.push({bufferView:views.length-1,componentType:5123,count:3,type:'VEC4'});const joints=accessors.length-1;
const weights=floats('VEC4',4,[1,0,0,0,1,0,0,0,1,0,0,0]),matrices=[];
names.forEach((_,i)=>{const p=[0,0,0];for(let a=i;a>=0;a=parents[a])translations[a].forEach((v,j)=>p[j]+=v);matrices.push(1,0,0,0,0,1,0,0,0,0,1,0,-p[0],-p[1],-p[2],1);});
const bind=floats('MAT4',16,matrices),travel=floats('VEC3',3,[0,1,0,2,1,0]);
const root={asset:{version:'2.0',copyright:'Original Maestro test fixture; Apache-2.0'},scene:0,scenes:[{nodes:[0,1]}],nodes,meshes:[{primitives:[{attributes:{POSITION:positions,NORMAL:normals,JOINTS_0:joints,WEIGHTS_0:weights},material:0}]}],materials:[{pbrMetallicRoughness:{baseColorFactor:[.25,.5,.75,1],metallicFactor:0}}],skins:[{joints:names.map((_,i)=>i+1),inverseBindMatrices:bind}],animations:[{name:'Synthetic walking pose',samplers:[{input:times,output:travel},{input:times,output:rotations}],channels:[{sampler:0,target:{node:1,path:'translation'}},...[5,12].map(node=>({sampler:1,target:{node,path:'rotation'}}))]}],bufferViews:views,accessors,buffers:[{byteLength:bytes}]};
let json=Buffer.from(JSON.stringify(root));json=Buffer.concat([json,Buffer.alloc((4-json.length%4)%4,32)]);const binary=Buffer.concat(chunks),header=Buffer.alloc(20),binaryHeader=Buffer.alloc(8);
header.writeUInt32LE(0x46546c67,0);header.writeUInt32LE(2,4);header.writeUInt32LE(28+json.length+binary.length,8);header.writeUInt32LE(json.length,12);header.writeUInt32LE(0x4e4f534a,16);binaryHeader.writeUInt32LE(binary.length,0);binaryHeader.writeUInt32LE(0x004e4942,4);
writeFileSync('test-fixtures/models/walking-character.glb',Buffer.concat([header,json,binaryHeader,binary]));
