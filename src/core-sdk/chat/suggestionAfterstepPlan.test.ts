// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { planSuggestionAftersteps } from './suggestionAfterstepPlan';

describe('explicit afterstep mode policies', () => {
  it.each(['browser-chat', 'browser-live'] as const)('finalizes an artifact without a tool in %s', mode => {
    const artifact = { dataUrl: 'data:text/html;base64,PGI+aGk8L2I+', mimeType: 'text/html', fileName: 'lesson.html' };
    const result = planSuggestionAftersteps({ mode, contextText: 'Visible lesson', artifact, toolRequest: null });
    expect(result.splitToolMessage).toBeNull();
    expect(result.assistantPatches[result.assistantPatches.length - 1]).toEqual({
      isLoadingArtifact: false,
      artifactLoadStartTime: undefined,
      imageUrl: artifact.dataUrl,
      imageMimeType: artifact.mimeType,
      attachmentName: artifact.fileName,
      storageOptimizedImageUrl: undefined,
      storageOptimizedImageMimeType: undefined,
      uploadedFileVariants: undefined,
    });
    if (mode === 'browser-chat') expect(result.assistantPatches).toHaveLength(1);
    else {
      expect(result.assistantPatches).toHaveLength(2);
      expect(result.assistantPatches[0].llmRawResponse).toContain('Visible lesson');
      expect(result.assistantPatches[0].llmRawResponse).toContain('lesson.html');
      expect(result.assistantPatches[0].llmRawResponse).toContain('live-suggestion-creator');
    }
  });

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

  it.each(['Show me a picture', 'Picture'])('preserves the explicit image instruction after nonidentical visible text %j', contextText => {
    const { assistantPatches } = planSuggestionAftersteps({ mode: 'browser-live', contextText, artifact: null, toolRequest: { tool: 'image', prompt: 'picture' } });
    expect(assistantPatches[0].llmRawResponse).toContain(`${contextText}\n\npicture`);
  });

  it('keeps browser cleanup distinct from headless retention even without an artifact', () => {
    const common = { contextText: '', artifact: null, toolRequest: null };
    expect(planSuggestionAftersteps({ ...common, mode: 'browser-chat' }).assistantPatches).toEqual([{ isLoadingArtifact: false, artifactLoadStartTime: undefined }]);
    expect(planSuggestionAftersteps({ ...common, mode: 'headless' }).assistantPatches).toEqual([{ isLoadingArtifact: false }]);
  });
});
