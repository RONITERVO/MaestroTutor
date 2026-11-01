// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Public endpoint data only. This validator never fetches media or owns credentials.
export function validLiveAudioEndpoint(value:unknown):value is string {
 if(typeof value!=='string'||value.length>2048||!value.startsWith('https://')||/[\s\p{Cc}\\?#@]/u.test(value))return false;
 try{const url=new URL(value);return url.protocol==='https:'&&!!url.hostname&&!url.username&&!url.password&&!url.search&&!url.hash;}catch{return false;}
}
