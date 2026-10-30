// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface AppFileWriter {write:(text:string)=>Promise<void>;close:()=>Promise<void>;abort?:()=>Promise<void>;location?:()=>string|undefined}
export type AppFileWriterFactory=(name:string,mime:string)=>Promise<AppFileWriter>;
let nativeFactory:AppFileWriterFactory|undefined;
/** Only the owning top-document platform adapter registers a native writer. */
export function registerNativeFileWriter(factory:AppFileWriterFactory){nativeFactory=factory;return()=>{if(nativeFactory===factory)nativeFactory=undefined;};}
export function nativeFileWriter(name:string,mime:string):Promise<AppFileWriter>|undefined{return nativeFactory?.(name,mime);}

/** Desktop save picker or the registered native book writer, with the same close contract. */
export async function createBrowserFileWriter(name:string,mime:string,description:string):Promise<AppFileWriter>{
 const native=nativeFileWriter(name,mime);if(native)return native;
 const picker=(window as Window & {showSaveFilePicker?:(options:unknown)=>Promise<{createWritable:()=>Promise<AppFileWriter>}>}).showSaveFilePicker;
 if(!picker)throw new Error('BROWSER_NOT_SUPPORTED');
 const extension=name.slice(name.lastIndexOf('.'));
 const handle=await picker.call(window,{suggestedName:name,types:[{description,accept:{[mime]:[extension]}}],excludeAcceptAllOption:false});
 const writable=await handle.createWritable();
 // Callbacks such as the task-archive encoder pass write as a bare function.
 // Browser stream methods require their original receiver.
 return {write:async text=>{await writable.write(text);},close:async()=>{await writable.close();},
  ...(writable.abort?{abort:async()=>{await writable.abort!();}}:{})};
}
