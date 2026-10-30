// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TextFileViewer, MiniGameViewer } from '../../features/chat';
import { BookPresentationContext } from './BookPresentationContext';

vi.mock('../../shared/hooks/useAppTranslations', () => ({ useAppTranslations: () => ({ t: (key: string) => key }) }));
afterEach(cleanup);
const source = '<html><body><canvas></canvas><script>void 0;</script></body></html>';
const src = `data:text/html;base64,${btoa(source)}`;

describe('book artifact ownership', () => {
  it('keeps all chat cards inert and exactly one sandboxed document on the practice page', () => {
    const selectArtifact = vi.fn();
    const { container, getByRole } = render(
      <BookPresentationContext.Provider value={{ layout: 'practice', spreadRoot: { current: null }, earlierPageTarget: null, earlierMessageIds: new Set(), selectedId: 'a2', selectArtifact, posters: new Map(), visibleMessageIds: new Set(['a1', 'a2']), historyPageKey: 'a1,a2', isLatestPage: true }}>
        <section data-testid="chat">
          <TextFileViewer src={src} variant="assistant" embedId="a1" fileName="first.html" mimeType="text/html" />
          <TextFileViewer src={src} variant="assistant" embedId="a2" fileName="second.html" mimeType="text/html" />
        </section>
        <MiniGameViewer embedId="book-a2" variant="preview" sourceCode={source} activeOnBook />
      </BookPresentationContext.Provider>,
    );
    expect(container.querySelector('[data-testid="chat"]')!.querySelectorAll('iframe')).toHaveLength(0);
    expect(container.querySelectorAll('iframe')).toHaveLength(1);
    expect(container.querySelector('iframe')!.getAttribute('sandbox')).toBe('allow-scripts');
    fireEvent.click(getByRole('button', { name: 'Open first.html on the right page' }));
    expect(selectArtifact).toHaveBeenCalledWith('a1');
  });
});
