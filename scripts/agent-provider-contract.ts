// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { isDeepStrictEqual } from 'node:util';
import type { RoomAgentState } from '../src/core-sdk/room/roomAgent';

// Structural input permits incremental migration to explicit NativeObjectReadSet.
// Never fabricate a RoomAgentState to represent those separate reads.
type RoomObjects = Pick<RoomAgentState, 'objects'>;
const saved = (object: RoomAgentState['objects'][number]) => ({
  id: object.id, name: object.name, kind: object.kind, position: object.position,
  scale: object.scale, color: object.color, physics: object.physics,
});
function unchangedOthers(before: RoomObjects, after: RoomObjects, target?: string) {
  for (const object of before.objects.filter(object => object.id !== target)) {
    const current = after.objects.find(candidate => candidate.id === object.id);
    if (!current || !isDeepStrictEqual(saved(current), saved(object))) throw new Error('Provider scenario changed an unrelated object: ' + object.name);
  }
}
export function assertCreatedParityBall(before: RoomObjects, after: RoomObjects, colour: 'blue' | 'red' = 'blue', expectedScale?: number) {
  const added = after.objects.filter(object => !before.objects.some(previous => previous.id === object.id));
  const ball = added[0];
  if (after.objects.length !== before.objects.length + 1 || added.length !== 1 || !ball
    || ball.name !== 'ParityBall' || ball.kind.toLowerCase() !== 'ball'
    || !Number.isFinite(ball.scale) || ball.scale <= 0 || ball.scale >= 1
    || (expectedScale !== undefined && Math.abs(ball.scale - expectedScale) > 0.0001)
    || ball.color.a !== 1 || (colour === 'blue' ? ball.color.b < 0.7 || ball.color.r > 0.2 || ball.color.g > 0.5 : ball.color.r < 0.7 || ball.color.g > 0.3 || ball.color.b > 0.3)) {
    throw new Error('Provider did not create exactly the requested small ' + colour + ' ParityBall.');
  }
  unchangedOthers(before, after);
  return structuredClone(ball);
}
export function assertPaintedParityBall(before: RoomObjects, after: RoomObjects, target: string) {
  const old = before.objects.find(object => object.id === target);
  const ball = after.objects.find(object => object.id === target);
  if (!old || !ball || after.objects.length !== before.objects.length
    || ball.color.r < 0.8 || ball.color.g > 0.2 || ball.color.b > 0.2 || ball.color.a !== 1
    || !isDeepStrictEqual({ ...saved(ball), color: old.color }, saved(old))) {
    throw new Error('Provider did not paint only the existing ParityBall red.');
  }
  unchangedOthers(before, after, target);
  return structuredClone(ball);
}
export function assertSameRoomObjects(expected: RoomObjects, actual: RoomObjects) {
  if (expected.objects.length !== actual.objects.length) throw new Error('Room object count differs from the expected state.');
  unchangedOthers(expected, actual);
}
