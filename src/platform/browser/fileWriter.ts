// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface AppFileWriter {write:(text:string)=>Promise<void>;close:()=>Promise<void>;abort?:()=>Promise<void>;location?:()=>string|undefined}
export type AppFileWriterFactory=(name:string,mime:string)=>Promise<AppFileWriter>;
let nativeFactory:AppFileWriterFactory|undefined;
/** Only the owning top-document platform adapter registers a native writer. */
export function registerNativeFileWriter(factory:AppFileWriterFactory){nativeFactory=factory;return()=>{if(nativeFactory===factory)nativeFactory=undefined;};}
export function nativeFileWriter(name:string,mime:string):Promise<AppFileWriter>|undefined{return nativeFactory?.(name,mime);}
