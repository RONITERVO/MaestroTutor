// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  type AudioEnergy,
  DEFAULT_SPEECH_GATE,
  SpeechGate,
  extendPlaybackEnd,
  isSpeechLike,
  measureEnergy,
} from './speechGate';

/**
 * The gate decides when an already-authorized Live socket may send input. The
 * pre-connect monitor uses the same state machine before a socket exists.
 */

const SILENCE: AudioEnergy = { rms: 0.0005, peak: 0.004, activeRatio: 0.001 };
const ROOM_TONE: AudioEnergy = { rms: 0.003, peak: 0.02, activeRatio: 0.008 };
const SPEECH: AudioEnergy = { rms: 0.05, peak: 0.4, activeRatio: 0.6 };
/** One loud sample among quiet ones: a click, a knock, a chair. */
const TRANSIENT: AudioEnergy = { rms: 0.0008, peak: 0.9, activeRatio: 0.002 };

const PACKET_MS = 100;

/** Feed packets at the real 100ms cadence and collect what would be sent. */
const feed = (
  gate: SpeechGate,
  energy: AudioEnergy,
  packets: number,
  startAt: number,
): { sent: number; openedAt: number | null; now: number } => {
  let sent = 0;
  let openedAt: number | null = null;
  let now = startAt;
  for (let i = 0; i < packets; i += 1) {
    const decision = gate.evaluate(energy, now);
    if (decision.send) {
      sent += 1;
      if (decision.opening && openedAt === null) openedAt = now;
    }
    now += PACKET_MS;
  }
  return { sent, openedAt, now };
};

describe('what counts as speech-like', () => {
  it('rejects silence and quiet room tone', () => {
    expect(isSpeechLike(SILENCE, DEFAULT_SPEECH_GATE)).toBe(false);
    expect(isSpeechLike(ROOM_TONE, DEFAULT_SPEECH_GATE)).toBe(false);
  });

  it('accepts someone talking', () => {
    expect(isSpeechLike(SPEECH, DEFAULT_SPEECH_GATE)).toBe(true);
  });

  it('rejects a loud transient', () => {
    // A door slam clears the peak test easily. Requiring sustained energy
    // across the packet is what stops it opening a paid socket.
    expect(isSpeechLike(TRANSIENT, DEFAULT_SPEECH_GATE)).toBe(false);
  });
});

describe('an idle room costs nothing', () => {
  it('sends not one packet through a minute of silence', () => {
    const gate = new SpeechGate();
    const { sent } = feed(gate, SILENCE, 600, 0);
    expect(sent).toBe(0);
    expect(gate.isOpen).toBe(false);
  });

  it('sends nothing through a minute of room tone', () => {
    const gate = new SpeechGate();
    expect(feed(gate, ROOM_TONE, 600, 0).sent).toBe(0);
  });
});

describe('opening', () => {
  it('waits for the signal to hold up before opening', () => {
    const gate = new SpeechGate({ openAfterMs: 200 });
    // The first packets establish an onset; they do not open the gate.
    expect(gate.evaluate(SPEECH, 0)).toEqual({ send: false, reason: 'waiting-for-onset' });
    expect(gate.evaluate(SPEECH, 100)).toEqual({ send: false, reason: 'waiting-for-onset' });
    expect(gate.evaluate(SPEECH, 200)).toEqual({ send: true, opening: true });
  });

  it('does not open on a single loud packet', () => {
    const gate = new SpeechGate();
    gate.evaluate(TRANSIENT, 0);
    gate.evaluate(SILENCE, 100);
    gate.evaluate(SILENCE, 200);
    expect(gate.isOpen).toBe(false);
  });

  it('forgets a false start rather than accumulating towards one', () => {
    // Isolated loud packets separated by silence must never add up to an onset.
    const gate = new SpeechGate();
    for (let i = 0; i < 20; i += 1) {
      gate.evaluate(SPEECH, i * 1000);
      gate.evaluate(SILENCE, i * 1000 + 100);
    }
    expect(gate.isOpen).toBe(false);
  });

  it('flags the opening packet exactly once so pre-roll is flushed once', () => {
    const gate = new SpeechGate();
    let openings = 0;
    let now = 0;
    for (let i = 0; i < 20; i += 1) {
      const decision = gate.evaluate(SPEECH, now);
      if (decision.send && decision.opening) openings += 1;
      now += PACKET_MS;
    }
    expect(openings).toBe(1);
  });

  it('waits for semantic confirmation when the observer requires it', () => {
    const gate = new SpeechGate({ requireConfirmation: true, openAfterMs: 200 });
    expect(gate.evaluate(SPEECH, 0)).toMatchObject({ send: false });
    expect(gate.evaluate(SPEECH, 100)).toMatchObject({ send: false });
    expect(gate.evaluate(SPEECH, 200)).toEqual({
      send: false,
      reason: 'awaiting-confirmation',
    });
    expect(gate.isOpen).toBe(false);
    expect(gate.confirmSpeech(300)).toBe(true);
    expect(gate.isOpen).toBe(true);
  });

  it('requires a fresh onset after semantic rejection', () => {
    const gate = new SpeechGate({ requireConfirmation: true, openAfterMs: 200, cooldownMs: 400 });
    feed(gate, SPEECH, 3, 0);
    gate.rejectSpeech(250);
    expect(gate.evaluate(SPEECH, 300)).toEqual({ send: false, reason: 'cooldown' });
    expect(gate.confirmSpeech(300)).toBe(false);
  });

  it('can seed a gate only after an external semantic trigger', () => {
    const semanticGate = new SpeechGate({ requireConfirmation: true });
    expect(semanticGate.openFromConfirmedTrigger(1_000)).toBe(true);
    expect(semanticGate.isOpen).toBe(true);

    const energyOnlyGate = new SpeechGate();
    expect(energyOnlyGate.openFromConfirmedTrigger(1_000)).toBe(false);
    expect(energyOnlyGate.isOpen).toBe(false);
  });
});

