// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useState } from 'react';
import { sessionActivity } from '../browser/sessionActivity';
import type { BookSnapshot } from './bookBridge';
import './questAudience.css';

/** Session-only self-confirmation. No birth date, saved age or identity check. */
export function QuestAudienceGate({ children }: React.PropsWithChildren) {
  const [confirmed, setConfirmed] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [declined, setDeclined] = useState(false);
  useEffect(() => { document.getElementById('splash-screen')?.remove(); }, []);
  useEffect(() => {
    if (accepted) return;
    sessionActivity.requireResume();
    // A lifecycle-only bridge lets native pause the page while a policy opens.
    // Without an acknowledgement the WebView watchdog would destroy the book.
    // No account, media, file, library or room-action capability is installed.
    const refuse = () => false;
    const bridge: NonNullable<Window['maestroBook']> = Object.freeze({
      snapshot: (): BookSnapshot => ({ version: 1, layout: 'conversation', activity: 'idle', audioPaused: true,
        bookmarkMessageId: null, selectedArtifactId: null, historyStart: 0, historyEnd: 0, historyTotal: 0 }),
      lifecycle: (suspended: boolean) => { if (typeof suspended === 'boolean') sessionActivity.setSuspended(suspended); },
      lifecycleState: () => sessionActivity.status(),
      roomSnapshot: () => ({ clientId: '', session: '', request: null }),
      command: refuse, integrityResult: refuse, roomState: refuse, cameraState: () => false, roomCapture: refuse, libraryState: refuse,
      takeFileSelection: refuse, fileExportPoll: () => null, fileExportResult: refuse,
    });
    window.maestroBook = bridge;
    return () => { if (window.maestroBook === bridge) delete window.maestroBook; };
  }, [accepted]);
  // Children are deliberately not mounted before confirmation: no account,
  // microphone, tutor, agent or restored chat hooks run behind the notice.
  if (accepted) return <>{children}</>;
  return <main className="quest-audience-spread" aria-label="Welcome to Maestro Quest">
    <section className="quest-audience-page" aria-labelledby="quest-privacy-title">
      <h1 id="quest-privacy-title">Your Maestro book</h1>
      <p>Your familiar conversation and artifacts live on these pages. You can speak or type, and ask Maestro to work with the room.</p>
      <h2>Before you begin</h2>
      <p>AI requests can include your conversation, attachments and relevant room-object or program details. With managed access, they pass through Maestro to Google Gemini. With your own API key, they go directly to Google.</p>
      <p>A room task handed off from Live can reuse the audio and enabled camera frames already sent during that turn. Task history, including that media, stays with your local chat and can be included in exported backups.</p>
      <p>Room scans support local collisions and movement. Passthrough does not turn on camera uploads. Microphone and room-access permissions are requested separately when needed.</p>
      <a href="https://chatwithmaestro.com/privacy.html">Read the privacy policy in your browser</a>
    </section>
    <section className="quest-audience-page" aria-labelledby="quest-age-title">
      <h1 id="quest-age-title">For adults, 18 and over</h1>
      {declined ? <div role="status"><p>Maestro Quest is only available to adults aged 18 or older.</p><p>You can close the app using the Quest menu. No AI session has started.</p></div> : <>
        <p>Quest v1 is for adult language learners. Confirm your age before opening the chat or using AI features.</p>
        <p>This confirmation is for this book session. We do not ask for your date of birth or save an age record.</p>
        <label className="quest-audience-confirm"><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.currentTarget.checked)} />I am 18 years of age or older.</label>
        <button type="button" disabled={!confirmed} onClick={() => { if (confirmed) setAccepted(true); }}>Open my Maestro book</button>
        <button type="button" onClick={() => setDeclined(true)}>I am under 18</button>
      </>}
      <a href="https://ai.google.dev/gemini-api/terms">Read the Gemini API terms in your browser</a>
    </section>
  </main>;
}
