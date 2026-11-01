// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowGroups.json';
import {validFactValue} from '../../../shared/behaviourFacts';

it('exposes simultaneous native waiting and flowing groups through the shared contract',()=>{
 const {waiting,waitingRain,flowing,receiver}=native;
 for(const [key,value] of Object.entries(native))expect(validFactValue(key==='waitingRain'?'object.container.rain':'object.container.live',value)).toBe(true);
 expect(waiting).toMatchObject({phase:'waiting',contents:{amountMl:400,savedAmountMl:400,transferredMl:0,spilledMl:0}});
 expect(waiting.error).not.toBe('');
 expect(waitingRain).toMatchObject({phase:'waiting',ready:false,exposure:0,collectedMl:0});
 expect(flowing).toMatchObject({phase:'flowing',error:'',contents:{savedAmountMl:400,spilledMl:0}});
 expect(receiver).toMatchObject({phase:'flowing',error:'',contents:{savedAmountMl:0}});
 expect(receiver.contents.amountMl).toBeGreaterThan(0);
 expect(flowing.contents.amountMl+receiver.contents.amountMl).toBeCloseTo(400,8);
 expect(flowing.contents.transferredMl).toBeCloseTo(receiver.contents.amountMl,8);
 expect(flowing.sessionId).toBe(receiver.sessionId);
});
it('recovers an unavailable group without backlog and publishes conserved native amounts',()=>{
 const {recovered,recoveredReceiver,accepted}=native;
 expect(recovered).toMatchObject({phase:'flowing',error:'',contents:{savedAmountMl:400,spilledMl:0}});
 expect(recoveredReceiver.contents.amountMl).toBeGreaterThan(0);
 expect(recoveredReceiver.contents.amountMl).toBeLessThanOrEqual(18.75);
 expect(recovered.contents.amountMl+recoveredReceiver.contents.amountMl).toBeCloseTo(400,8);
 expect(recovered.contents.transferredMl).toBeCloseTo(recoveredReceiver.contents.amountMl,8);
 expect(accepted).toMatchObject({phase:'idle',error:'',contents:{amountMl:recoveredReceiver.contents.amountMl,savedAmountMl:recoveredReceiver.contents.amountMl,transferredMl:0,spilledMl:0}});
 expect(accepted.sessionId).toBe(recoveredReceiver.sessionId);
});
