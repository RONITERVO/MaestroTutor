// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { planSuggestionAftersteps } from './suggestionAfterstepPlan';

describe('explicit afterstep mode policies', () => {
  it.each(['browser-chat', 'browser-live', 'headless'] as const)('does not finalize a tool-only attachment early in %s', mode => {
    const result = planSuggestionAftersteps({ mode, contextText: 'Visible', artifact: null, toolRequest: { tool: 'image', prompt: 'Picture' } });
    expect(result.splitToolMessage).toBeNull();
    expect(result.assistantPatches.some(patch => 'isLoadingArtifact' in patch)).toBe(false);
    expect(result.assistantPatches.length).toBe(mode === 'browser-live' ? 1 : 0);
  });

  it('records the image prompt once when it already equals visible Live text', () => {
    const { assistantPatches } = planSuggestionAftersteps({ mode: 'browser-live', contextText: ' Picture ', artifact: null, toolRequest: { tool: 'image', prompt: 'Picture' } });
    expect(assistantPatches[0].llmRawResponse?.split('\n\n')[0]).toBe('Picture');
    expect(assistantPatches[0].llmRawResponse).not.toContain('Picture\n\nPicture');
  });

  it('keeps browser cleanup distinct from headless retention even without an artifact', () => {
    const common = { contextText: '', artifact: null, toolRequest: null };
    expect(planSuggestionAftersteps({ ...common, mode: 'browser-chat' }).assistantPatches).toEqual([{ isLoadingArtifact: false, artifactLoadStartTime: undefined }]);
    expect(planSuggestionAftersteps({ ...common, mode: 'headless' }).assistantPatches).toEqual([{ isLoadingArtifact: false }]);
  });
});
