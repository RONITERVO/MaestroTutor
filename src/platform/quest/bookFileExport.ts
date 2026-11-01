// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {registerNativeFileWriter,type AppFileWriter} from '../browser/fileWriter';
export const EXPORT_CHUNK_BYTES=24576,EXPORT_MAX_BYTES=256*1024*1024;
type Request={id:string;sequence:number;operation:'open';name:string;mime:string}|{id:string;sequence:number;operation:'chunk';offset:number;data:string}|{id:string;sequence:number;operation:'finish'|'abort';offset:number};
type Response={version:1;id:string;sequence:number;ok:boolean;bytes:number;location?:string;error?:string};
const validName=(name:string)=>name.length>0&&name.length<=128&&!/[\\/:*?"<>|\u0000-\u001f\u007f-\u009f]/.test(name)&&/.+\.(ndjson|jsonl|json|txt)$/i.test(name);
/** Native polls only this trusted document. There is no JS-to-native interface
 * available to opaque artifact frames, and no request contains a path or URI. */
export function createBookFileExport(target:Window,uuid=()=>crypto.randomUUID().replace(/-/g,'')){
 let unregister:(()=>void)|undefined,disposed=false,suspended=false;
 let active:string|null=null,pending:Request|null=null,resolve:((r:Response)=>void)|null=null,reject:((e:Error)=>void)|null=null,timer:number|undefined;
 const clear=()=>{if(timer!==undefined)target.clearTimeout(timer);timer=undefined;pending=null;resolve=reject=null;};
 const fail=(message:string)=>{const no=reject;clear();active=null;no?.(new Error(message));};
 const request=(value:Request):Promise<Response>=>{
  if(disposed||suspended||active!==value.id)return Promise.reject(new Error('Export interrupted. A closing file may have completed; check Downloads/Maestro.'));
  if(pending)return Promise.reject(new Error('Wait for the current export write.'));
  return new Promise((yes,no)=>{pending=value;resolve=yes;reject=no;timer=target.setTimeout(()=>fail('Export could not be confirmed. Check Downloads/Maestro before saving again.'),30000);});
 };
 const open=async(name:string,mime:string):Promise<AppFileWriter>=>{
  if(disposed||suspended||active)throw new Error('Finish the current export or reopen the book.');
  if(!validName(name)||!['application/x-ndjson','application/json','text/plain'].includes(mime))throw new Error('Choose a supported export filename and type.');
  const id=uuid();active=id;let sequence=0,bytes=0,ended=false,busy=false,location:string|undefined;
  const send=async(value:Request)=>{const result=await request(value);if(!result.ok){active=null;ended=true;throw new Error(result.error||'Export failed.');}if(result.bytes!==bytes)throw new Error('Export byte count differs; check Downloads/Maestro.');return result;};
  try{await send({id,sequence:sequence++,operation:'open',name,mime});}catch(e){if(active===id)active=null;throw e;}
  const available=()=>{if(ended||active!==id||busy)throw new Error('Export is closed, interrupted or already writing.');};
  return {
   async write(text){available();busy=true;try{
    // Slice UTF-16 before encoding; never split a surrogate pair or allocate the
    // complete UTF-8 archive. One acknowledged chunk supplies backpressure.
    for(let at=0;at<text.length;){let end=Math.min(at+8192,text.length);if(end<text.length&&text.charCodeAt(end-1)>=0xd800&&text.charCodeAt(end-1)<=0xdbff)end--;
     const chunk=new TextEncoder().encode(text.slice(at,end));if(bytes+chunk.length>EXPORT_MAX_BYTES)throw new Error('Export exceeds 256 MiB; save smaller conversations separately.');const offset=bytes;bytes+=chunk.length;
     await send({id,sequence:sequence++,operation:'chunk',offset,data:btoa(String.fromCharCode(...chunk))});at=end;
    }
   }finally{busy=false;}},
   async close(){available();busy=true;try{const result=await send({id,sequence:sequence++,operation:'finish',offset:bytes});if(!result.location)throw new Error('Downloads did not provide a saved-file receipt.');location=result.location;ended=true;if(active===id)active=null;}finally{busy=false;}},
   async abort(){if(ended||active!==id)return;if(busy)throw new Error('Wait for the current export write before aborting.');try{await request({id,sequence:sequence++,operation:'abort',offset:bytes});}finally{ended=true;if(active===id)active=null;}},
   location:()=>location,
  };
 };
 return {
  poll(){if(disposed||suspended)return null;if(!unregister)unregister=registerNativeFileWriter(open);return pending?{...pending,version:1}:null;},
  receive(value:unknown){if(!value||typeof value!=='object'||!pending)return false;const r=value as Response;
   if(r.version!==1||r.id!==pending.id||r.sequence!==pending.sequence||typeof r.ok!=='boolean'||!Number.isSafeInteger(r.bytes)||r.bytes<0||r.bytes>EXPORT_MAX_BYTES||r.location!==undefined&&(typeof r.location!=='string'||r.location.length>256)||r.error!==undefined&&(typeof r.error!=='string'||r.error.length>1024))return false;
   const yes=resolve;clear();yes?.(r);return true;
  },
  lifecycle(value:boolean){suspended=value;if(value)fail('Export interrupted. A closing file may have completed; check Downloads/Maestro.');},
  dispose(){disposed=true;unregister?.();unregister=undefined;fail('The book closed during export. Check Downloads/Maestro.');},
 };
}
