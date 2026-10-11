// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface InlineImage {mimeType:'image/jpeg';data:string;label:string}
export function jpegDimensions(bytes:Uint8Array):{width:number;height:number}|null {
 if(bytes.length<4||bytes[0]!==255||bytes[1]!==216||bytes[bytes.length-2]!==255||bytes[bytes.length-1]!==217)return null;
 let offset=2;
 while(offset+3<bytes.length){
  if(bytes[offset++]!==255)return null;while(bytes[offset]===255)offset++;const marker=bytes[offset++];
  if(marker===218||marker===217)return null;
  if(marker===216||marker===1||marker>=208&&marker<=215)continue;
  const size=bytes[offset]*256+bytes[offset+1];if(size<2||offset+size>bytes.length)return null;
  if([192,193,194].includes(marker)){if(size<8)return null;return{height:bytes[offset+3]*256+bytes[offset+4],width:bytes[offset+5]*256+bytes[offset+6]};}
  offset+=size;
 }
 return null;
}
/** Bounded inline image inputs; pixels are data, not URLs or executable content. */
export function validateInlineImages(images:InlineImage[]):void {
 if(!Array.isArray(images)||images.length>4)throw new Error('Too many inline images.');
 let total=0;
 for(const image of images){
  if(!image||image.mimeType!=='image/jpeg'||typeof image.data!=='string'||image.data.length>131072||!image.data.length||image.data.length%4!==0||!/^[A-Za-z0-9+/]+={0,2}$/.test(image.data)||typeof image.label!=='string'||!image.label.length||image.label.length>1024)throw new Error('Invalid inline image.');
  const bytes=Uint8Array.from(atob(image.data),c=>c.charCodeAt(0));const size=jpegDimensions(bytes);total+=bytes.length;
  if(!size||size.width<1||size.height<1||size.width>2048||size.height>2048||total>393216)throw new Error('Inline image dimensions or byte budget exceeded.');
 }
}
