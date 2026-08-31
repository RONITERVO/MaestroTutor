// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Deciding when the silent observer is allowed to spend money.
 *
 * The observer holds a Gemini Live session open the whole time the app is
 * foregrounded and idle, and every microphone packet — plus periodic video —
 * is paid input whether or not anyone is there. A quiet room should not consume
 * user quota or managed-backend credits.
 *
 * This is the first of the layers that stop that:
 *
 *   1. energy      is there enough signal for this to be someone talking
 *   2. transcript  did real words come out (local Whisper — a later layer)
 *   3. cooldown    do not re-trigger immediately on the same noise
 *   4. playback    never trigger on the app's own voice coming back in
 *
 * Layers 1, 3 and 4 live entirely here. Layer 2 remains outside this pure state
 * machine: the caller runs local Whisper, then calls `confirmSpeech()` or
 * `rejectSpeech()`. This keeps model loading and workers out of the gate tests.
 *
 * Kept pure so the thresholds and the hysteresis can be exercised without a
 * microphone, an audio context, or a device.
 */

/** Loudness of one captured packet, measured where the samples already are. */
export interface AudioEnergy {
  /** Root mean square amplitude, 0..1. */
  rms: number;
  /** Largest absolute sample, 0..1. */
  peak: number;
  /** Fraction of samples above the noise floor, 0..1. */
  activeRatio: number;
}

export interface SpeechGateOptions {
  /** Sustained level that counts as someone speaking. */
  minRms?: number;
  /** A single loud sample is not speech on its own, but silence never reaches this. */
  minPeak?: number;
  /** Guards against a click or a knock passing the RMS test. */
  minActiveRatio?: number;
  /** How long the signal must hold up before the gate opens. */
  openAfterMs?: number;
  /** How long to keep streaming after it falls away, so tails are not clipped. */
  hangoverMs?: number;
  /** Silence after closing during which nothing may reopen the gate. */
  cooldownMs?: number;
  /**
   * How long after the app's own audio stops before the microphone is trusted
   * again. Speaker-to-microphone bleed outlives the playback itself, and
   * without this the assistant reliably re-triggers on its own voice.
   */
  playbackSettleMs?: number;
  /**
   * Keep the gate shut after the energy test until a semantic detector (local
   * Whisper in Maestro) confirms that the buffered sound contains real words.
   */
  requireConfirmation?: boolean;
}

export const DEFAULT_SPEECH_GATE: Required<SpeechGateOptions> = {
  // Matches the levels ariadne settled on in the field rather than being
  // derived: quiet room tone sits well under these, a person talking to the
  // device sits well over.
  minRms: 0.004,
  minPeak: 0.025,
  minActiveRatio: 0.01,
  openAfterMs: 200,
  hangoverMs: 900,
  cooldownMs: 400,
  playbackSettleMs: 500,
  requireConfirmation: false,
};

export type GateDecision =
  /** Stream this packet, and anything buffered before it. */
  | { send: true; opening: boolean }
  /** Hold it back. */
  | { send: false; reason: GateHoldReason; closing?: boolean };

export type GateHoldReason =
  | 'below-threshold'
  | 'waiting-for-onset'
  | 'awaiting-confirmation'
  | 'cooldown'
  | 'playback';

/**
 * Whether a packet is loud enough to be worth considering.
 *
 * All three have to hold. RMS alone lets a door slam through; peak alone lets
 * steady hum through; the active ratio is what separates a voice from a click.
 */
export const isSpeechLike = (
  energy: AudioEnergy,
  options: Required<SpeechGateOptions>,
): boolean => (
  energy.rms >= options.minRms
  && energy.peak >= options.minPeak
  && energy.activeRatio >= options.minActiveRatio
);

export class SpeechGate {
  private readonly options: Required<SpeechGateOptions>;
  private open = false;
  /**
   * When the current run of speech-like packets began, or null outside one.
   *
   * Null rather than 0: timestamps here come from a clock that starts at zero,
   * so a sentinel of 0 is indistinguishable from a genuine onset at t=0 and the
   * run never accumulates.
   */
  private onsetAt: number | null = null;
  /** When the signal last looked like speech, for the hangover. */
  private lastSpeechAt = 0;
  /** Silence enforced after the gate closed, so one noise cannot chatter it. */
  private cooldownUntil = 0;
  /** Silence enforced because the app is, or just was, speaking. */
  private playbackMutedUntil = 0;
  /** The energy test passed and the semantic detector owns the next decision. */
  private awaitingConfirmation = false;

  constructor(options: SpeechGateOptions = {}) {
    this.options = { ...DEFAULT_SPEECH_GATE, ...options };
  }

  get isOpen(): boolean {
    return this.open;
  }

  /**
   * Hold the gate shut while the app is speaking, and for a moment afterwards.
   *
   * Called whenever model audio starts or stops playing. This is layer 4, and
   * it is not optional: the microphone hears the speaker, so without it the
   * observer answers itself in a loop and bills for every round.
   */
  notePlayback(isPlaying: boolean, now: number): void {
    if (isPlaying) {
      // Far enough ahead that it only lapses once playback actually stops and
      // this is called again with false.
      this.playbackMutedUntil = Number.POSITIVE_INFINITY;
      this.forceClose();
      return;
    }
    this.playbackMutedUntil = now + this.options.playbackSettleMs;
  }

