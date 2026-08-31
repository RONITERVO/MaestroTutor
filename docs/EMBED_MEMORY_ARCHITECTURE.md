# Embed memory & layout stability architecture

Branch: `perf/single-live-embed`

> **Status: implemented.** Sections 1–3 are the analysis and the decision record;
> §4 tracks what shipped. One item from the original plan — `content-visibility`
> on message bubbles — was deliberately **not** shipped; see §4, Phase 4.

Goal: at most one live embed (iframe / PDF / scripted SVG) alive at a time, without
the chat ever shifting when embeds mount, unmount, boot, or resize — and without
losing the "part of the notebook page" feel.

Driver: Google Play's new quality requirements set thresholds on **dynamic memory
usage** and **bitmap usage**, on top of the Android memory limits. Our current
attachment pipeline fails both by construction, and the same construction is what
makes low-end scrolling janky.

---

## 1. What we actually do today

### 1.1 Embeds are immortal

`MiniGameViewer.tsx:174-197` — `hasIntersected` is a sticky `useState(false)` that
flips to `true` the first time the container comes within `600px` of the viewport
and **is never reset**. The `<iframe>` at `MiniGameViewer.tsx:592` is mounted from
that moment until the message scrolls out of `maxVisibleMessages` (default **50**,
`settingsSlice.ts:24`) or the app is killed.

So the live-iframe count is not "what's on screen", it is "every mini-game the user
has ever scrolled past in this session, up to 50". Each one is a full WebView
document: its own JS heap, its own `requestAnimationFrame` loop, its own canvas
backing store, its own `MutationObserver` + `ResizeObserver` (`miniGameAttachment.ts:625`).

`ChatInterface.tsx:729` renders `messagesToRender.map(...)` with no virtualization,
so nothing above ever unmounts either.

### 1.2 Every embed also runs its own observer stack

Per `MiniGameViewer`: 2 × `IntersectionObserver` (`:187`, `:426`), 1 × `ResizeObserver`
observing both the frame shell *and* the nearest scroll container (`:420-424`),
plus `resize`, `visualViewport.resize` and `scroll` listeners (`:417-418`, `:464-465`).
Observing the shared scroll container from N embeds means one scroll produces N
callbacks, N `getBoundingClientRect()` calls, and up to N `setState` calls per frame.
That is the low-end CPU cost, and it scales with history length, not with what's visible.

### 1.3 PDF is the bitmap-threshold offender

`PdfViewer.tsx:117-135` renders **every page** at `scale: 1.5` into a canvas and keeps
each one as a base64 JPEG data URL in React state, forever. An A4 page at 1.5× is
≈892×1262 px; decoded that is ≈4.5 MB of bitmap. A 20-page PDF is ≈90 MB of decoded
bitmaps plus ≈2-4 MB of base64 strings retained in the JS heap, for one message.
Nothing is released on scroll-away and nothing is capped.

### 1.4 Decorative iframes

`ArtifactLoadingScene.tsx` mounts a **whole iframe per loading message** to show a
decorative SVG animation. Static SVG attachments also go through heavyweight paths
when an `<img>` would do.

### 1.5 The layout shift is structural, not a bug

This is the important one. Today the box size is *derived from the running content*:

```
iframe boots → measureContentMetrics() (miniGameAttachment.ts:283)
            → postMessage 'metrics'
            → setFrameMetrics (MiniGameViewer.tsx:303)
            → resolvedFrameHeight (:364)
            → shell height changes
            → chat content height changes
            → every message below moves
            → the moved iframes re-measure
            → …
```

Plus `frameHeightCap` (`:350-362`) recomputes from the scroll container's rect on
every resize/scroll/RO tick, and feeds the same height.

Two consequences:

1. **You cannot unmount an embed safely.** Unmounting removes the thing that
   defines the size, so the box collapses and the page jumps. This is exactly why
   `hasIntersected` was made sticky — the immortality is a *workaround for the
   sizing model*, not an independent decision. Any "only 1 iframe alive" change
   that doesn't fix sizing first will make the jumping worse, not better.
2. **The remembered size is a pixel height**, so it is wrong after rotation, after
   the keyboard opens, after a font-size change — which forces another live
   re-measure, which shifts the page again.

Fixing the sizing model is therefore the prerequisite for everything else, and it
happens to fix the jank on its own.

---

## 2. The long-term solution

