import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { ConcealedSpeech } from './ConcealedSpeech';

describe('concealed speech feedback', () => {
  it('renders bounded decorative ink with a stable accessible status and no playback control', () => {
    const markup = renderToStaticMarkup(<ConcealedSpeech progress={10000} />);
    expect((markup.match(/<span /g) || [])).toHaveLength(24);
    expect(markup).toContain('Speech received; preparing transcript');
    expect(markup).toContain('aria-hidden="true"');
    expect(markup).not.toContain('role="button"');
    expect(markup).not.toContain('animate-');
  });
});
