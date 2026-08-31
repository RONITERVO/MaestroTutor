// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Embed activation manager.
 *
 * Decides which embeds are allowed to be *live* (a real iframe / rendered PDF)
 * at any moment. Everything else is a placeholder or a poster, so the number of
 * documents and decoded bitmaps in the WebView is bounded by policy rather than
 * by how far the user has scrolled.
 *
 * Design notes:
 *
 * - **One** IntersectionObserver for the whole chat, not one per embed. Every
 *   embed used to observe the shared scroll container, so a single scroll cost
 *   N callbacks and N rect reads. Here a scroll costs one batch and one rAF.
 * - Arbitration never runs synchronously from an observer callback; it is
 *   coalesced into a single rAF so a burst of entries settles once.
 * - Promotion waits for a dwell period and is suppressed during a fling, so
 *   flicking past ten mini-games boots zero of them instead of ten.
 * - Demotion of a fully off-screen embed is immediate: it frees memory at once
 *   and cannot be seen.
 *
 * This module is deliberately framework-free (see useEmbedSlot for the React
 * binding) so it can be driven directly from tests.
 */

import type { EmbedKind, EmbedPhase } from './embedTypes';

/** How long a candidate must stay top-ranked before we boot it. */
const PROMOTION_DWELL_MS = 250;
/** Above this scroll speed we assume a fling and hold off on booting anything. */
const FLING_VELOCITY_PX_PER_MS = 1.2;
/** How long after the last fast scroll sample we keep treating it as a fling. */
const FLING_COOLDOWN_MS = 180;
/**
 * How long an embed the user was engaged with stays alive after scrolling off.
 *
 * Glancing up at the previous message and coming back should not restart a game
 * in progress. It yields immediately to anything visible, so this never costs a
 * slot that something on screen wants; the timer only stops a forgotten game
 * from holding a document open for the rest of the session.
 */
const PIN_OFFSCREEN_GRACE_MS = 20_000;
/** How long we wait for the observer to say anything before assuming it never will. */
const OBSERVER_SILENCE_TIMEOUT_MS = 1_200;

const INTERSECTION_THRESHOLDS = [0, 0.01, 0.15, 0.35, 0.6, 0.85, 0.99];

export interface EmbedBudgets {
  /** Maximum simultaneously live embeds (real iframes / rendered documents). */
  maxLiveEmbeds: number;
  /** Maximum retained poster bitmaps for frozen embeds. 0 disables posters. */
  posterBudget: number;
}

const DEFAULT_BUDGETS: EmbedBudgets = { maxLiveEmbeds: 1, posterBudget: 4 };

interface EmbedRecord {
  id: string;
  kind: EmbedKind;
  element: Element | null;
  /** Fraction of the embed visible in the scroll root, 0..1. */
  visibleFraction: number;
  /** 1 when the embed's centre sits on the root's centre line, 0 at the edge. */
  centeredness: number;
  /** User is actively engaged (playing, scrolling a PDF). Beats visibility. */
  pinned: boolean;
  /** When a pinned embed left the viewport; 0 while it is on screen. */
  pinnedOffscreenAt: number;
  /** When engagement was taken. The expiry clock when visibility is unavailable. */
  pinnedAt: number;
  phase: EmbedPhase;
  /** Registration order, used as a stable tie-break and as the IO-less fallback. */
  seq: number;
  /** Latched at the 0.99 threshold, so it changes rarely rather than per scroll. */
  fullyVisible: boolean;
  onPhase: (phase: EmbedPhase) => void;
  onFullyVisible?: (fullyVisible: boolean) => void;
}

/** An embed is "fully visible" once essentially all of it is inside the root. */
const FULL_VISIBILITY_RATIO = 0.99;

export interface EmbedActivationDebugRecord {
  id: string;
  kind: EmbedKind;
  phase: EmbedPhase;
  visibleFraction: number;
  centeredness: number;
  pinned: boolean;
  observed: boolean;
}

export interface EmbedActivationDebugSnapshot {
  live: string[];
  frozen: string[];
  placeholder: string[];
  posters: number;
  budgets: EmbedBudgets;
  observing: boolean;
  /** A scroll container was nominated for velocity sampling. */
  hasRoot: boolean;
  /** The observer has delivered at least one entry. False here explains a lot. */
  receivedEntries: boolean;
  records: EmbedActivationDebugRecord[];
}

const now = (): number => (typeof performance !== 'undefined' ? performance.now() : Date.now());

