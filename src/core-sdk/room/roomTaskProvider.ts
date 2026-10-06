// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { roomCaptureImages } from '../../../shared/prompts/room';
import { buildRoomResultInstruction, buildRoomTaskReplyInstruction, buildRoomTaskOutcomeInstruction, buildRoomTaskReplyRequest } from '../../../shared/prompts';
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
      // Only the result text may be corrected. Native planning/dispatch has ended;
      // accepted or uncertain effects must never be repeated to obtain a reply.
      let rejectedReply: string | undefined;
      for (let attempt = 0; attempt < 2; attempt++) {
        signal.throwIfAborted();
        const turn = await runTutorTextTurn({ ...input,
          prompt: buildRoomTaskReplyRequest(input.prompt, rejectedReply),
          currentImages: [...(input.currentImages ?? []), ...roomCaptureImages(result.snapshots)],
          systemInstruction: input.systemInstruction + '\n\n' + buildRoomResultInstruction(result.receipts, result.scene)
            + buildRoomTaskReplyInstruction(result.budgetExhausted)
            + (result.relatedTask ? buildRoomTaskOutcomeInstruction(result.relatedTask, result.needsReview) : ''),
          configOverrides: { maxOutputTokens: 2048 },
        }, { ...source(), signal });
        usage(turn.response, input.model, 'reply');
        signal.throwIfAborted();
        if (!roomReplyHasToolRequest(turn.rawResponse)) return { parsed: turn.parsed, rawResponse: turn.parsed.visibleText };
        rejectedReply = turn.rawResponse;
      }
      throw new Error('The result reply proposed another tool. Recorded room actions remain available; they were not repeated.');
    },
  };
}

/** Tool fences are not valid result prose, even when malformed or hidden by parsing. */
export const roomReplyHasToolRequest = (text: string): boolean => /(?:`{3,}|~{3,})[ \t]*maestro-tool\b/i.test(text);