The single principle:

> **The box is owned by the host and computed statically. The content is a
> replaceable tenant that is never allowed to resize its box.**

Once that holds, an embed can be mounted, unmounted, swapped for a poster or for
nothing at all, and the chat does not move by a single pixel. Memory policy then
becomes a free variable we can tune per device.

### 2.1 Reserved box: store an aspect ratio, not a height

Add to `ChatMessage` (`core/types/index.ts`):

```ts
/** Host-computed layout box for a rich attachment. Viewport-independent. */
embedBox?: {
  /** width / height of the content, from static analysis or a committed measure. */
  aspectRatio: number;
  /** 'static' = parsed from source, 'measured' = committed from a past live run. */
  source: 'static' | 'measured';
  /** Schema version so we can re-derive when the heuristics improve. */
  v: number;
};
```

An **aspect ratio** survives rotation, keyboard, split-screen and font scaling; a
pixel height does not. This is the whole reason the current "remembered size" feels
unreliable.

The box then renders in pure CSS, with **no JS in the layout path**:

```css
.embed-box {
  width: 100%;
  aspect-ratio: var(--embed-ar);
  max-height: var(--embed-max-h);   /* from the chat column, not from content */
  min-height: 220px;
  contain: layout paint size;       /* content can never escape or resize the box */
}
```

`--embed-max-h` is set **once** per chat viewport (one shared `ResizeObserver` on
the scroll container in `ChatInterface`, published via context/store), not once per
embed. That removes N-1 of the current N observers.

### 2.2 Derive the aspect ratio without running anything

The parsing logic already exists — it just lives on the wrong side of the iframe
boundary. `parseAspectRatio` and `getIntrinsicAspectRatio` (`miniGameAttachment.ts:124-171`)
run *inside* the frame, so we must boot a program to learn how big its box should be.

Move them into a new host-side module `src/features/chat/utils/embedIntrinsics.ts`
that takes the attachment **source text** and returns a ratio, in priority order:

1. explicit hint in the source (`<meta name="maestro-aspect" content="16/9">`) —
   worth adding to the artifact prompt so the model declares it;
2. `<svg viewBox>` / `width`+`height` attributes;
3. first `<canvas width= height=>`;
4. a literal `aspect-ratio:` in an inline `<style>` block;
5. per-kind default (game 4/3, SVG 1/1, PDF from page 1's viewport, which pdf.js
   gives us *without* rasterizing anything).

This runs in microseconds on a string we already have in memory, at message-render
time, with no iframe, no canvas and no layout. That is what makes it safe to have
**zero** embeds mounted and still lay the chat out correctly on first paint —
including for messages restored from the DB in a fresh session.

### 2.3 Runtime measurement becomes advisory, committed on exit

Keep the in-frame measurement, but change what it is allowed to do:

- A live `metrics` message **never** changes the current box.
- It is stored in a ref. On deactivation (or after a 2 s quiet period), if the
  measured ratio differs from the stored one by more than ~4%, we `updateMessage`
  the `embedBox` with `source: 'measured'`.
- The new ratio therefore applies to the **next** mount, at a moment when nothing
  is animating and the user is not mid-scroll.

Self-correcting over one session, zero shift during it.

### 2.4 One arbiter, one observer, one live embed

New module `src/features/chat/embeds/EmbedActivationManager.ts` — a small
subscribable store (a `uiSlice` addition, or a standalone one; it must not live in
React state that re-renders the whole list).

```
registerEmbed(id, el, kind, priorityHints) → unregister
```

- **One** `IntersectionObserver` for the whole chat, created by `ChatInterface`,
  with a fine threshold list, observing every registered embed element.
- On each intersection batch, rAF-throttled, score candidates:
  `score = visibleFraction * 100 + centeredness * 20 + (isUserEngaged ? 1000 : 0)`.
- Grant `live` to the top `N` (device-tiered, §2.6 — **1** on low-end), everyone
  else gets `placeholder` or `frozen`.
- Explicit user interaction (tapping "game swipes", scrolling a PDF) pins that
  embed live and demotes whatever else held the slot. Pinning must beat visibility
  so an embed can't be evicted out from under an active player.
- Engagement is **exclusive** and has no timer on it. Two pins competing for one
  slot would resolve on an arbitrary tie-break rather than on what the user last
  asked for, so pinning releases every other pin; and an expiring pin hands the
  slot straight back to whichever neighbour scored higher, which is what made a
  tapped embed stop about a second after it started.
