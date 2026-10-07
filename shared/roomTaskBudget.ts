// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/** Per-request ceilings, shared by the planner and its instructions. */
// Loading a room, reading its planes and discovering a placement can require
// more than six reads. Allow discovery without increasing mutation allowance.
export const ROOM_TASK_LIMITS = Object.freeze({planningCalls:18,queryBatches:12,actionBatches:3});
export type RoomTaskBudget = {planningCalls:number;queryBatches:number;actionBatches:number};
/** Planning calls include the call about to be made; batches count acknowledged dispatches. */
export const remainingRoomTaskBudget=(plans:number,queries:number,actions:number):RoomTaskBudget=>({
 planningCalls:ROOM_TASK_LIMITS.planningCalls-plans,
 queryBatches:ROOM_TASK_LIMITS.queryBatches-queries,
 actionBatches:ROOM_TASK_LIMITS.actionBatches-actions,
});