describe('closing', () => {
  it('keeps the default gate open across a natural one-second clause pause', () => {
    const gate = new SpeechGate();
    const { now } = feed(gate, SPEECH, 5, 0);
    expect(gate.evaluate(SILENCE, now + 1000)).toMatchObject({ send: true });
    expect(gate.isOpen).toBe(true);
    expect(gate.evaluate(SILENCE, now + 1600)).toEqual({
      send: false,
      reason: 'cooldown',
      closing: true,
    });
  });

  it('keeps streaming through short gaps between words', () => {
    const gate = new SpeechGate({ hangoverMs: 900 });
    const { now } = feed(gate, SPEECH, 5, 0);
    // A pause mid-sentence must not cut the turn off.
    expect(gate.evaluate(SILENCE, now)).toMatchObject({ send: true });
    expect(gate.evaluate(SILENCE, now + 400)).toMatchObject({ send: true });
    expect(gate.isOpen).toBe(true);
  });

  it('closes once the pause outlasts the hangover', () => {
    const gate = new SpeechGate({ hangoverMs: 900 });
    const { now } = feed(gate, SPEECH, 5, 0);
    expect(gate.evaluate(SILENCE, now + 1000)).toEqual({
      send: false,
      reason: 'cooldown',
      closing: true,
    });
    expect(gate.isOpen).toBe(false);
  });

  it('refuses to reopen immediately, so one noise cannot chatter the gate', () => {
    const gate = new SpeechGate({ hangoverMs: 200, cooldownMs: 400 });
    const { now } = feed(gate, SPEECH, 5, 0);
    gate.evaluate(SILENCE, now + 500);
    expect(gate.evaluate(SPEECH, now + 600)).toEqual({ send: false, reason: 'cooldown' });
  });
});

describe('the app never answers itself', () => {
  it('holds shut while model audio is playing, however loud the room', () => {
    const gate = new SpeechGate();
    feed(gate, SPEECH, 5, 0);
    expect(gate.isOpen).toBe(true);

    gate.notePlayback(true, 1000);
    expect(gate.isOpen).toBe(false);
    // The microphone is hearing the speaker; none of this may be sent back.
    expect(feed(gate, SPEECH, 50, 1000).sent).toBe(0);
  });

  it('stays shut briefly after playback stops, while the room settles', () => {
    const gate = new SpeechGate({ playbackSettleMs: 500 });
    gate.notePlayback(true, 0);
    gate.notePlayback(false, 1000);
    expect(gate.evaluate(SPEECH, 1100)).toEqual({ send: false, reason: 'playback' });
    expect(gate.evaluate(SPEECH, 1400)).toEqual({ send: false, reason: 'playback' });
  });

  it('accepts the user again once the room has settled', () => {
    const gate = new SpeechGate({ playbackSettleMs: 500, openAfterMs: 200 });
    gate.notePlayback(true, 0);
    gate.notePlayback(false, 1000);
    const { sent } = feed(gate, SPEECH, 10, 1600);
    expect(sent).toBeGreaterThan(0);
  });

  it('rejects a delayed semantic result after playback begins', () => {
    const gate = new SpeechGate({ requireConfirmation: true, openAfterMs: 200 });
    feed(gate, SPEECH, 3, 0);
    gate.notePlayback(true, 250);
    expect(gate.confirmSpeech(300)).toBe(false);
    expect(gate.isOpen).toBe(false);
  });
});

