// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { roomCaptureImages } from '../../../shared/prompts/room';
import { buildRoomResultInstruction, buildRoomTaskReplyInstruction, buildRoomTaskOutcomeInstruction } from '../../../shared/prompts';
import { runTutorTextTurn, type TutorTextTurnOptions } from '../chat/tutorTextTurn';
import { runRoomActionTask } from './roomAgent';
import type { RoomTaskPorts } from './roomTaskHandoff';

/** Identical provider planning and final tutor reply for browser and headless.
 * Hosts supply identity, journal and native transport; neither implements an agent. */
export function roomTaskProvider(
  source: () => TutorTextTurnOptions,
  usage: (response: any, configuredModel: string, stage: 'planning' | 'reply') => void,
): Pick<RoomTaskPorts, 'run' | 'reply'> {
  return {
    run: (input, lease, control) => runRoomActionTask(input, source(), lease,
      response => usage(response, input.model, 'planning'), control),
    reply: async (input, result, signal) => {
      const turn = await runTutorTextTurn({ ...input,
        currentImages: [...(input.currentImages ?? []), ...roomCaptureImages(result.snapshots)],
        systemInstruction: input.systemInstruction + '\n\n' + buildRoomResultInstruction(result.receipts, result.scene)
          + buildRoomTaskReplyInstruction(result.budgetExhausted)
          + (result.relatedTask ? buildRoomTaskOutcomeInstruction(result.relatedTask, result.needsReview) : ''),
        configOverrides: { maxOutputTokens: 2048 },
      }, { ...source(), signal });
      usage(turn.response, input.model, 'reply');
      return { parsed: turn.parsed, rawResponse: turn.parsed.visibleText };
    },
  };
}
