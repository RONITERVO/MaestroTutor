# Embed memory & layout stability architecture

Branch: `perf/single-live-embed`

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
- Hysteresis: a ~250 ms dwell before promoting, and a slot is not handed over while
  a fling is in progress (`scroll` velocity check), so a fast scroll through ten
  games boots **zero** of them instead of ten.

Every embed component (`MiniGameViewer`, `PdfViewer`, scripted-SVG, `OfficeFileViewer`)
subscribes to its own id only, so arbitration re-renders one component, not the list.

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

## 4. Sequencing

Each phase is shippable on its own and leaves the app better than it found it.

**Phase 0 — free wins, no architecture change**
- de-iframe static SVG (§2.7)
- single shared / inlined `ArtifactLoadingScene` (§2.7)
- cap `PdfViewer` render scale by device pixel ratio and box width
- *Effect:* immediate drop in document count and bitmap bytes. No behaviour change.

**Phase 1 — reserved box (the keystone)**
- `embedIntrinsics.ts` + `embedBox` on `ChatMessage` (§2.2, §2.1)
- CSS `aspect-ratio` box with `contain`; shared `--embed-max-h` from one RO
- demote live metrics to advisory + commit-on-exit (§2.3)
- *Effect:* the jitter and the scroll-jump-on-boot are gone. Nothing has been
  unmounted yet, so this phase is low-risk and independently valuable.

**Phase 2 — activation manager**
- `EmbedActivationManager` + one shared IO, wired into `MiniGameViewer` first (§2.4)
- placeholder state; delete the sticky `hasIntersected`; delete the per-embed
  observer stack
- *Effect:* live embed count drops from "everything ever seen" to 1.

**Phase 3 — posters + tiering**
- frozen state, blob-URL poster LRU (§2.5)
- `devicePerformanceTier` in `hardwareSlice` driving every budget (§2.6)
- extend the manager to `PdfViewer` and `OfficeFileViewer`

**Phase 4 — PDF windowing + `content-visibility`**
- page-level windowing with blob URLs (§2.7)
- `content-visibility: auto` on bubbles (§2.8)

---

## 5. Guardrails

Without these, this regresses within a few features.

- **Test:** render a 50-message fixture with 10 mini-games, scroll it, assert
  `container.querySelectorAll('iframe').length <= maxLiveEmbeds`. This is the
  regression test that matters; everything else is secondary.
- **Test:** the reserved box is computed identically from source text before and
  after a live run (no shift on remount), and a rotation changes the height but
  not the stored `embedBox`.
- **Dev counter:** a `__EMBED_DEBUG__` overlay showing live embeds, frozen posters,
  live blob URLs, and current tier. Cheap to add, and it makes "did this feature
  leak a document?" a five-second check.
- **Release checklist:** add a low-tier memory pass (Android Studio memory profiler
  against a long chat with mixed attachments) to `docs/RELEASE_CHECKLIST.md`, since
  the Play thresholds are measured on real devices, not in dev.

---

## 6. Files touched

| File | Change |
|---|---|
| `src/core/types/index.ts` | `embedBox` on `ChatMessage` |
| `src/features/chat/utils/embedIntrinsics.ts` | **new** — static aspect-ratio derivation |
| `src/features/chat/embeds/EmbedActivationManager.ts` | **new** — arbiter + shared IO |
| `src/features/chat/embeds/EmbedBox.tsx` | **new** — reserved box + placeholder/frozen/live |
| `src/features/chat/components/MiniGameViewer.tsx` | drop sticky `hasIntersected`, drop the 3-observer stack, consume `EmbedBox` |
| `src/features/chat/utils/miniGameAttachment.ts` | metrics advisory only; add poster capture on demand |
| `src/features/chat/components/PdfViewer.tsx` | page windowing, blob URLs, tiered scale |
| `src/features/chat/components/ChatMessageBubble.tsx` | static SVG via `<img>`; box sizing from `embedBox` |
| `src/features/chat/components/ArtifactLoadingScene.tsx` | inline SVG / shared instance |
| `src/features/chat/components/ChatInterface.tsx` | owns the single IO + the `--embed-max-h` RO |
| `src/store/slices/hardwareSlice.ts` | `devicePerformanceTier` + budgets |
| `src/app/index.css` | `.embed-box`, `content-visibility` on bubbles |