class EmbedActivationManager {
  private records = new Map<string, EmbedRecord>();
  private elementIndex = new WeakMap<Element, string>();
  private posters = new Map<string, string>(); // insertion-ordered = LRU
  private budgets: EmbedBudgets = { ...DEFAULT_BUDGETS };
  private observer: IntersectionObserver | null = null;
  private root: Element | null = null;
  private seq = 0;
  private rafHandle = 0;
  private dwellTimer: ReturnType<typeof setTimeout> | null = null;
  private lastScrollTop = 0;
  private lastScrollAt = 0;
  private lastFlingAt = 0;
  private scrollListenerAttached = false;
  /** Whether the observer has ever delivered anything. See assumeVisibleIfSilent. */
  private hasIngested = false;
  private silenceTimer: ReturnType<typeof setTimeout> | null = null;
  private pinExpiryTimer: ReturnType<typeof setTimeout> | null = null;

  // ---------------------------------------------------------------- lifecycle

  /**
   * Tell the manager which element the chat *intends* to scroll.
   *
   * Used only to sample scroll velocity for fling suppression — visibility is
   * measured against the viewport (see ensureObserver), so activation no longer
   * depends on this element actually being the scroller.
   */
  setRoot(root: Element | null): void {
    if (this.root === root) return;
    this.detachScrollListener();
    this.root = root;
    this.attachScrollListener();
    this.schedule();
  }

  setBudgets(budgets: Partial<EmbedBudgets>): void {
    const next: EmbedBudgets = {
      maxLiveEmbeds: Math.max(1, Math.floor(budgets.maxLiveEmbeds ?? this.budgets.maxLiveEmbeds)),
      posterBudget: Math.max(0, Math.floor(budgets.posterBudget ?? this.budgets.posterBudget)),
    };
    if (next.maxLiveEmbeds === this.budgets.maxLiveEmbeds && next.posterBudget === this.budgets.posterBudget) {
      return;
    }
    this.budgets = next;
    this.trimPosters();
    this.schedule();
  }

  getBudgets(): EmbedBudgets {
    return { ...this.budgets };
  }

  /**
   * Register an embed. Returns an unregister function.
   *
   * Re-registering the same id (React StrictMode double-invoke, or a re-mount
   * after a key change) replaces the previous record and keeps the current
   * phase, so a live embed is not torn down and rebooted by a remount.
   */
  register(options: {
    id: string;
    kind: EmbedKind;
    element: Element | null;
    onPhase: (phase: EmbedPhase) => void;
    onFullyVisible?: (fullyVisible: boolean) => void;
  }): () => void {
    const { id, kind, element, onPhase, onFullyVisible } = options;
    const previous = this.records.get(id);

    if (previous?.element && previous.element !== element) {
      this.observer?.unobserve(previous.element);
    }

    const record: EmbedRecord = {
      id,
      kind,
      element,
      visibleFraction: previous?.visibleFraction ?? 0,
      centeredness: previous?.centeredness ?? 0,
      pinned: previous?.pinned ?? false,
      pinnedOffscreenAt: previous?.pinnedOffscreenAt ?? 0,
      pinnedAt: previous?.pinnedAt ?? 0,
      phase: previous?.phase ?? (this.posters.has(id) ? 'frozen' : 'placeholder'),
      seq: previous?.seq ?? this.seq++,
      fullyVisible: previous?.fullyVisible ?? false,
      onPhase,
      onFullyVisible,
    };
    this.records.set(id, record);

    if (element) {
      this.elementIndex.set(element, id);
      this.ensureObserver();
      this.observer?.observe(element);
    }
    // With no observer yet — the root has not been set, or the platform has no
    // IntersectionObserver at all — assume visible rather than leaving every
    // embed inert. A silent "nothing ever runs" is a far worse failure than a
    // brief over-eager guess, and the observer corrects this on its first batch,
    // well inside the promotion dwell.
    if (!this.observer) record.visibleFraction = 1;

    this.schedule();
    this.armSilenceFallback();
    onPhase(record.phase);
    onFullyVisible?.(record.fullyVisible);

    return () => this.unregister(id, element);
  }

  private unregister(id: string, element: Element | null): void {
    const record = this.records.get(id);
    if (!record) return;
    // A re-register may already have replaced this record with a new element.
    if (element && record.element !== element) return;

    if (record.element) this.observer?.unobserve(record.element);
    this.records.delete(id);
    this.schedule();
  }

