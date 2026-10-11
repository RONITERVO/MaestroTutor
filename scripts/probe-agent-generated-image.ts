// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import {runHeadlessImageGeneration} from '../src/headless/mediaJourney';
import {beginManagedJourneyBilling,evaluateManagedJourneyBilling,waitForManagedJourneyBillingSettlement} from '../src/headless/managedJourneyBilling';
import {chatImageSources} from '../src/core-sdk/room/chatImageSource';
import type {HeadlessRoomTransport} from '../src/headless/roomTransport';
import {runAgentImportedImageProof} from './probe-agent-imported-image';
type ProofInput=Parameters<typeof runAgentImportedImageProof>[0];
export async function prepareAgentGeneratedImage(client:HeadlessClient,transport:HeadlessRoomTransport,execute:ProofInput['execute'],directory:string,assistantMessageId:string){
 const operationId=client.runtime.ids.create('generated-texture-test');
 const before=client.accessMode==='managed'?await beginManagedJourneyBilling(client,operationId):null;
 let generated:Awaited<ReturnType<typeof runHeadlessImageGeneration>>|undefined,failure:unknown;
 try{generated=await runHeadlessImageGeneration(client,{assistantMessageId,maxAttempts:1,upload:true,
  contextText:'A simple square seamless illustrated terracotta tile texture: flat warm coral squares with cream grout. No text, no people, no perspective. For an imaginary Spanish cafe book cover.'});}catch(error){failure=error;}
 const billing=before?evaluateManagedJourneyBilling(before,await waitForManagedJourneyBillingSettlement(client,operationId),{requirePaidUsage:!failure&&!!generated&&'dataUrlLength' in generated}):{applicable:false,passed:true};
 await writeFile(join(directory,'generated-image-attempt.json'),JSON.stringify({generated,billing,failed:!!failure||!generated||!('dataUrlLength' in generated)},null,2));
 if(failure)throw failure;
 assert.ok(generated&&'dataUrlLength' in generated,'The original app image provider did not return pixels.');
 assert.equal(billing.passed,true);
 const scope=client.state.settings.selectedLanguagePairId!,messages=client.state.chats[scope];
 const image=messages.find(m=>m.id===assistantMessageId)!;assert.equal(image.imageOrigin,'generated');
 const bytes=Buffer.from(image.imageUrl!.split(',')[1],'base64'),hash=createHash('sha256').update(bytes).digest('hex');
 await transport.client.chatImages.update(scope,chatImageSources(messages));
 const baseline=await execute([{action:'rules',rule:{action:'inspect'}}]);
 await writeFile(join(directory,'generated-image.json'),JSON.stringify({boundary:'Real original-app image generation, fresh synthetic learning context. Original bytes offered from chat, not seeded into native storage.',generated,billing,hash,bytes:bytes.length},null,2));
 return {baseline,fixture:{hash,name:'assistant-generated.jpg',width:0,height:0,source:'generated-chat' as const}};
}
export const runAgentGeneratedImageProof=runAgentImportedImageProof;
