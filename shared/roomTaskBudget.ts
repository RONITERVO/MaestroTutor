// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/** Per-request ceilings, shared by the planner and its instructions. */
// A composite edit can discover multiple definitions, guards and asset libraries.
// Bound that read-only work separately; it grants no additional mutations.
export const ROOM_TASK_LIMITS = Object.freeze({planningCalls:32,queryBatches:24,actionBatches:3});
/** Independent catalog reads per provider response, still charged one native read each. */
export const ROOM_DISCOVERY_PLAN_LIMIT = 4;
export type RoomTaskBudget = {planningCalls:number;queryBatches:number;actionBatches:number};
/** Planning calls include the call about to be made; batches count acknowledged dispatches. */
export const remainingRoomTaskBudget=(plans:number,queries:number,actions:number):RoomTaskBudget=>({
 planningCalls:ROOM_TASK_LIMITS.planningCalls-plans,
 queryBatches:ROOM_TASK_LIMITS.queryBatches-queries,
 actionBatches:ROOM_TASK_LIMITS.actionBatches-actions,
});