  // ------------------------------------------------------------------ pinning

  /**
   * Pin an embed the user is actively engaged with. A pinned embed outranks
   * everything visible, so an in-progress game cannot be evicted out from under
   * the player by an embed that happens to be more centred.
   *
   * Engagement is **exclusive**: you can only be interacting with one thing, and
   * two pinned records competing for a single slot would resolve by an
   * arbitrary tie-break rather than by what the user last asked for. Pinning
   * therefore releases every other pin.
   */
  setPinned(id: string, pinned: boolean): void {
    const record = this.records.get(id);
    if (!record) return;

    if (pinned) {
      for (const other of this.records.values()) {
        if (other === record || !other.pinned) continue;
        other.pinned = false;
        other.pinnedOffscreenAt = 0;
      }
    } else if (!record.pinned) {
      return;
    }

    record.pinned = pinned;
    record.pinnedAt = pinned ? now() : 0;
    record.pinnedOffscreenAt = pinned && record.visibleFraction <= 0 ? now() : 0;

    // Arbitration is event-driven, so without a timer a pin taken in a quiet
    // chat would never be reconsidered and would outlive its own grace period.
    if (this.pinExpiryTimer !== null) clearTimeout(this.pinExpiryTimer);
    this.pinExpiryTimer = pinned
      ? setTimeout(() => { this.pinExpiryTimer = null; this.arbitrate(); }, PIN_OFFSCREEN_GRACE_MS + 100)
      : null;

    // Engagement is an explicit user action; apply it without dwell.
    this.clearDwell();
    this.arbitrate();
  }

  // ------------------------------------------------------------------ posters

  /**
   * Hand the manager a poster for a frozen embed. Ownership of the blob URL
   * transfers to the manager, which revokes it on eviction.
   */
  setPoster(id: string, url: string): void {
    if (this.budgets.posterBudget <= 0) {
      revokeObjectUrl(url);
      return;
    }
    const existing = this.posters.get(id);
    if (existing && existing !== url) revokeObjectUrl(existing);
    this.posters.delete(id);
    this.posters.set(id, url);
    this.trimPosters();

    const record = this.records.get(id);
    if (record && record.phase === 'placeholder') this.setPhase(record, 'frozen');
  }

  getPoster(id: string): string | undefined {
    return this.posters.get(id);
  }

  /**
   * Whether it is worth capturing posters at all. On the low tier the budget is
   * zero, and capturing a frame we would immediately throw away is pure cost.
   */
  postersEnabled(): boolean {
    return this.budgets.posterBudget > 0;
  }

  private trimPosters(): void {
    while (this.posters.size > this.budgets.posterBudget) {
      const oldest = this.posters.keys().next();
      if (oldest.done) break;
      this.dropPoster(oldest.value);
    }
  }

  private dropPoster(id: string): void {
    const url = this.posters.get(id);
    if (url) revokeObjectUrl(url);
    this.posters.delete(id);
    const record = this.records.get(id);
    if (record && record.phase === 'frozen') this.setPhase(record, 'placeholder');
  }

  // -------------------------------------------------------------- arbitration

  private supportsObserver(): boolean {
    return typeof IntersectionObserver !== 'undefined';
  }

  /**
   * Create the observer if we do not have one.
   *
   * The root is deliberately the viewport (`root: null`) rather than the chat's
   * scroll container. Passing that container looked right and was badly wrong:
   * its `overflow-y: auto` never engages, because the flex chain above it only
   * ever sets a *minimum* height, so it grows to the full conversation — 9717px
   * against an 800px screen on a real device. Every embed then measured as
   * fully visible, "centred" meant the middle of the whole transcript rather
   * than of the screen, and scrolling the document produced no entries at all,
   * so nothing was ever promoted or released by scrolling.
   *
   * A viewport root cannot drift out of sync with the layout that way, and it
   * still accounts for clipping by any intermediate scroll container, so it
   * stays correct if the chat later gains a real inner scroller.
   *
   * Creating it lazily on first registration also removes the ordering hazard:
   * React runs child effects before parent ones, so embeds register before
   * ChatInterface could hand us anything.
   */
  private ensureObserver(): void {
    if (this.observer || !this.supportsObserver()) return;

    this.observer = new IntersectionObserver(
      (entries) => this.ingest(entries),
      { root: null, threshold: INTERSECTION_THRESHOLDS },
    );
    for (const record of this.records.values()) {
      if (record.element) this.observer.observe(record.element);
    }
  }

