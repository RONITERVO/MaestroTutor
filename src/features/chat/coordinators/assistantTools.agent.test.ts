// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { createAssistantTools, type AssistantToolPorts } from './assistantTools';
import { planSuggestionAftersteps } from '../../../core-sdk/chat/suggestionAfterstepPlan';

describe('agent uses the existing assistant tool dispatcher', () => {
  it('passes the original source ID without truncating or replacing the request with artifact text', async () => {
    const agent = vi.fn(async () => {});
    const ports = { messagesRef: { current: [{ id: 'split', role: 'assistant', imageUrl: 'data:text/plain,artifact', imageMimeType: 'text/plain' }] },
      updateMessage: vi.fn(), runAgentTask: agent } as unknown as AssistantToolPorts;
    await createAssistantTools(ports).executeAssistantToolRequest('split', { tool: 'agent' }, 'original-tutor-id');
    expect(agent).toHaveBeenCalledWith('original-tutor-id');
  });
  it('keeps an artifact on its original message while the agent owns its own progress message', () => {
    const plan = planSuggestionAftersteps({ mode: 'browser-chat', contextText: 'Reply', artifact: { dataUrl: 'data:text/plain,hello', mimeType: 'text/plain', fileName: 'hello.txt' }, toolRequest: { tool: 'agent' } });
    expect(plan.splitToolMessage).toBeNull();
    expect(plan.assistantPatches[0]).toMatchObject({ imageMimeType: 'text/plain' });
  });
});
