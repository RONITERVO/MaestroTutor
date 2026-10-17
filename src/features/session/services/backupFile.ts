// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {Capacitor} from '@capacitor/core';
import {Filesystem,Directory,Encoding} from '@capacitor/filesystem';
import {Share} from '@capacitor/share';
import {createBrowserFileWriter,type AppFileWriter} from '../../../platform/browser/fileWriter';
export type BackupSaveResult={status:'saved';location?:string;sharingFailed?:boolean}|{status:'shared'}|{status:'cancelled'}|{status:'empty'}|{status:'failed';message:string};
export const isBackupCancellation=(error:unknown)=>error instanceof DOMException&&error.name==='AbortError'||/cancelled|canceled/i.test(error instanceof Error?error.message:String(error));
/** A share-sheet acknowledgement is not a durable recovery copy. Mandatory
 * pre-change backups require a completed desktop/Quest file or Documents write. */
export async function saveBackupFile(name:string,title:string,write:(writer:Pick<AppFileWriter,'write'>)=>Promise<void>,required=false):Promise<Extract<BackupSaveResult,{status:'saved'|'shared'}>>{
 if(!Capacitor.isNativePlatform()){
  const writer=await createBrowserFileWriter(name,'application/x-ndjson','Maestro Backup');
  try{await write(writer);await writer.close();return {status:'saved',location:writer.location?.()};}
  catch(error){try{await writer.abort?.();}catch{}throw error;}
 }
 // An automatically created phone backup must never overwrite an older copy.
 const path=name.replace(/\.(ndjson|jsonl)$/i,'-'+crypto.randomUUID()+'.$1');
 const attempt=async(directory:Directory)=>{
  let written=false,uri:string|undefined,published=false,aborted=false;const pendingPath=path+'.pending';
  const writer:AppFileWriter={
   async write(data){if(!written){const file=await Filesystem.writeFile({path:pendingPath,data,directory,encoding:Encoding.UTF8});uri=file.uri;written=true;}else await Filesystem.appendFile({path:pendingPath,data,directory,encoding:Encoding.UTF8});},
   async close(){if(!uri)throw new Error('Missing file URI for backup');await Filesystem.rename({from:pendingPath,to:path,directory});published=true;uri=(await Filesystem.getUri({path,directory})).uri;},
   async abort(){if(aborted)return;aborted=true;if(!published)await Filesystem.deleteFile({path:pendingPath,directory});},
  };
  try{await write(writer);await writer.close();if(!uri)throw new Error('Missing file URI for backup');return uri;}
  catch(error){try{await writer.abort?.();}catch{}throw error;}
 };
 let uri:string,durable=true;
 try{uri=await attempt(Directory.Documents);}catch(error){if(required)throw error;durable=false;uri=await attempt(Directory.Cache);}
 if(required)return {status:'saved',location:'Documents/'+path};
 try{await Share.share({title,url:uri,dialogTitle:title});}
 catch(error){if(durable)return {status:'saved',location:'Documents/'+path,sharingFailed:true};throw error;}
 return durable?{status:'saved',location:'Documents/'+path}:{status:'shared'};
}