  /**
   * Guarantee that something runs even if the observer never speaks.
   *
   * Visibility-driven activation has one catastrophic failure mode: if no entry
   * ever arrives, every embed sits at zero visibility, nothing is ever promoted,
   * and the whole chat is permanently inert — artifacts that only start when
   * tapped, and an engaged one that is never reported off screen so it holds the
   * slot for the session. That is far worse than being slightly over-eager, and
   * it is not a state we can rule out across every WebView and every DOM shape
   * an attachment might sit in.
   *
   * So: if the observer has delivered nothing at all shortly after embeds have
   * registered, assume they are visible and let normal arbitration proceed. A
   * working observer corrects this on its first batch, long before the timer.
   */
  private armSilenceFallback(): void {
    if (this.hasIngested || this.silenceTimer !== null) return;

    this.silenceTimer = setTimeout(() => {
      this.silenceTimer = null;
      if (this.hasIngested || this.records.size === 0) return;

      // Deliberately not dev-gated: this only fires when something is wrong,
      // it fires once, and a release build on a real device is exactly where we
      // need to see it (Capacitor forwards console to logcat).
      console.warn(
        '[embedActivation] no IntersectionObserver entries arrived; falling back to '
        + 'assuming embeds are visible. Activation will not follow scrolling.',
      );
      for (const record of this.records.values()) {
        record.visibleFraction = 1;
        record.centeredness = 0;
      }
      this.arbitrate();
    }, OBSERVER_SILENCE_TIMEOUT_MS);
  }

  private ingest(entries: IntersectionObserverEntry[]): void {
    this.hasIngested = true;
    if (this.silenceTimer !== null) {
      clearTimeout(this.silenceTimer);
      this.silenceTimer = null;
    }

    for (const entry of entries) {
      const record = this.findByElement(entry.target);
      if (!record) continue;

      record.visibleFraction = entry.isIntersecting ? entry.intersectionRatio : 0;
      record.centeredness = computeCenteredness(entry);

      const fullyVisible = record.visibleFraction >= FULL_VISIBILITY_RATIO;
      if (fullyVisible !== record.fullyVisible) {
        record.fullyVisible = fullyVisible;
        record.onFullyVisible?.(fullyVisible);
      }

      if (record.visibleFraction > 0) {
        record.pinnedOffscreenAt = 0;
        continue;
      }

      if (record.pinned) {
        // Start the grace clock, but leave the decision to arbitration: an
        // engaged embed yields to anything visible, and only survives while
        // nothing else wants the slot.
        if (record.pinnedOffscreenAt === 0) record.pinnedOffscreenAt = now();
        continue;
      }

      // Out of view and not engaged: free it now. No dwell — nobody can see the
      // change, and holding the slot open is exactly the leak we are fixing.
      if (record.phase === 'live') {
        this.setPhase(record, this.posters.has(record.id) ? 'frozen' : 'placeholder');
      }
    }
    this.schedule();
  }

  private findByElement(element: Element): EmbedRecord | undefined {
    const id = this.elementIndex.get(element);
    if (id === undefined) return undefined;
    const record = this.records.get(id);
    return record?.element === element ? record : undefined;
  }

  private schedule(): void {
    if (this.rafHandle) return;
    const run = () => {
      this.rafHandle = 0;
      this.arbitrate();
    };
    this.rafHandle = typeof requestAnimationFrame === 'function'
      ? requestAnimationFrame(run)
      : (setTimeout(run, 16) as unknown as number);
  }

  private score(record: EmbedRecord): number {
    if (record.pinned) {
      // On screen and engaged: outranks everything, so an in-progress game
      // cannot be evicted by something that merely drifted closer to centre.
      if (record.visibleFraction > 0) return 10_000 + record.visibleFraction;
      // Engaged but scrolled off: worth less than any visible embed, so it
      // yields the moment something on screen needs the slot.
      return 0.01;
    }
    if (record.visibleFraction <= 0) return -1;
    return record.visibleFraction * 100 + record.centeredness * 20;
  }

