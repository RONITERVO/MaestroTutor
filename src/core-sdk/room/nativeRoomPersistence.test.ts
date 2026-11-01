// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/roomPersistence.json';
import {validFactValue} from '../../../shared/behaviourFacts';
it('shares native pending, confirmed, held and failed checkpoint observations with the book and agent',()=>{
 for(const value of Object.values(native))expect(validFactValue('room.persistence',value)).toBe(true);
 for(const store of ['room','behaviours'] as const){
  expect(native.saving[store]).toMatchObject({phase:'saving',pending:true});
  expect(native.saved[store]).toMatchObject({phase:'idle',pending:false,dirty:false,error:''});
  expect(native.failed[store]).toMatchObject({phase:'unconfirmed',pending:false,dirty:true});
  expect(native.failed[store].error).not.toBe('');
  expect(native.held[store].dispatchHeld).toBe(true);
 }
 expect(native.newer.room).toMatchObject({phase:'unsaved',dirty:true,pending:false});
 expect(native.temporary.room).toMatchObject({phase:'temporary',pending:false});
 expect(native.recovered.room).toMatchObject({phase:'idle',pending:false,dirty:false,error:''});
 expect(validFactValue('room.persistence',{...native.saved,room:{...native.saved.room,pending:'false'}})).toBe(false);
});