  /** Shut the gate without waiting for the hangover. */
  forceClose(): void {
    this.open = false;
    this.awaitingConfirmation = false;
    this.onsetAt = null;
    this.lastSpeechAt = 0;
  }

  reset(now = 0): void {
    this.forceClose();
    this.cooldownUntil = now;
    this.playbackMutedUntil = now;
  }

  /**
   * Open a semantically gated candidate after local transcription found words.
   *
   * A result that arrives during playback is rejected even if it came from a
   * previously clean window. That second check closes the async race between a
   * Whisper request and the assistant beginning to speak.
   */
  confirmSpeech(now: number): boolean {
    if (!this.options.requireConfirmation || !this.awaitingConfirmation) return false;
    if (now < this.playbackMutedUntil || now < this.cooldownUntil) {
      this.forceClose();
      return false;
    }
    this.open = true;
    this.awaitingConfirmation = false;
    this.onsetAt = null;
    this.lastSpeechAt = now;
    return true;
  }

  /** Reject a semantic candidate and make a fresh sound earn a fresh check. */
  rejectSpeech(now: number): void {
    this.forceClose();
    this.cooldownUntil = now + this.options.cooldownMs;
  }

  /**
   * Decide what to do with one captured packet.
   *
   * `opening` marks the packet that opened the gate, so the caller knows to
   * flush whatever pre-roll it has been holding: the first syllable arrives
   * before the energy test can possibly have passed, and sending only from the
   * decision point clips every utterance.
   */
  evaluate(energy: AudioEnergy, now: number): GateDecision {
    // Playback is checked first and reported distinctly: "the app is talking"
    // and "this noise just stopped" are different conditions, and collapsing
    // them makes the gate impossible to reason about from a log.
    if (now < this.playbackMutedUntil) {
      this.forceClose();
      return { send: false, reason: 'playback' };
    }

    const speechLike = isSpeechLike(energy, this.options);

    if (!this.open && now < this.cooldownUntil) {
      this.onsetAt = null;
      return { send: false, reason: 'cooldown' };
    }

    if (this.open) {
      if (speechLike) {
        this.lastSpeechAt = now;
        return { send: true, opening: false };
      }
      // Keep streaming through short gaps between words.
      if (now - this.lastSpeechAt <= this.options.hangoverMs) {
        return { send: true, opening: false };
      }
      this.open = false;
      this.onsetAt = null;
      this.cooldownUntil = now + this.options.cooldownMs;
      return { send: false, reason: 'cooldown', closing: true };
    }

    if (!speechLike) {
      if (this.awaitingConfirmation) {
        return { send: false, reason: 'awaiting-confirmation' };
      }
      this.onsetAt = null;
      return { send: false, reason: 'below-threshold' };
    }

    if (this.onsetAt === null) {
      this.onsetAt = now;
    }

    // A sustained run, not a single loud packet: this is what keeps a cough or
    // a chair scrape from opening a paid socket.
    if (now - this.onsetAt < this.options.openAfterMs) {
      return { send: false, reason: 'waiting-for-onset' };
    }

    if (this.options.requireConfirmation) {
      this.awaitingConfirmation = true;
      return { send: false, reason: 'awaiting-confirmation' };
    }

    this.open = true;
    this.lastSpeechAt = now;
    return { send: true, opening: true };
  }
}

/**
 * When audio queued for playback is expected to have finished.
 *
 * The playback worklet reports no start or stop, so "the app is speaking" has to
 * be derived from what has been handed to it. Chunks arrive from the network far
 * faster than they play, so each one extends the end of the queue rather than
 * describing a moment: whichever is later — now, or where the already-queued
 * audio was going to run out — is what this chunk plays after.
 *
 * Taking `now` rather than the previous end alone matters after a gap. Once the
 * queue has drained, a stale end lies in the past, and adding to it would return
 * a moment that has already passed and report the app as silent while it speaks.
 */
export const extendPlaybackEnd = (
  currentEnd: number,
  now: number,
  chunkSamples: number,
  sampleRate: number,
): number => {
  if (chunkSamples <= 0 || sampleRate <= 0) return currentEnd;
  return Math.max(now, currentEnd) + (chunkSamples / sampleRate) * 1000;
};

/**
 * Measure one PCM packet.
 *
 * Deliberately computed where the samples already are, on the main thread,
 * rather than in the worklet: the packet has to cross that boundary regardless,
 * and what this avoids is the base64 encoding, the websocket write and the
 * billing that follow — not the postMessage. Leaving the worklet untouched also
 * keeps the STT capture path, which shares it, exactly as it was.
 *
 * `noiseFloor` is in int16 units; samples below it do not count as active.
 */
export const measureEnergy = (pcm: Int16Array, noiseFloor = 400): AudioEnergy => {
  if (pcm.length === 0) return { rms: 0, peak: 0, activeRatio: 0 };

  let sumSquares = 0;
  let peak = 0;
  let active = 0;

  for (let i = 0; i < pcm.length; i += 1) {
    const sample = pcm[i];
    const magnitude = sample < 0 ? -sample : sample;
    sumSquares += sample * sample;
    if (magnitude > peak) peak = magnitude;
    if (magnitude >= noiseFloor) active += 1;
  }

  const scale = 32768;
  return {
    rms: Math.sqrt(sumSquares / pcm.length) / scale,
    peak: peak / scale,
    activeRatio: active / pcm.length,
  };
};