  /** Release engagement that has been off screen long enough to be forgotten. */
  private expireStalePins(): void {
    const at = now();
    for (const record of this.records.values()) {
      if (!record.pinned) continue;

      // Without visibility data we cannot tell that the user scrolled away, so
      // "off screen" never arrives and the pin would hold the only live slot for
      // the rest of the session — the user taps one artifact and no other will
      // ever start again. Fall back to expiring from when engagement was taken.
      const since = this.hasIngested ? record.pinnedOffscreenAt : record.pinnedAt;
      if (since === 0 || at - since < PIN_OFFSCREEN_GRACE_MS) continue;

      record.pinned = false;
      record.pinnedOffscreenAt = 0;
      record.pinnedAt = 0;
    }
  }

  /** Ids that *should* be live right now, best first. */
  private desiredLive(): string[] {
    const ranked = [...this.records.values()]
      .map((record) => ({ record, score: this.score(record) }))
      .filter((entry) => entry.score >= 0)
      .sort((a, b) => (b.score - a.score) || (a.record.seq - b.record.seq));

    return ranked.slice(0, this.budgets.maxLiveEmbeds).map((entry) => entry.record.id);
  }

  private isFlinging(): boolean {
    return now() - this.lastFlingAt < FLING_COOLDOWN_MS;
  }

  private arbitrate(): void {
    if (this.records.size === 0) {
      this.clearDwell();
      return;
    }

    this.expireStalePins();

    const desired = new Set(this.desiredLive());
    const currentLive = new Set(
      [...this.records.values()].filter((record) => record.phase === 'live').map((record) => record.id),
    );

    const wantsPromotion = [...desired].some((id) => !currentLive.has(id));
    const hasPin = [...this.records.values()].some((record) => record.pinned);

    // Booting a document mid-fling is the worst possible moment for it: hold the
    // current assignment and re-arbitrate once the scroll settles.
    if (wantsPromotion && this.isFlinging() && !hasPin) {
      this.armDwell();
      return;
    }

    // Demotions are always safe to apply: they only ever free resources.
    for (const record of this.records.values()) {
      if (record.phase === 'live' && !desired.has(record.id)) {
        this.setPhase(record, this.posters.has(record.id) ? 'frozen' : 'placeholder');
      }
    }

    if (wantsPromotion && !hasPin) {
      // Require the candidate set to stay stable for a beat before booting.
      this.armDwell();
      return;
    }

    this.clearDwell();
    this.applyPromotions(desired);
  }

  /**
   * Make the world match `desired` in both directions.
   *
   * Demoting here as well as in arbitrate() is deliberate belt-and-braces: this
   * is also reached from the dwell timer, and the budget invariant should not
   * depend on tracing which call path got us here.
   */
  private applyPromotions(desired: Set<string>): void {
    for (const record of this.records.values()) {
      if (desired.has(record.id)) {
        if (record.phase !== 'live') this.setPhase(record, 'live');
        continue;
      }
      const next: EmbedPhase = this.posters.has(record.id) ? 'frozen' : 'placeholder';
      if (record.phase !== next) this.setPhase(record, next);
    }
  }

  private armDwell(): void {
    this.clearDwell();
    this.dwellTimer = setTimeout(() => {
      this.dwellTimer = null;
      if (this.isFlinging()) {
        this.armDwell();
        return;
      }
      this.applyPromotions(new Set(this.desiredLive()));
    }, PROMOTION_DWELL_MS);
  }

  private clearDwell(): void {
    if (this.dwellTimer === null) return;
    clearTimeout(this.dwellTimer);
    this.dwellTimer = null;
  }

  private setPhase(record: EmbedRecord, phase: EmbedPhase): void {
    if (record.phase === phase) return;
    record.phase = phase;
    record.onPhase(phase);
  }

  // ------------------------------------------------------------ scroll velocity

  /**
   * Read the offset of whatever actually scrolled.
   *
   * Taken from the event target rather than by probing both the document and
   * the nominated container: reading a second element's scrollTop can force a
   * layout flush, and this runs on every scroll event. Profiling on device had
   * that probe at 2.6% of CPU during a scroll, which is more than the whole
   * activation system should ever cost.
   */
  private scrollOffsetOf(target: EventTarget | null): number {
    if (!target || target === document || target === window) {
      return typeof window !== 'undefined' ? window.scrollY || 0 : 0;
    }
    return (target as Element).scrollTop ?? 0;
  }

  private handleScroll = (event: Event): void => {
    const at = now();
    const top = this.scrollOffsetOf(event.target);
    const elapsed = at - this.lastScrollAt;
    if (elapsed > 0 && this.lastScrollAt > 0) {
      const velocity = Math.abs(top - this.lastScrollTop) / elapsed;
      if (velocity > FLING_VELOCITY_PX_PER_MS) this.lastFlingAt = at;
    }
    this.lastScrollTop = top;
    this.lastScrollAt = at;
  };