describe('overall saving', () => {
  it('sends only around the speech in a mostly quiet stretch', () => {
    const gate = new SpeechGate();
    let sent = 0;
    let now = 0;
    const push = (energy: AudioEnergy, packets: number) => {
      for (let i = 0; i < packets; i += 1) {
        if (gate.evaluate(energy, now).send) sent += 1;
        now += PACKET_MS;
      }
    };

    push(SILENCE, 300);   // 30s of nothing
    push(SPEECH, 30);     // 3s of talking
    push(SILENCE, 300);   // 30s of nothing

    // Without the gate all 630 packets would be billed. With it, only the
    // speech plus its hangover.
    expect(sent).toBeGreaterThan(0);
    expect(sent).toBeLessThan(60);
  });
});

describe('measuring a packet', () => {
  const buildPcm = (fill: (index: number) => number, length = 1600): Int16Array => {
    const pcm = new Int16Array(length);
    for (let i = 0; i < length; i += 1) pcm[i] = fill(i);
    return pcm;
  };

  it('reports nothing for digital silence', () => {
    const energy = measureEnergy(buildPcm(() => 0));
    expect(energy).toEqual({ rms: 0, peak: 0, activeRatio: 0 });
    expect(isSpeechLike(energy, DEFAULT_SPEECH_GATE)).toBe(false);
  });

  it('reports a speech-like packet as speech-like', () => {
    // A 200Hz tone at a third of full scale stands in for a voice.
    const energy = measureEnergy(buildPcm((i) => Math.round(Math.sin(i / 40) * 10000)));
    expect(isSpeechLike(energy, DEFAULT_SPEECH_GATE)).toBe(true);
  });

  it('does not call a single click speech-like', () => {
    const energy = measureEnergy(buildPcm((i) => (i === 0 ? 32000 : 0)));
    expect(energy.peak).toBeGreaterThan(0.9);
    // Loud, but almost no active samples: exactly what the active ratio is for.
    expect(isSpeechLike(energy, DEFAULT_SPEECH_GATE)).toBe(false);
  });

  it('handles an empty packet without dividing by zero', () => {
    expect(measureEnergy(new Int16Array(0))).toEqual({ rms: 0, peak: 0, activeRatio: 0 });
  });
});

describe('knowing when the app has stopped speaking', () => {
  const RATE = 24000;
  /** One second of audio at the playback rate. */
  const ONE_SECOND = RATE;

  it('places a first chunk immediately after now', () => {
    expect(extendPlaybackEnd(0, 5_000, ONE_SECOND, RATE)).toBe(6_000);
  });

  it('queues a burst of chunks end to end rather than on top of each other', () => {
    // Chunks arrive from the network much faster than they play. Three seconds
    // of audio delivered in one burst still takes three seconds to come out.
    let end = 0;
    const now = 5_000;
    for (let i = 0; i < 3; i += 1) end = extendPlaybackEnd(end, now, ONE_SECOND, RATE);
    expect(end).toBe(8_000);
  });

  it('restarts from now when the queue has already drained', () => {
    // The regression this guards: adding to an end that is in the past returns
    // a moment already gone, so the gate would call the app silent mid-sentence.
    const staleEnd = 1_000;
    expect(extendPlaybackEnd(staleEnd, 60_000, ONE_SECOND, RATE)).toBe(61_000);
  });

  it('leaves the end alone for an empty chunk or a nonsense rate', () => {
    expect(extendPlaybackEnd(9_000, 5_000, 0, RATE)).toBe(9_000);
    expect(extendPlaybackEnd(9_000, 5_000, ONE_SECOND, 0)).toBe(9_000);
  });

  it('reports the app as speaking for exactly as long as it has audio queued', () => {
    // How the caller uses this: one second of audio queued at t=1000 means
    // speaking until t=2000, and the gate is shut for all of it.
    const end = extendPlaybackEnd(0, 1_000, ONE_SECOND, RATE);
    expect(1_500 < end).toBe(true);
    expect(2_100 < end).toBe(false);

    const gate = new SpeechGate();
    gate.notePlayback(true, 1_000);
    expect(feed(gate, SPEECH, 10, 1_000).sent).toBe(0);
  });
});
