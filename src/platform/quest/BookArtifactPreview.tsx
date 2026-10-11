// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useBookPresentation } from './BookPresentationContext';

export function BookArtifactPreview({ messageId, title }: { messageId: string; title: string }) {
  const book = useBookPresentation();
  const poster = book?.posters.get(messageId);
  return (
    <button type="button" className="quest-artifact-preview" onClick={() => book?.selectArtifact(messageId)} aria-label={`Open ${title} on the right page`}>
      {poster ? <img src={poster} alt="" /> : (
        <svg viewBox="0 0 360 200" aria-hidden="true">
          <rect x="1" y="1" width="358" height="198" rx="10" fill="#f8ecd4" stroke="#756b5c" />
          <path d="M48 44h155M48 58h210M48 72h184M48 144h122M48 158h176" stroke="#b3a491" strokeWidth="4" strokeLinecap="round" />
          <circle cx="180" cy="104" r="29" fill="#2b8d88" />
          <path d="m173 90 23 14-23 14z" fill="#fff0d2" />
        </svg>
      )}
      <span>{title}</span><small>{book?.selectedId === messageId ? 'Open on right page' : 'Open on right page →'}</small>
    </button>
  );
}