- A pinned embed that scrolls off screen is *not* dropped immediately — glancing
  up at the previous message should not restart a game in progress. It falls
  below every visible embed in the ranking, so it yields the instant anything on
  screen needs the slot, and a 20s grace period stops a forgotten game from
  holding a document open for the rest of the session.
- Hysteresis: a ~250 ms dwell before promoting, and a slot is not handed over while
  a fling is in progress (`scroll` velocity check), so a fast scroll through ten
  games boots **zero** of them instead of ten.

Every embed component (`MiniGameViewer`, `PdfViewer`, scripted-SVG, `OfficeFileViewer`)
subscribes to its own id only, so arbitration re-renders one component, not the list.

### 2.4a Measure visibility against the viewport, not the chat container

The observer's root is `null` (the viewport), **not** the chat's scroll
container, and that is not a detail. Passing the container looked obviously
right and was badly wrong on a real device: its `overflow-y: auto` never
engages, because the flex chain above it (`min-h-screen`, `flex-1`, `h-full`)
only ever sets a *minimum* height. It therefore grows to the full conversation
— measured at 9717px against an 800px screen — and the document scrolls instead.

With that as the root, every embed measured as fully visible, "centred" meant
the middle of the entire transcript rather than of the screen, and scrolling
produced no entries at all. The visible result: nothing auto-ran, whatever did
run was off-screen, and an engaged embed was never reported off-screen so it
held the only live slot for the rest of the session.

A viewport root cannot drift out of sync with the layout that way, and it still
accounts for clipping by any intermediate scroll container, so it stays correct
if the chat later gains a real inner scroller. The same layout fact means the
`--embed-max-h` cap must come from the visual viewport rather than the
container's height, and that scroll velocity has to be sampled by capturing at
the window rather than listening on a container that never scrolls.

### 2.5 Three states, with a budgeted poster

```
placeholder ──promote──> live ──demote──> frozen ──evict──> placeholder
     ^                                                          |
     └──────────────────────────────────────────────────────────┘
```

- **placeholder** — the reserved box with the notebook paper/texture treatment and
  a small "tap to run" affordance. Cost: one div. This is the default and the
  resting state for everything off-screen.
- **live** — the real iframe / rendered PDF pages.
- **frozen** — a poster bitmap of the last live frame, so scrolling back doesn't
  look like the content vanished.

Poster rules, written against Play's bitmap threshold:

