/** Silent ink marks: no provisional words, animation loop, or per-frame React updates. */
export const ConcealedSpeech = ({ progress, label = 'Speech received; preparing transcript' }: { progress: number; label?: string }) => (
  <div role="status" aria-label={label} className="py-2" data-testid="concealed-speech">
    <div aria-hidden="true" className="flex flex-wrap items-center gap-x-3 gap-y-3 opacity-40" style={{ maxWidth: '28em', minHeight: '1.8em' }}>
      {Array.from({ length: Math.max(3, Math.min(24, progress)) }, (_, index) => (
        <span key={index} style={{ display: 'inline-block', width: `${[2.6, 4.1, 1.8, 3.3, 2.2][index % 5]}em`, height: '0.48em', borderRadius: '40% 16% 30% 12%', background: 'currentColor', transform: `rotate(${index % 2 ? -1 : 1}deg)` }} />
      ))}
    </div>
  </div>
);