  /**
   * Listen at the window in the capture phase.
   *
   * Scroll events do not bubble, so listening on the element we were handed
   * misses the case that actually occurs here — the document being the thing
   * that scrolls. Capturing at the window catches every scroller regardless of
   * which one the layout ends up using.
   */
  private attachScrollListener(): void {
    if (typeof window === 'undefined' || this.scrollListenerAttached) return;
    this.lastScrollTop = this.scrollOffsetOf(null);
    this.lastScrollAt = now();
    window.addEventListener('scroll', this.handleScroll, { capture: true, passive: true });
    this.scrollListenerAttached = true;
  }

  private detachScrollListener(): void {
    if (typeof window !== 'undefined' && this.scrollListenerAttached) {
      window.removeEventListener('scroll', this.handleScroll, { capture: true });
    }
    this.scrollListenerAttached = false;
    this.lastScrollAt = 0;
    this.lastFlingAt = 0;
  }

  // ----------------------------------------------------------------- teardown

  /** Release every observer, timer and poster. Used on chat teardown and by tests. */
  reset(): void {
    this.clearDwell();
    if (this.silenceTimer !== null) clearTimeout(this.silenceTimer);
    this.silenceTimer = null;
    if (this.pinExpiryTimer !== null) clearTimeout(this.pinExpiryTimer);
    this.pinExpiryTimer = null;
    this.hasIngested = false;
    if (this.rafHandle && typeof cancelAnimationFrame === 'function') cancelAnimationFrame(this.rafHandle);
    this.rafHandle = 0;
    this.observer?.disconnect();
    this.observer = null;
    this.detachScrollListener();
    this.root = null;
    this.records.clear();
    for (const url of this.posters.values()) revokeObjectUrl(url);
    this.posters.clear();
    this.budgets = { ...DEFAULT_BUDGETS };
    this.seq = 0;
  }

  /** Snapshot for the dev overlay and for regression tests. */
  debugSnapshot(): EmbedActivationDebugSnapshot {
    const live: string[] = [];
    const frozen: string[] = [];
    const placeholder: string[] = [];
    const records: EmbedActivationDebugRecord[] = [];

    for (const record of this.records.values()) {
      if (record.phase === 'live') live.push(record.id);
      else if (record.phase === 'frozen') frozen.push(record.id);
      else placeholder.push(record.id);

      records.push({
        id: record.id,
        kind: record.kind,
        phase: record.phase,
        visibleFraction: Math.round(record.visibleFraction * 100) / 100,
        centeredness: Math.round(record.centeredness * 100) / 100,
        pinned: record.pinned,
        observed: record.element !== null,
      });
    }

    return {
      live,
      frozen,
      placeholder,
      posters: this.posters.size,
      budgets: { ...this.budgets },
      observing: this.observer !== null,
      // The two fields that tell you whether visibility is working at all: a
      // root but no entries means the observer is attached and silent.
      hasRoot: this.root !== null,
      receivedEntries: this.hasIngested,
      records,
    };
  }
}

/**
 * 1 when the embed's centre sits on the root's centre line, falling to 0 at the
 * root's edge. Breaks ties between two equally-visible embeds in favour of the
 * one the user is actually looking at.
 */
const computeCenteredness = (entry: IntersectionObserverEntry): number => {
  const root = entry.rootBounds;
  if (!root || root.height <= 0) return 0;
  const elementCentre = entry.boundingClientRect.top + entry.boundingClientRect.height / 2;
  const rootCentre = root.top + root.height / 2;
  const distance = Math.abs(elementCentre - rootCentre);
  return Math.max(0, 1 - distance / (root.height / 2));
};

const revokeObjectUrl = (url: string): void => {
  if (!url.startsWith('blob:')) return;
  try {
    URL.revokeObjectURL(url);
  } catch {
    /* the URL may already be gone; nothing to do */
  }
};

export const embedActivation = new EmbedActivationManager();

// Exposed in release builds too, not just dev: these budgets are only ever
// verified on real low-end devices over chrome://inspect, and a read-only
// snapshot function is a negligible thing to ship for that.
if (typeof window !== 'undefined') {
  (window as unknown as Record<string, unknown>).__EMBED_DEBUG__ = () => embedActivation.debugSnapshot();
}