- captured on demotion via `canvas.toDataURL('image/jpeg', 0.6)` **inside the frame**,
  downscaled to `min(boxWidth, 360) * dpr`, converted to a **blob URL** on the host
  (not a data URL — blob URLs are revocable and don't sit in the JS heap as base64);
- an LRU with a hard budget: **8 posters on high tier, 4 on mid, 0 on low** — low
  tier goes straight to placeholder;
- every eviction `URL.revokeObjectURL`s. A dev-mode counter asserts live blob URLs
  never exceed the budget.

### 2.6 Device tiering

Extend `hardwareSlice.ts` with a `devicePerformanceTier` computed once at startup:

```ts
const tier =
  (navigator.deviceMemory ?? 4) <= 2 || (navigator.hardwareConcurrency ?? 4) <= 4
    ? 'low'
    : (navigator.deviceMemory ?? 4) <= 4 ? 'mid' : 'high';
```

| | low | mid | high |
|---|---|---|---|
| max live embeds | 1 | 1 | 2 |
| poster budget | 0 | 4 | 8 |
| PDF pre-rendered pages | 1 | 2 | 3 |
| PDF render scale cap | 1.0 | 1.25 | 1.5 |
| `maxVisibleMessages` cap | 20 | 35 | 50 |

`navigator.deviceMemory` is absent on some WebViews — the `?? 4` default lands on
`mid`, which is the safe middle. Expose an override in settings so the tier can be
forced for testing and by users who know their device.

### 2.7 Stop using iframes for things that are not programs

Independent of everything above, and the cheapest win available:

- **Static SVG** — no iframe. Sanitize with the existing
  `sanitizeSvgAnimationStructure.ts`, then render as `<img src="data:image/svg+xml,…">`.
  One bitmap instead of a document + JS context. Only SVGs containing `<script>` or
  event-handler attributes need the sandbox, and those go through the normal
  embed lifecycle as scripted artifacts.
- **`ArtifactLoadingScene`** — currently one iframe per loading message for decoration.
  Inline the SVG, or keep a single shared instance portalled to whichever message is
  loading. There is never a reason for two of these to exist.
- **PDF** — rewrite `PdfViewer` to page-level windowing: keep `getOrLoadPdf`'s
  document cache, but render only the pages near the viewport at
  `min(tierScaleCap, boxWidth * dpr / pageWidth)`, hold them as **blob URLs**, and
  revoke pages leaving the window. This alone takes a 20-page PDF from ≈90 MB of
  bitmaps to ≈9 MB, and is what moves us under the bitmap threshold.

### 2.8 Make off-screen messages free

With §2.1 in place, every message has a knowable height without measuring, which
unlocks:

```css
.chat-message { content-visibility: auto; contain-intrinsic-size: auto 220px; }
```

The browser then skips layout, style and paint for off-screen bubbles entirely.
This is a one-line change that is *unsafe today* (it would cause scroll jumps
because heights aren't known) and *safe after* the reserved box lands. On low-end
devices this is typically the largest single scrolling win, larger than the iframe
policy itself.

---

## 3. Why this over the alternatives

**"Just unmount iframes when off-screen."** Doesn't work on its own — the box
collapses on unmount and the chat jumps, which is why the current code deliberately
keeps them alive. Sizing has to be fixed first; once it is, unmounting is trivial.

**"Cache the pixel height per message."** This is roughly what happens today via
`frameMetrics`, and it's the source of the rotation/keyboard wrongness. Pixel
heights are viewport-dependent state masquerading as content state.

**"Virtualize the message list."** Worth doing eventually, but it is a bigger,
riskier change (swipe trays, annotation overlays, focused mode and the bookmark
chunking at `ChatInterface.tsx:479-502` all assume a real DOM list), and
`content-visibility: auto` gets most of the paint/layout win for one CSS line. Keep
virtualization as a later option, not a prerequisite.

**"One shared iframe reused for whichever artifact is focused."** Tempting, and it
does bound memory at exactly one document. Rejected as the primary mechanism: a
single reused frame can't hold per-artifact runtime state (a game in progress dies
when you scroll past it), and re-`srcdoc`ing on scroll is *more* CPU churn on low-end,
not less. The arbiter in §2.4 gives the same memory bound with better behaviour.
The shared-frame trick is still the right answer for `ArtifactLoadingScene`.

---

## 4. What shipped

Each phase landed on its own and left the app better than it found it.

**Phase 0 — free wins** ✅
- `ArtifactLoadingScene` is now a single shared iframe granted to the first
  claimant (`embeds/useArtifactLoadingSceneSlot.ts`); concurrent loads get the
  ordinary spinner. There is never a reason for two of these to exist.
- Static SVG attachments already rendered through `<img>`, so there was nothing
  to de-iframe there — the audit in §1.4 was wrong about that one, and the real
  decorative-iframe cost was the loading scene.
- The in-frame `MutationObserver` now disconnects after a 4s settling window
  instead of firing on every DOM write for the life of the game.

**Phase 1 — reserved box (the keystone)** ✅
- `utils/embedIntrinsics.ts` derives an aspect ratio from source text; `embedBox`
  persists it on `ChatMessage` (and rides the existing `sanitizeForPersistence`
  spread, so it survives a restart with no schema work).
- `embeds/EmbedBox.tsx` + `.embed-box` in `index.css`: `aspect-ratio` and
  `contain: layout paint size`, with `--embed-max-h` published once per viewport
  by `useEmbedViewport`. A `@supports` fallback covers WebViews below Chrome 88.
- Live metrics are advisory, held in a ref and committed on the way out of the
  live phase.
- Artifact generation is now asked to emit `<meta name="maestro-aspect">`, which
  is what makes the *first* paint of a brand-new artifact land on the right box.

**Phase 2 — activation manager** ✅
- `embeds/embedActivation.ts` + `useEmbedSlot`: one IntersectionObserver, rAF-
  coalesced arbitration, dwell before promotion, fling suppression, pinning for
  active engagement, immediate demotion on leaving the viewport.
- The sticky `hasIntersected` and the per-embed observer stack are gone.

**Phase 3 — posters + tiering** ✅
- Blob-URL poster LRU with revocation on eviction; capture happens *while* live
  (a demotion cannot wait for a postMessage round trip) and only for
  canvas-backed artifacts, since rasterizing arbitrary DOM is not worth a
  dependency.
- `devicePerformanceTier` in `hardwareSlice` drives every budget, including a
  cap on `maxVisibleMessages`.

**Phase 4 — PDF windowing** ✅ / **`content-visibility`** ❌ deliberately deferred
- `PdfViewer` rasterizes only the pages in a tier-sized window around the
  reader, at a scale derived from the displayed width, into revocable blob URLs.
  Every page still reserves its slot at the document's page ratio.
- `content-visibility: auto` on message bubbles was **not** shipped. The
  transcript is bottom-anchored and the user scrolls up into history that has
  never been rendered, so `contain-intrinsic-size: auto <estimate>` would snap
  each bubble from the estimate to its true height — a correction of ~80px to
  ~500px depending on the message — *above* the scroll position, absorbed only
  by Chromium scroll anchoring, in a container that also drives its own
  `scrollIntoView`. That is the same class of shift this whole design removes,
  so it is not a trade worth making blind. To land it: give each message a
  remembered height hint the way `embedBox` remembers a ratio, then verify on a
  real low-end device against a long mixed-attachment history.

## 4b. Measured on device

Honor DNP_NX9, Android 16, 800px viewport, a real 28-message conversation with
6 mini-game artifacts. Identical scripted scroll (top to bottom and back) on
each build, via CDP against a release-signed diagnostic build.

| | main | this branch |
|---|---|---|
| Documents — idle → after scroll → after GC | **8 → 12 → 10** | **4 → 4 → 4** |
| DOM nodes | 2204 → 2351 → 2315 | 2070 → 2070 → 2070 |
| JS event listeners | 440 → 556 → 496 | 390 → 434 → 390 |
| JS heap | 61 MB | 57.5 MB |
| Main JS chunk | 2587 KB (688 KB gz) | 2085 KB (538 KB gz) |

The document row is the result that matters. On `main` a **single** scroll pass
creates four more documents and garbage collection reclaims only two; the same
pass on this branch leaves every counter exactly where it started. Node and
listener counts behave the same way. That is the accumulation the Play memory
thresholds punish, and it is now bounded by policy rather than by history depth.

**On PSS:** don't read much into it. Sampled repeatedly it swings ±10 MB on an
idle device and is dominated by Graphics (~86 MB of WebView compositing), so
while the branch measured lower than `main`, the difference sits inside the
noise band. The deterministic counters above are the evidence; PSS is not.

### React render behaviour

Profiled by installing a React DevTools hook stub before app scripts run and
counting commits plus prop-changed fibers.

| | main | branch | after fixes |
|---|---|---|---|
| ChatMessageBubble (prop-driven) | 281 | 469 | **0** |
| IconTrash | 336 | 560 | **0** |
| IconBookmark | 168 | 280 | **0** |
| commits per scroll | 12 | 20 | 20 |

Every bubble was re-rendering on every commit despite `React.memo`, because a
single forwarded callback (`onQuotaStartLive`) changed identity each render.
One unstable prop is enough to defeat memo for a whole subtree, and nothing
surfaces it short of profiling — hence `useStableCallback` at the ChatInterface
boundary rather than pinning the upstream chain.

Commits rose 12 → 20 because embed promotion and demotion are state changes.
That is a real cost of the design, now offset by each commit being far cheaper.

**Where it stands:** CPU during a scroll is 57% browser compositing and 35%
idle, with no JS frame above ~3%. The remaining per-commit icon renders inside
bubbles (bubbles subscribe to the store directly, so store changes still
re-render them) do not show up in the profile. Narrowing those subscriptions is
the next lever if one is ever needed; there is currently nothing to gain.

### Not done, and why

- **R8 / `minifyEnabled`** is still `false`. Play names code optimization as a
  threshold, so this is worth doing — but it needs a functional pass over every
  Capacitor plugin (secure storage, filesystem, share, clipboard, browser,
  preferences) with real ProGuard rules, which is not a change to make
  unverified immediately before a release.

---

## 5. Guardrails

Without these, this regresses within a few features.

- **`components/MiniGameViewer.test.tsx`** — renders a wall of mini-games, walks a
  viewport down it, and asserts the live `iframe` count never exceeds the budget,
  including across the exact scroll motion that used to leave one behind at every
  stop. It also asserts the reserved box is byte-identical before and after one
  of them boots. This is the test that matters; the rest are supporting.
- **`embeds/embedActivation.test.ts`** — budget enforcement, centre-weighted
  selection, immediate release on leaving the viewport, engagement outranking
  visibility, pins dropping when the embed scrolls away, poster eviction and
  revocation, and the no-IntersectionObserver fallback.
- **`components/PdfViewer.test.tsx`** — only the window around the reader is
  rasterized, and every page still reserves its slot at the document ratio.
- **`utils/embedIntrinsics.test.ts`** — derivation priority, clamping,
  determinism, and the commit policy that keeps boots from churning the layout.
- **`store/slices/hardwareSlice.test.ts`** — tier classification (including the
  unreported-memory case that must not resolve optimistically) and monotonic budgets.
- **Dev counter:** `window.__EMBED_DEBUG__()` returns live ids, frozen ids, poster
  count, budgets and whether the observer is attached. Makes "did this feature leak
  a document?" a five-second check.
- **Release checklist:** `docs/RELEASE_CHECKLIST.md` carries a low-tier memory pass,
  since the Play thresholds are measured on real devices, not in dev.

### Known follow-ups, not addressed here

- **`sandbox="allow-scripts allow-same-origin"` on the mini-game iframe.** That
  pair is effectively no sandbox for a same-origin `srcdoc` frame, and the content
  is model-authored. Out of scope for a memory and layout change, and dropping
  `allow-same-origin` would need its own compatibility pass over existing artifacts
  (storage access, canvas readback), but it should be looked at.
- **`renderPdfPageToImage` still rasterizes at a fixed 1.5 scale** for the
  annotation flow. That is one page on an explicit user action, so it is not a
  standing cost, but it is the last uncapped rasterization in the app.
- **`content-visibility` on message bubbles**, per §4.

---

## 6. Files touched

| File | Change |
|---|---|
| `src/core/types/index.ts` | `EmbedBox` type; `embedBox` on `ChatMessage` |
| `src/features/chat/utils/embedIntrinsics.ts` | **new** — static aspect-ratio derivation and the commit policy |
| `src/features/chat/embeds/embedTypes.ts` | **new** — shared embed vocabulary |
| `src/features/chat/embeds/embedActivation.ts` | **new** — arbiter + the single shared IntersectionObserver |
| `src/features/chat/embeds/useEmbedSlot.ts` | **new** — React binding (callback ref, so late-appearing boxes register) |
| `src/features/chat/embeds/useEmbedViewport.ts` | **new** — publishes `--embed-max-h`, points the arbiter at the scroll root, applies budgets |
| `src/features/chat/embeds/EmbedBox.tsx` | **new** — the reserved box |
| `src/features/chat/embeds/posterStore.ts` | **new** — poster sizing constants + data-URL → blob-URL |
| `src/features/chat/embeds/useArtifactLoadingSceneSlot.ts` | **new** — grants the one shared loading-scene iframe |
| `src/features/chat/components/MiniGameViewer.tsx` | drop sticky `hasIntersected` and the 3-observer stack; consume `EmbedBox` + the slot; poster capture; commit-on-exit |
| `src/features/chat/utils/miniGameAttachment.ts` | metrics advisory only; poster capture in the bridge; MutationObserver disconnects after settling |
| `src/features/chat/components/PdfViewer.tsx` | page windowing, blob URLs, tiered scale, activation slot |
| `src/features/chat/components/TextFileViewer.tsx` | threads `embedId` / `embedBox` through to the mini-game |
| `src/features/chat/components/ChatMessageBubble.tsx` | supplies `embedId` / `embedBox` and persists a committed box |
| `src/features/chat/components/ArtifactLoadingScene.tsx` | one shared instance, spinner fallback |
| `src/features/chat/components/ChatInterface.tsx` | owns the viewport wiring; tier-capped `maxVisibleMessages` |
| `src/store/slices/hardwareSlice.ts` | `devicePerformanceTier` + `DEVICE_BUDGETS` |
| `src/core/config/prompts.ts` | asks artifacts to declare `<meta name="maestro-aspect">` |
| `src/app/index.css` | `.embed-box` + `@supports` fallback, placeholder/poster/rest-hint styling |
