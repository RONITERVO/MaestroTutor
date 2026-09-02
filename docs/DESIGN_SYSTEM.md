# Maestro Tutor Color and Design System

This is the canonical color and UI styling reference for contributors.

## Overview

- Last updated: 2026-09-02
- Active color tokens: 316
- Token groups: 35
- Legacy migration keys supported: 91
- Token model: one token per visual UI element (1:1 element token mapping)
- Source of truth files: `src/features/theme/config/colorRegistry.ts` (token registry), `src/features/theme/config/themeColors.ts` (palettes), `src/features/theme/config/defaultTheme.ts`, `src/features/theme/config/presetThemes.ts`
- Generated from those: the `:root` block in `src/app/index.css` (via `colorTokensPlugin` in `vite.config.ts`) and the Tailwind palette in `tailwind.config.ts`

## Non-Negotiable Rules

- Every visual element gets its own token. Do not reuse unrelated tokens to "save" variables.
- Do not hardcode color utilities for product UI (examples to avoid: `hover:bg-white/20`, `ring-green-500`, `shadow-[0_0_15px_rgba(...)]`).
  `tokenVars.test.ts` fails the build on any of these, including a literal colour hidden inside an arbitrary value.
  Multi-colour illustrations (`shared/ui/Icons.tsx`), the canvas-generated practice paper and the globe widget's internal palette are
  deliberately exempt: they are artwork rather than themeable chrome.
- If a mode changes meaningfully (chat mode vs suggestion mode, native vs target, idle vs active), use mode-specific tokens.
- State-specific styling needs state-specific tokens (`-hover`, `-ring`, `-focus`, `-spinner`, `-glow`, etc.) when that state should be independently themeable.
- New UI is not complete until token wiring is done across all token source files.
- A token that must keep its fill in the Clear themes has to be listed in `transparentTheme.ts`; everything with a `-bg` name goes transparent by default.

## Naming Convention

- Pattern: `domain-element-role` (examples: `user-msg-bg`, `live-stop-icon`, `annotation-btn-focus`).
- Typical suffixes:
  - `-bg`: background fill
  - `-text`: text or icon color
  - `-border`: border stroke
  - `-hover`: hover state fill
  - `-ring` or `-focus`: focus treatment
  - `-spinner` or `-glow`: dedicated indicator or glow color

## Opacity

Every token carries an opacity as well as a colour, and both the user and the
developer can set one. A token value is `"<h> <s>% <l>%"` with an optional
alpha — `"210 20% 97% / 0.5"`. No alpha means fully opaque, so a value written
before opacity existed still means exactly what it meant then.

Each token expands to three CSS variables:

| Variable | Holds | Written by |
| --- | --- | --- |
| `--x` | the HSL channels | generated `:root`, overridden inline by the customizer |
| `--x-alpha` | the user's opacity, defaulting to `1` | same |
| `--x-color` | `hsl(var(--x) / var(--x-alpha, 1))` | generated `:root` only |

**How to reference a token:**

- Tailwind: `bg-page-bg`, or `bg-page-bg/50` to tint it further.
- Hand-written CSS or an inline style: `var(--page-bg-color)`.
- Hand-written CSS that needs its own tint:
  `hsl(var(--page-bg) / calc(var(--page-bg-alpha, 1) * 0.5))`.

Never write `hsl(var(--page-bg))` or `hsl(var(--page-bg) / 0.5)`. Both drop
whatever opacity the user chose. `tokenVars.test.ts` fails the build on either.

The two controls **multiply**: a token the user set to 50% rendered through
`bg-x/30` lands at 15%, and a plain `bg-x` is exactly the user's 50%. This is
why the channels and the alpha live in separate variables — a token that
carried its alpha inline would make Tailwind emit
`hsl(H S% L% / 0.5 / .3)` for `bg-x/30`, which is invalid CSS and is dropped
silently by the browser.

Writes to the DOM all go through `applyTokenValue` / `clearTokenValue` in
`src/features/theme/utils/applyTokenValue.ts`, so channels and alpha can never
get out of step. The grammar itself lives in `utils/tokenValue.ts`.

## Theme Variants

Every included theme ships twice: a **Clear** variant and the **Solid** original.
Clear drops the fills so the sketch outlines carry the design, and **Clear
Graphite is the app default**. Both are listed in the theme customizer; there is
no separate gallery, and every theme is free.

Only the solid palettes are written by hand, in `themeColors.ts`. The Clear
variants are derived by `makeTransparentPalette` in `config/transparentTheme.ts`,
so a new token gets a sensible clear value in all eleven themes without anyone
touching a palette. Two rules make that safe:

- **Some fills stay.** `KEEP_FILLED` and `UNTOUCHED_GROUPS` in that file list
  what a clear theme must not hollow out - the Maestro flag, the translation
  highlight, the audio player's controls, the API key gate, the traffic log's
  header and cards, recording and live state, destructive buttons, and anything
  drawn over photos, video or a mini-game canvas. The traffic log keeps its
  backdrop see-through and its header and cards solid, which is why it has
  `debug-panel-bg` separate from `debug-header-bg` and `debug-card-bg`.
- **Text follows its surface.** Once a fill goes, whatever sat on it is read
  against the page instead. Graphite's user bubble is dark with near-white text;
  drop the bubble and that text would land on near-white paper. Every foreground
  whose surface went clear is re-checked with `utils/contrast.ts` and walked to a
  legible lightness, keeping its hue. `transparentTheme.test.ts` fails if any
  variant leaves a foreground below 4.5:1 for text or 3:1 for a UI mark.

Hover fills become a faint wash rather than disappearing, so the feedback
survives. To change what a clear theme does, edit the rules in
`transparentTheme.ts` - never a generated palette.

## Required Contributor Workflow

When adding or changing a colorized element:

1. Add token metadata to `src/features/theme/config/colorRegistry.ts` with friendly name and description. This registry is the single source of truth: the `:root` declarations in `src/app/index.css` are generated from it by `colorTokensPlugin` in `vite.config.ts` (replacing the `/* __COLOR_TOKENS__ */` marker), and the Tailwind color map in `tailwind.config.ts` is derived from it too. Neither needs a manual edit.
2. Add token to the active default palette in `src/features/theme/config/themeColors.ts` and keep `src/features/theme/config/defaultTheme.ts` aligned if the app default changes. The build fails if a registered token has no default value. Add `/ <alpha>` to the value only if the token should ship translucent.
3. Use the tokenized utility class in JSX/TSX; remove any direct hardcoded color utility or literal color. For hand-written CSS see [Opacity](#opacity) — `var(--x-color)`, never `hsl(var(--x))`.
4. If replacing legacy token keys, add mapping to `src/features/theme/config/colorRenameMap.ts`.
5. Validate with build + visual pass + Theme Customizer coverage.

## Recent Token Isolation Updates (2026-09-02)

- Action confirmation panels split by role: `action-*-accent` paints the panel tint, border and button; the new `action-*-label` paints the heading and the type-to-confirm prompt. The accent is a fill, and using it as text left those invisible in most themes.
- Clear theme variants: every theme now ships a fill-less Clear variant, and Clear Graphite is the app default. See [Theme Variants](#theme-variants).
- Traffic log surfaces split so its backdrop can go see-through while the header and cards stay readable: `debug-panel-bg`, `debug-header-bg`, `debug-card-bg`.
- The theme gallery sheet was removed; the customizer lists every included theme directly.
- Per-token opacity: every token now carries an alpha the user can set, composing with developer `/50` modifiers. See [Opacity](#opacity).
- Overlay scrims isolated: `scrim-modal`, `scrim-gate`, `scrim-panel`, `scrim-busy`.
- Media overlay isolated: `media-letterbox`, `media-overlay-icon`, `media-overlay-focus`, `media-overlay-shadow`, `media-chip-bg`, `media-chip-text`,
  `media-loading-bg`, `media-error-scrim`, `media-preview-veil`, `media-rec-chip-bg`, `media-observer-dot`, `pdf-page-placeholder`.
- Mini-game overlay isolated: `game-status-bg`, `game-deck-bg`, `game-error-bg` and their siblings.
- Status notices and destructive actions isolated: `notice-ok-*`, `notice-error-*`, `danger-btn-*`, `danger-zone-*`, `danger-input-*`.
- Debug traffic log isolated: `debug-ok-text`, `debug-error-text`, `debug-payload-bg` and siblings.
- Remaining one-off shadows tokenised: `mic-record-glow`, `attachment-toggle-shadow`, `game-code-shadow`, `scroll-wheel-flag-shadow`.

## Earlier Token Isolation Updates (2026-03-15)

- Assistant playback highlight split: `marker-target-bg`, `marker-target-text`, `marker-native-bg`, `marker-native-text`.
- User attachment text parity: `user-attachment-inline-text`, `user-attachment-audio-text`, `user-attachment-overlay-text`, `user-attachment-svg-text`, `user-attachment-game-text`.
- Annotation save button state isolation: `annotation-btn-hover`, `annotation-btn-focus`.
- Live idle mode isolation: `live-idle-sugg-btn-bg`, `live-idle-sugg-btn-text`, `live-idle-spinner`.
- Composer send and icon state isolation: `send-sugg-btn-bg`, `send-sugg-btn-text`, `chat-input-icon-hover-bg`, `snapshot-error-bg`.
- Suggestions double-click focus isolation: `suggestion-double-ring`.
- Session selector isolation: `scroll-wheel-target-accent`, `globe-native-accent`, `globe-target-accent`, `maestro-avatar-glow`.
- Message tape isolation: `tape-bg-light`, `tape-bg-mid`, `tape-bg-dark`, `tape-border`, `tape-shadow`, `tape-inset`, `tape-wrinkle`, `tape-highlight`, `tape-crease`.

## Typography

- `font-sketch`: Caveat
- `font-hand`: Patrick Hand (default body/UI)
- `font-architect`: Architects Daughter
- Base body stack in CSS: Patrick Hand, Caveat, cursive

## Shape and Decorative System

- Shape variants: `.sketch-shape-0` through `.sketch-shape-11` in `src/app/index.css`.
- Borders: `.sketchy-border`, `.sketchy-border-thin`, `.sketchy-underline`.
- Decor: `.paper-texture`, `.notebook-lines`, `.tape-effect`, `.torn-paper`.
- Use `rounded-full` for true circles (icon buttons, avatars), not sketch shape classes.

## Motion

- Tailwind animations are configured in `index.html`.
- App utility animations are in `src/app/index.css` (`animate-voice-swap`, `animate-voice-ripple`, `animate-flag-wave`).

## Access Card Layout

The API key gate is one card and stays one card. Every access method it offers
competes for the same two-control budget the Gemini key path already uses: a
button that goes and gets something, and the field it lands in.

- No prose on the card. Text belongs inside a button that names its action or
  inside a field. `OR USE YOUR OWN KEY` is the deliberate exception: it marks
  the boundary between two access methods and nothing else does that job.
- Controls that act on a field sit inside it, as icons unless the icon alone
  would be a guess. Buttons keep words only where the word is the explanation.
- Anything that does not fit — balances, ledgers, purchases, account deletion,
  status and error prose — opens from a button in a modal. That button may
  carry a one-value preview (a balance, an estimated spend), never a sentence.
- Two icons on the card never mean two different things. The API-key help
  button owns the question mark; managed access uses its own symbol.
- The card stays within roughly twice the height of the bare key path. Growing
  past that means something belongs in a modal, not that the card should grow.

`ApiKeyGate`, `ManagedAccessPanel` and `ManagedAccountModal` implement this;
`ManagedAccessPanel.test.tsx` pins each state's control count so a later
addition has to make the same choice deliberately.

## Legacy Migration

- Legacy key map: `src/features/theme/config/colorRenameMap.ts`.
- Persisted settings migration: `src/store/slices/settingsSlice.ts`.
- Imported preset migration: `src/features/theme/utils/themeFileIO.ts`.
- Legacy keys are supported for migration compatibility only; do not use them in new code.

## Full Token Inventory

Generated from `colorRegistry.ts` by `npm run docs:tokens`. Do not edit by hand.

Default HSL is the value in the active default palette; every other theme may override it.

### Page Canvas

The full-screen paper and main writing color

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--page-bg` | `40 8% 97%` | Page Background | Main app background behind everything |
| `--page-text` | `220 8% 14%` | Page Text | Default text color used across the app |
| `--paper-surface` | `40 6% 99% / 0` | Paper Surface | Notebook paper areas in the main content |
| `--paper-stripe` | `40 5% 92% / 0` | Paper Stripe | Darker paper stripes and paper depth |
| `--deep-ink` | `220 10% 11%` | Deep Ink | Strong deep-ink text and marks |
| `--link-text` | `224 76% 48%` | Text Link | Inline links in body text, such as the privacy policy link |

### Chat Message Bubbles

Backgrounds and text for each message type

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--user-msg-bg` | `220 8% 14% / 0` | User Message Background | Background of your sent message bubbles |
| `--user-msg-text` | `40 8% 43%` | User Message Text | Text and icons inside your messages |
| `--ai-msg-bg` | `40 6% 99% / 0` | AI Message Background | Background of assistant reply bubbles |
| `--ai-msg-text` | `220 8% 16%` | AI Message Text | Text inside assistant replies |
| `--status-msg-bg` | `40 5% 90% / 0` | Status Message Background | Background of system/status messages |
| `--status-msg-text` | `220 6% 30%` | Status Message Text | Text inside system/status messages |
| `--error-msg-bg` | `220 5% 40% / 0` | Error Message Background | Background of error message bubbles |
| `--error-msg-text` | `220 5% 40%` | Error Message Text | Text color for error messages |
| `--thinking-bubble-bg` | `40 5% 90% / 0` | Thinking Indicator Background | Background of the thinking... bubble |
| `--thinking-bubble-text` | `220 5% 45%` | Thinking Indicator Text | Text in the thinking... bubble |

### Message Sub-elements

File attachments, image placeholders, and error indicators within messages

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--ai-msg-placeholder` | `220 6% 32% / 0` | AI Image Placeholder | Placeholder background while AI image loads (focused view) |
| `--ai-file-bg` | `40 5% 88% / 0` | AI File Attachment | Background of file attachments in assistant messages |
| `--ai-file-text` | `220 5% 45%` | AI File Text | Text and icon color in assistant file attachments |
| `--img-error-text` | `220 5% 42%` | Image Error Text | Error text color for image generation failures |

### Attachment Transcript Text

Separate text colors for user and assistant attachment text across inline, audio, detached attachment shells, and mini-game placements

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--user-attachment-inline-text` | `40 8% 43%` | User Inline Attachment Text | Your message text and inline attachment labels next to standard attachments |
| `--user-attachment-audio-text` | `40 8% 43%` | User Audio Attachment Text | Your message text when an audio attachment is shown in the audio shell |
| `--user-attachment-svg-text` | `220 8% 14%` | User Detached Attachment Text | Your message text shown in detached attachment transcript shells (images, PDFs, SVG, and notebooks) |
| `--user-attachment-game-text` | `220 8% 14%` | User Game Attachment Text | Your message text shown with mini-game attachment shells and controls |
| `--attachment-inline-target-text` | `220 8% 16%` | Inline Target Text | Main attachment transcript text shown under attachments, including music replies |
| `--attachment-inline-native-text` | `220 5% 45%` | Inline Native Text | Secondary or native attachment transcript text shown under attachments, including music replies |
| `--attachment-audio-target-text` | `220 8% 14%` | Audio Target Text | Main text in the focused assistant audio scroll wheel |
| `--attachment-audio-native-text` | `220 5% 42%` | Audio Native Text | Secondary or native text in the focused assistant audio scroll wheel |
| `--attachment-svg-target-text` | `220 8% 14%` | Detached Target Text | Main transcript text in detached attachment transcript shells |
| `--attachment-svg-native-text` | `220 5% 45%` | Detached Native Text | Secondary or native transcript text in detached attachment transcript shells |
| `--attachment-game-target-text` | `220 8% 14%` | Game Target Text | Main transcript text when the transcript overlaps a mini-game |
| `--attachment-game-native-text` | `220 5% 45%` | Game Native Text | Secondary or native transcript text when the transcript overlaps a mini-game |

### Chat Input Area

Message composer in chat and suggestion modes

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--chat-input-bg` | `220 8% 12% / 0` | Chat Input Background | Input field background in chat mode |
| `--chat-input-text` | `40 8% 43%` | Chat Input Text | Text color in chat mode input |
| `--chat-input-icon` | `40 8% 55%` | Chat Input Icons | Icon button color inside chat input |
| `--chat-input-icon-hover-bg` | `40 6% 99% / 0.14` | Chat Icon Hover | Hover background for chat-mode icon buttons |
| `--sugg-input-bg` | `40 6% 99% / 0` | Suggestion Input Background | Input field background in suggestion mode |
| `--sugg-input-text` | `220 8% 16%` | Suggestion Input Text | Text color in suggestion mode input |
| `--sugg-input-icon` | `220 5% 50%` | Suggestion Input Icons | Icon button color in suggestion mode |
| `--send-btn-bg` | `40 6% 99% / 0` | Send Button Background | Background of the send message button |
| `--send-btn-text` | `220 8% 16%` | Send Button Text | Text/icon color on the send button |
| `--send-sugg-btn-bg` | `220 6% 28% / 0` | Suggest Send Background | Background of the send/create button in suggestion mode |
| `--send-sugg-btn-text` | `40 8% 43%` | Suggest Send Text | Text/icon color of the suggestion-mode send/create button |
| `--input-focus-ring` | `220 7% 30%` | Input Focus Ring | Ring shown when the input field is focused |
| `--input-error-bg` | `220 5% 40% / 0.16` | Input Error Background | Background for error messages in the input area |
| `--input-error-text` | `40 8% 43%` | Input Error Text | Text color for input area error messages |
| `--snapshot-error-bg` | `220 8% 18% / 0.16` | Snapshot Error Background | Background tint for snapshot-related input errors |
| `--chat-outer-bg` | `220 6% 28% / 0` | Chat Mode Container | Outer container background in chat mode |
| `--chat-outer-text` | `40 8% 43%` | Chat Mode Container Text | Text in the outer chat mode container |
| `--sugg-outer-bg` | `40 5% 90% / 0` | Suggestion Mode Container | Outer container background in suggestion mode |
| `--stt-lang-selected-bg` | `0 0% 100% / 0.18` | Speech Language Selected | The chosen speech language while the selector is collapsed |
| `--stt-lang-selected-sugg-bg` | `0 0% 100% / 0.18` | Speech Language Selected (Suggestion) | The chosen speech language in suggestion mode |
| `--stt-lang-hover-bg` | `0 0% 100% / 0.14` | Speech Language (Hover) | A speech language option while pointed at |
| `--stt-lang-sugg-hover-bg` | `0 0% 0% / 0.14` | Speech Language (Hover, Suggestion) | A speech language option in suggestion mode |
| `--stt-lang-selected-text` | `0 0% 44%` | Speech Language Selected Text | The chosen speech language while expanded |

### Chat Interface Chrome

History peek, navigation buttons, and suggestion controls

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--history-peek-bg` | `40 5% 90% / 0` | History Peek Background | Background of the message history peek zone |
| `--history-peek-icon` | `220 5% 55%` | History Peek Eye Icon | Eye icon color in the history peek zone |
| `--history-btn-bg` | `40 6% 99% / 0` | History Button Background | Background of history navigation buttons |
| `--history-btn-hover` | `40 5% 92% / 0.14` | History Button Hover | Hover color for history navigation buttons |
| `--delete-msg-bg` | `220 6% 28% / 0` | Delete Message Button | Background of the delete message button |
| `--delete-msg-text` | `40 8% 43%` | Delete Message Text | Icon color on the delete message button |
| `--save-sugg-bg` | `220 6% 28% / 0` | Save Suggestion Button | Background of the save suggestion button |
| `--save-sugg-text` | `40 8% 43%` | Save Suggestion Text | Text on the save suggestion button |
| `--clear-sugg-bg` | `220 5% 50% / 0` | Clear Suggestion Button | Background of the clear suggestion button |
| `--clear-sugg-text` | `40 8% 43%` | Clear Suggestion Text | Text on the clear suggestion button |
| `--web-results-bg` | `40 5% 90% / 0` | Web Results Container | Background of the web search results area |
| `--web-results-link` | `220 7% 30%` | Web Results Link | Link color in web search results |

### Audio Player

Playback controls for recorded audio messages

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--audio-player-bg` | `40 5% 90% / 0` | Audio Player Background | Background of the audio playback bar |
| `--audio-play-btn` | `220 6% 28%` | Audio Play Button | Background of the play/pause button |
| `--audio-play-text` | `40 8% 97%` | Audio Play Icon | Icon color on the play/pause button |
| `--audio-bar` | `220 6% 28%` | Audio Progress Bar | Color of the audio progress bar |
| `--audio-time-text` | `220 5% 45%` | Audio Time Display | Time text in the audio player |

### Bookmark Actions

Bookmark save panel and manage buttons

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--bookmark-bg` | `220 8% 14% / 0` | Bookmark Panel | Background of the bookmark save panel |
| `--bookmark-text` | `40 8% 43%` | Bookmark Text | Text and button color in bookmark panel |
| `--bookmark-input-bg` | `40 6% 99% / 0` | Bookmark Input | Background of the bookmark name input field |
| `--bookmark-input-text` | `220 8% 14%` | Bookmark Input Text | Text color in the bookmark name field |
| `--bookmark-divider` | `220 5% 60%` | Bookmark Divider | Line divider between bookmark sections |

### Suggestions List

Translation suggestion items

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--suggestion-bg` | `40 5% 90% / 0` | Suggestion Background | Background of individual suggestion lines |
| `--suggestion-hover` | `40 5% 86% / 0.14` | Suggestion Hover | Hover background for suggestion lines |
| `--suggestion-ring` | `220 6% 28%` | Suggestion Focus Ring | Focus ring around selected suggestion |
| `--suggestion-double-ring` | `220 8% 14%` | Suggestion Confirm Ring | Focus ring for suggestions on double-click/confirm interaction |
| `--suggestion-active-bg` | `220 6% 28% / 0.18` | Creating Suggestion | Background while a suggestion is being created |
| `--suggestion-active-text` | `40 8% 43%` | Creating Suggestion Text | Text while a suggestion is being created |

### Session Controls

Profile editing, mode toggle, and sidebar controls

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--profile-label-text` | `220 6% 36%` | Profile Label Text | Profile heading label color |
| `--profile-input-accent` | `220 6% 36%` | Profile Input Accent | Accent color for profile input fields and borders |
| `--scroll-wheel-flag-shadow` | `0 0% 0% / 0.22` | Language Flag Shadow | Drop shadow under the flags in the language wheel |
| `--scroll-wheel-target-accent` | `220 6% 28%` | Scroll Wheel Target Accent | Scroll focus ring for the non-native language wheel |
| `--globe-native-accent` | `220 5% 42%` | Globe Native Accent | Border and glow for the native-language marker on the globe |
| `--globe-target-accent` | `220 6% 28%` | Globe Target Accent | Border and glow for the target-language marker on the globe |
| `--maestro-avatar-glow` | `220 6% 32%` | Maestro Avatar Glow | Glow color around the maestro avatar when an image is present |
| `--profile-btn-bg` | `220 8% 14% / 0` | Profile Button | Background of profile edit/label buttons |
| `--profile-btn-text` | `40 8% 43%` | Profile Button Text | Text on profile edit/label buttons |
| `--profile-accept-bg` | `220 8% 20% / 0` | Profile Accept Button | Background of the profile accept/confirm button |
| `--profile-accept-text` | `40 8% 43%` | Profile Accept Text | Text on the profile accept button |
| `--mode-toggle-bg` | `220 8% 14% / 0` | Mode Toggle Container | Background of the All/This mode toggle |
| `--mode-toggle-text` | `40 8% 43%` | Mode Toggle Text | Text on mode toggle buttons |
| `--save-chat-text` | `220 7% 22%` | Save Chat Label | Text color for the save chat action label |
| `--ctrl-muted-text` | `220 5% 45%` | Controls Muted Text | Dimmed text in session controls |

### Header

Debug button and loading indicator in the app header

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--debug-btn-bg` | `220 8% 14%` | Debug Button | Background of the debug logs button |
| `--debug-btn-text` | `40 8% 97%` | Debug Button Text | Text on the debug logs button |
| `--debug-btn-muted` | `220 5% 50%` | Debug Button Muted | Muted text state of the debug button |
| `--loading-spinner` | `220 6% 36%` | Loading Spinner | Color of the loading spinner indicator |

### Live Session Idle Button

Live session button when no session is active

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--live-idle-btn-bg` | `220 8% 14% / 0` | Live Idle Button | Live session button background when idle |
| `--live-idle-btn-text` | `40 8% 43%` | Live Idle Button Text | Live session button text when idle |
| `--live-idle-sugg-btn-bg` | `40 5% 90% / 0` | Live Suggest Idle Background | Live session button background when idle in suggestion mode |
| `--live-idle-sugg-btn-text` | `220 8% 14%` | Live Suggest Idle Text | Live session button text when idle in suggestion mode |
| `--live-idle-spinner` | `220 6% 28%` | Live Idle Spinner | Spinner color while connecting a live session |

### Media Attachments

Media preview containers and camera toggle

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--media-chat-bg` | `220 6% 28% / 0` | Media Preview (Chat) | Media attachment preview background in chat mode |
| `--media-sugg-bg` | `40 5% 90% / 0` | Media Preview (Suggest) | Media attachment preview background in suggestion mode |
| `--media-empty-bg` | `220 6% 28% / 0.08` | No Attachment Icon BG | Placeholder icon background when no media attached |
| `--media-empty-text` | `40 8% 43%` | No Attachment Icon | Placeholder icon color when no media attached |
| `--camera-toggle-text` | `220 6% 28%` | Camera Toggle Active | Camera toggle button active text color |
| `--imagegen-cam-icon` | `270 95% 70%` | Image Camera Icon | Camera icon when the image-generation camera is selected |
| `--imagegen-cam-active-text` | `271 81% 56%` | Image Camera Selected | The image-generation camera in the picker, selected |
| `--imagegen-cam-text` | `269 97% 61%` | Image Camera Option | The image-generation camera in the picker, unselected |
| `--attachment-toggle-shadow` | `229 84% 5% / 0.28` | Attachment Toggle Shadow | Drop shadow under the compact attachment mode toggle |

### API Key Gate

Setup screen for API key configuration

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--gate-bg` | `40 6% 99%` | Setup Panel Background | Background of the API key setup panel |
| `--gate-text` | `220 8% 16%` | Setup Panel Text | Primary text in the setup panel |
| `--gate-muted-text` | `220 5% 50%` | Setup Panel Muted | Supporting text in the setup panel |
| `--gate-input-bg` | `40 6% 99%` | Setup Input Background | Input field background in setup panel |
| `--gate-btn-bg` | `220 6% 28%` | Setup Action Button | Action button background in setup panel |
| `--gate-btn-text` | `40 8% 97%` | Setup Action Text | Action button text in setup panel |
| `--gate-error-text` | `220 5% 42%` | Setup Error Text | Error message text in setup panel |
| `--gate-accent` | `220 6% 28%` | Setup Icon Accent | Accent color for icons in setup panel |
| `--gate-ok-border` | `120 40% 60%` | Managed Account Ready Border | Border for a connected managed account |
| `--gate-error-border` | `0 60% 60%` | Managed Account Error Border | Border for a managed account that needs attention |
| `--gate-disclaimer-text` | `0 0% 100% / 0.55` | Disclaimer Text | Fine print under the API key form |
| `--gate-disclaimer-link-hover` | `0 0% 100% / 0.75` | Disclaimer Link (Hover) | A disclaimer link while pointed at |
| `--gate-disclaimer-underline` | `0 0% 100% / 0.25` | Disclaimer Link Underline | Underline beneath disclaimer links |

### Theme Customizer

Theme customization panel colors

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--theme-panel-bg` | `40 6% 99% / 0` | Theme Panel Background | Background of the theme customizer panel |
| `--theme-panel-text` | `220 8% 16%` | Theme Panel Text | Text in the theme customizer panel |
| `--theme-muted-text` | `220 5% 45%` | Theme Muted Text | Helper text in the theme customizer |
| `--theme-input-bg` | `40 8% 97% / 0` | Theme Input Background | Color input field background |
| `--theme-input-border` | `220 4% 76%` | Theme Input Border | Color input field border |
| `--theme-preset-btn` | `40 8% 97% / 0` | Theme Preset Button | Preset theme selector button background |

### CTA Buttons

Call-to-action buttons in message bubbles

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--cta-btn-bg` | `220 6% 28% / 0` | CTA Button Background | Background of call-to-action buttons like Setup Billing |
| `--cta-btn-text` | `40 8% 43%` | CTA Button Text | Text on call-to-action buttons |

### Annotation Save Button

Save/confirm button for image annotations

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--annotation-btn-bg` | `220 8% 18% / 0` | Annotation Save Button | Background of the annotation save button |
| `--annotation-btn-text` | `40 8% 43%` | Annotation Save Text | Text on the annotation save button |
| `--annotation-btn-hover` | `220 9% 13% / 0.14` | Annotation Save Hover | Hover background of the annotation save button |
| `--annotation-btn-focus` | `220 6% 28%` | Annotation Save Focus Ring | Focus ring color of the annotation save button |

### Translation Highlight

Active word highlighting during audio playback

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--marker-target-bg` | `40 8% 82%` | Target Highlight Background | Background for actively spoken target-language text |
| `--marker-target-text` | `220 8% 14%` | Target Highlight Text | Text color for actively spoken target-language text |
| `--marker-native-bg` | `220 5% 76%` | Native Highlight Background | Background for actively spoken native-language text |
| `--marker-native-text` | `220 8% 14%` | Native Highlight Text | Text color for actively spoken native-language text |

### Notebook Marks

Sketch lines, watercolor wash, and correction marks

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--pencil-stroke` | `220 7% 24%` | Pencil Stroke | Dark sketch strokes and strong notebook outlines |
| `--pencil-emphasis` | `220 8% 18%` | Pencil Emphasis | Emphasized pencil strokes and markups |
| `--sketch-line` | `220 5% 60%` | Sketch Line | Thin sketchy outlines and subtle notebook lines |
| `--sketch-shadow` | `220 8% 14%` | Sketch Shadow | Shadows in hand-drawn elements |
| `--watercolor-wash` | `220 4% 72%` | Watercolor Wash | Soft watercolor accent wash |
| `--correction-pen` | `220 5% 42%` | Correction Pen | Correction/error red pen color |

### Message Tape Effect

Translucent tape strips, wrinkles, and lifted tape shading on message bubbles

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--tape-bg-light` | `40 8% 91%` | Tape Light Fill | Lightest translucent tone in the tape gradient |
| `--tape-bg-mid` | `40 6% 94%` | Tape Mid Fill | Mid translucent tone in the tape gradient |
| `--tape-bg-dark` | `40 8% 88%` | Tape Dark Fill | Darkest translucent tone in the tape gradient |
| `--tape-border` | `40 5% 78%` | Tape Border | Thin border line around each tape strip |
| `--tape-shadow` | `220 8% 14%` | Tape Shadow | Outer shadow beneath tape strips and lifted corners |
| `--tape-inset` | `40 6% 96%` | Tape Inset Glow | Soft inner glow inside tape strips |
| `--tape-wrinkle` | `40 6% 84%` | Tape Wrinkle | Crease tint used in wrinkled tape strips |
| `--tape-highlight` | `0 0% 100%` | Tape Highlight | Glossy highlight streak inside tape strips |
| `--tape-crease` | `40 5% 72%` | Tape Crease Shadow | Lower crease shadow in wrinkled tape strips |

### Borders and Focus

Global outlines and focus glow

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--line-border` | `220 4% 82%` | Default Border | Most borders and separator lines |
| `--input-outline` | `220 4% 78%` | Input Outline | Text input outlines |
| `--focus-ring` | `220 7% 30%` | Focus Glow | Glow shown when controls are focused |

### Maestro Flag: Hold

Top-left flag when you pause maestro

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--flag-hold-bg` | `40 6% 62%` | Hold Background | Flag background in hold mode |
| `--flag-hold-border` | `40 7% 55%` | Hold Border | Flag border in hold mode |
| `--flag-hold-text` | `40 6% 99%` | Hold Text | Flag icon/text in hold mode |

### Maestro Flag: Speaking and Typing

Top-left flag while maestro is actively responding

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--flag-speaking-bg` | `220 6% 30%` | Speaking Background | Flag background while maestro is speaking |
| `--flag-speaking-border` | `220 7% 24%` | Speaking Border | Flag border while maestro is speaking |
| `--flag-speaking-text` | `40 8% 97%` | Speaking Text | Flag icon/text while maestro is speaking |
| `--flag-typing-bg` | `220 8% 20%` | Typing Background | Flag background while maestro is typing |
| `--flag-typing-border` | `220 9% 15%` | Typing Border | Flag border while maestro is typing |
| `--flag-typing-text` | `40 8% 97%` | Typing Text | Flag icon/text while maestro is typing |

### Maestro Flag: Listening, Observing, Idle

Top-left flag for passive or waiting states

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--flag-listening-bg` | `220 6% 42%` | Listening Background | Flag background while maestro is listening |
| `--flag-listening-border` | `220 6% 34%` | Listening Border | Flag border while maestro is listening |
| `--flag-listening-text` | `40 8% 97%` | Listening Text | Flag icon/text while maestro is listening |
| `--flag-observing-bg` | `40 5% 88%` | Observing Background | Flag background while observing quietly |
| `--flag-observing-border` | `220 4% 78%` | Observing Border | Flag border while observing quietly |
| `--flag-observing-text` | `220 5% 50%` | Observing Text | Flag icon/text while observing quietly |
| `--flag-engaging-bg` | `220 7% 26%` | About To Engage Background | Flag background when maestro is about to engage |
| `--flag-engaging-border` | `220 8% 20%` | About To Engage Border | Flag border when maestro is about to engage |
| `--flag-engaging-text` | `40 8% 97%` | About To Engage Text | Flag icon/text when maestro is about to engage |
| `--flag-idle-bg` | `40 5% 92%` | Idle Background | Flag background when nothing is running |
| `--flag-idle-border` | `220 4% 82%` | Idle Border | Flag border when nothing is running |
| `--flag-idle-text` | `220 5% 50%` | Idle Text | Flag icon/text when nothing is running |
| `--flag-busy-bg` | `220 5% 50%` | Busy Background | Flag background tint while background tasks are active |
| `--flag-busy-border` | `220 5% 42%` | Busy Border | Flag border tint while background tasks are active |
| `--flag-busy-text` | `220 5% 38%` | Busy Text | Flag icon/text while background tasks are active |

### API Key Button

Top-right button that shows key present/missing

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--apikey-ok-bg` | `220 6% 28% / 0` | Key Present Background | API key button background when key exists |
| `--apikey-ok-hover` | `220 7% 22% / 0.14` | Key Present Hover | API key button hover color when key exists |
| `--apikey-ok-text` | `40 8% 43%` | Key Present Text | API key button text/icon when key exists |
| `--apikey-missing-bg` | `220 5% 50% / 0` | Key Missing Background | API key button background when key is missing |
| `--apikey-missing-hover` | `220 5% 44% / 0.14` | Key Missing Hover | API key button hover color when key is missing |
| `--apikey-missing-text` | `40 8% 43%` | Key Missing Text | API key button text/icon when key is missing |

### Microphone Recording Button

Hold-to-record and listening mic states

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--mic-record-bg` | `220 9% 12%` | Mic Recording Background | Mic button while hold-to-record is active |
| `--mic-record-icon` | `40 8% 97%` | Mic Recording Icon | Mic icon while hold-to-record is active |
| `--mic-record-ring` | `220 10% 10%` | Mic Recording Ring | Ring around mic while hold-to-record is active |
| `--mic-stt-bg` | `220 8% 18%` | Mic Listening Background | Mic button while speech-to-text is listening |
| `--mic-stt-icon` | `40 8% 97%` | Mic Listening Icon | Mic icon while speech-to-text is listening |
| `--mic-pulse-outer` | `220 9% 12%` | Mic Pulse Outer | Outer pulse ring while hold-to-record is active |
| `--mic-pulse-inner` | `220 7% 22%` | Mic Pulse Inner | Inner pulse ring while hold-to-record is active |
| `--mic-record-glow` | `0 84% 60% / 0.8` | Microphone Recording Glow | Glow around the microphone icon while recording a note |

### Live and Attachment Recording Controls

Live chip, stop squares, remove buttons, and inline recording errors

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--live-badge-bg` | `220 9% 12%` | Live Badge Background | The small LIVE badge background in live preview |
| `--live-badge-text` | `40 8% 97%` | Live Badge Text | Text color inside the LIVE badge |
| `--live-badge-dot` | `40 8% 97%` | Live Badge Dot | Blinking dot inside the LIVE badge |
| `--live-stop-bg` | `220 9% 12%` | Live Stop Button Background | Round stop button background while live session is active |
| `--live-stop-hover` | `220 10% 8%` | Live Stop Button Hover | Round stop button hover color while live session is active |
| `--live-stop-text` | `40 8% 97%` | Live Stop Button Foreground | Foreground color on live-session stop button |
| `--live-stop-icon` | `40 8% 97%` | Live Stop Square Icon | Square stop icon for live-session stop button |
| `--vid-stop-bg` | `220 8% 16%` | Video Stop Button Background | Round stop button background while local video recording is active |
| `--vid-stop-hover` | `220 9% 12%` | Video Stop Button Hover | Round stop button hover while local video recording is active |
| `--vid-stop-text` | `40 8% 97%` | Video Stop Button Foreground | Foreground color on local-video stop button |
| `--vid-stop-icon` | `40 8% 97%` | Video Stop Square Icon | Square stop icon for local-video recording stop button |
| `--remove-attach-bg` | `220 9% 12%` | Remove Attachment Background | Round X button background for removing an attachment |
| `--remove-attach-hover` | `220 10% 8%` | Remove Attachment Hover | Round X button hover color for removing an attachment |
| `--remove-attach-icon` | `40 8% 97%` | Remove Attachment Icon | X icon color on remove-attachment button |
| `--rec-dot` | `220 5% 50%` | REC Dot | Tiny REC indicator dot while local recording is active |
| `--rec-error-bg` | `220 8% 16%` | Recording Error Background | Inline error background related to recording/live issues |
| `--rec-error-text` | `40 8% 97%` | Recording Error Text | Inline error text related to recording/live issues |
| `--top-live-active-bg` | `220 9% 10%` | Live Button Active Background | Live-session button background when session is active |
| `--top-live-active-hover` | `220 10% 7%` | Live Button Active Hover | Live-session button hover color when session is active |
| `--top-live-active-text` | `40 8% 97%` | Live Button Active Text | Live-session button text when session is active |
| `--top-live-error-bg` | `220 6% 34%` | Top Live Button Error Background | Top live button background when retry is needed |
| `--top-live-error-hover` | `220 7% 28%` | Top Live Button Error Hover | Top live button hover color when retry is needed |
| `--top-live-error-text` | `40 8% 97%` | Top Live Button Error Text | Top live button text when retry is needed |
| `--overlay-live-error-bg` | `220 6% 36%` | Overlay Live Button Error Background | Overlay LIVE button background when retry is needed |
| `--overlay-live-error-hover` | `220 7% 30%` | Overlay Live Button Error Hover | Overlay LIVE button hover color when retry is needed |
| `--overlay-live-error-text` | `40 8% 97%` | Overlay Live Button Error Text | Overlay LIVE button text when retry is needed |

### Action Confirmation Panels

Panels for load, delete, export, combine, and trim actions

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--action-load-label` | `40 6% 43%` | Load Action Label | Heading and prompt text of the load-all confirmation panel |
| `--action-delete-label` | `220 6% 28%` | Reset Action Label | Heading and prompt text of the reset confirmation panel |
| `--action-export-label` | `40 8% 43%` | Save Action Label | Heading and prompt text of the save confirmation panel |
| `--action-combine-label` | `220 7% 22%` | Combine Action Label | Heading and prompt text of the combine confirmation panel |
| `--action-trim-label` | `220 5% 45%` | Trim Action Label | Heading and prompt text of the trim confirmation panel |
| `--action-load-accent` | `40 6% 92%` | Load Panel | Panel background for load/import actions |
| `--action-load-text` | `220 8% 14%` | Load Panel Text | Text color in load/import panels |
| `--action-delete-accent` | `220 6% 28%` | Delete Panel | Panel background for delete/reset actions |
| `--action-delete-text` | `40 8% 97%` | Delete Panel Text | Text color in delete/reset panels |
| `--action-export-accent` | `40 8% 90%` | Export Panel | Panel background for export actions |
| `--action-export-text` | `220 8% 14%` | Export Panel Text | Text color in export panels |
| `--action-combine-accent` | `220 7% 22%` | Combine Panel | Panel background for merge/combine actions |
| `--action-combine-text` | `40 8% 97%` | Combine Panel Text | Text color in merge/combine panels |
| `--action-trim-accent` | `220 5% 50%` | Trim Panel | Panel background for trim actions |
| `--action-trim-text` | `40 8% 97%` | Trim Panel Text | Text color in trim panels |
| `--delete-shortcut-hover-bg` | `220 6% 34% / 0.14` | Delete Shortcut Hover Background | Hover background of the small delete shortcut button |
| `--delete-shortcut-hover-text` | `40 8% 97% / 0.14` | Delete Shortcut Hover Icon | Hover icon color of the small delete shortcut button |
| `--trim-shortcut-hover-bg` | `220 5% 56% / 0.14` | Trim Shortcut Hover Background | Hover background of the small trim shortcut button |
| `--trim-shortcut-hover-text` | `40 8% 97% / 0.14` | Trim Shortcut Hover Icon | Hover icon color of the small trim shortcut button |

### Voice Identity

Color ring identity for each voice character

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--voice-zephyr` | `220 6% 28%` | Voice: Zephyr | Identity color for Zephyr voice |
| `--voice-puck` | `40 6% 62%` | Voice: Puck | Identity color for Puck voice |
| `--voice-charon` | `220 5% 50%` | Voice: Charon | Identity color for Charon voice |
| `--voice-kore` | `40 8% 82%` | Voice: Kore | Identity color for Kore voice |
| `--voice-fenrir` | `220 8% 16%` | Voice: Fenrir | Identity color for Fenrir voice |

### Overlay Scrims

Dimming behind dialogs, panels and busy controls

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--scrim-modal` | `0 0% 0% / 0.45` | Dialog Backdrop | Dimming behind pop-up dialogs |
| `--scrim-gate` | `0 0% 0% / 0.4` | Setup Backdrop | Dimming behind the API key setup screens |
| `--scrim-panel` | `0 0% 0% / 0.2` | Panel Backdrop | Dimming behind slide-in panels such as the theme editor |
| `--scrim-busy` | `0 0% 0% / 0.6` | Busy Overlay | Dimming over a control while it is uploading or working |

### Media Overlay

Controls and labels drawn on top of photos, video and PDFs

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--media-letterbox` | `0 0% 0%` | Media Letterbox | Empty space around a photo or video inside its frame |
| `--media-overlay-icon` | `0 0% 100%` | Overlay Icon | Icon buttons drawn on top of an attachment |
| `--media-overlay-focus` | `0 0% 100% / 0.4` | Overlay Focus Ring | Focus ring on controls over an attachment |
| `--media-overlay-shadow` | `0 0% 0% / 0.72` | Overlay Icon Shadow | Drop shadow that keeps overlay icons readable on bright media |
| `--media-chip-bg` | `0 0% 0% / 0.6` | Media Label Background | Small labels over media, such as the PDF page counter |
| `--media-chip-text` | `0 0% 100%` | Media Label Text | Text inside labels drawn over media |
| `--media-loading-bg` | `0 0% 0% / 0.3` | Attachment Loading Badge | Backdrop of the loading animation over an attachment |
| `--media-error-scrim` | `0 0% 0% / 0.6` | Image Error Overlay | Dimming over an image that failed to generate |
| `--media-preview-veil` | `0 0% 0% / 0.2` | Camera Preview Veil | Dimming over the camera preview |
| `--media-preview-veil-hover` | `0 0% 0% / 0.4` | Camera Preview Veil (Hover) | Camera preview dimming while pointed at |
| `--media-ctrl-focus` | `0 0% 100% / 0.5` | Media Control Focus Ring | Focus ring on camera and live-session buttons |
| `--media-rec-chip-bg` | `0 0% 0% / 0.5` | Recording Badge Background | Backdrop of the REC badge on the camera preview |
| `--media-rec-chip-text` | `0 0% 100%` | Recording Badge Text | The word REC on the camera preview |
| `--media-observer-dot` | `158 64% 52%` | Observer Dot | Dot marking the silent observer as active |
| `--media-observer-dot-border` | `0 0% 0% / 0.4` | Observer Dot Outline | Outline around the observer dot |
| `--pdf-page-placeholder` | `0 0% 0% / 0.03` | PDF Page Placeholder | Blank page area before a PDF page finishes rendering |

### Mini-game Overlay

Status bubbles and controls drawn over a running mini-game

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--game-status-bg` | `0 0% 0% / 0.7` | Mini-game Status Bubble | Background of the launching and status bubble |
| `--game-status-error-bg` | `0 63% 31% / 0.8` | Mini-game Status Bubble (Error) | Status bubble background after a mini-game fails |
| `--game-status-text` | `0 0% 100%` | Mini-game Status Text | Text inside the mini-game status bubble |
| `--game-deck-bg` | `0 0% 0% / 0.55` | Mini-game Control Deck | Background of the floating control deck |
| `--game-deck-btn-bg` | `0 0% 0% / 0.35` | Mini-game Deck Button | Buttons inside the control deck |
| `--game-deck-btn-hover` | `0 0% 0% / 0.5` | Mini-game Deck Button (Hover) | Deck buttons while pointed at |
| `--game-deck-text` | `0 0% 100%` | Mini-game Deck Text | Labels inside the control deck |
| `--game-deck-subtle-text` | `0 0% 100% / 0.8` | Mini-game Deck Subtle Text | Secondary labels inside the control deck |
| `--game-deck-line` | `0 0% 100% / 0.25` | Mini-game Deck Divider | Divider lines inside the control deck |
| `--game-error-bg` | `0 75% 15% / 0.6` | Mini-game Crash Panel | Panel shown when a mini-game crashes |
| `--game-error-border` | `0 63% 31% / 0.4` | Mini-game Crash Panel Border | Border around the crash panel |
| `--game-error-text` | `0 96% 89%` | Mini-game Crash Text | Text inside the crash panel |
| `--game-error-btn-bg` | `0 63% 31% / 0.4` | Mini-game Retry Button | Retry button inside the crash panel |
| `--game-error-btn-hover` | `0 63% 31% / 0.6` | Mini-game Retry Button (Hover) | Retry button while pointed at |
| `--game-error-btn-border` | `0 91% 71% / 0.3` | Mini-game Retry Button Border | Border around the retry button |
| `--game-code-shadow` | `229 84% 5% / 0.18` | Mini-game Source Panel Shadow | Drop shadow under the mini-game source panel |

### Status Notices

Inline success and failure banners

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--notice-ok-bg` | `152 81% 96% / 0` | Success Notice Background | Background of a success banner |
| `--notice-ok-border` | `156 72% 67%` | Success Notice Border | Border around a success banner |
| `--notice-ok-text` | `164 86% 16%` | Success Notice Text | Text inside a success banner |
| `--notice-error-bg` | `0 86% 97% / 0` | Error Notice Background | Background of a failure banner |
| `--notice-error-border` | `0 94% 82%` | Error Notice Border | Border around a failure banner |
| `--notice-error-text` | `0 70% 35%` | Error Notice Text | Text inside a failure banner |

### Danger Actions

Destructive buttons and the account deletion area

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--danger-btn-bg` | `0 74% 42%` | Danger Button | Background of a destructive button, e.g. Delete account |
| `--danger-btn-hover` | `0 70% 35%` | Danger Button (Hover) | Destructive button while pointed at |
| `--danger-btn-text` | `0 0% 100%` | Danger Button Text | Text on a destructive button |
| `--danger-icon` | `0 74% 42%` | Danger Icon | Icons that flag a destructive or reporting action |
| `--danger-zone-bg` | `0 86% 97% / 0` | Danger Zone Background | Background of the account deletion area |
| `--danger-zone-border` | `0 94% 82% / 0.8` | Danger Zone Border | Border around the account deletion area |
| `--danger-zone-text` | `0 63% 31%` | Danger Zone Text | Text inside the account deletion area |
| `--danger-ghost-hover` | `0 93% 94% / 0.14` | Danger Ghost Button (Hover) | Outlined destructive button while pointed at |
| `--danger-input-bg` | `0 0% 100% / 0` | Danger Input Background | The type-DELETE confirmation field |
| `--danger-input-border` | `0 94% 82%` | Danger Input Border | Border of the confirmation field |
| `--danger-input-ring` | `0 91% 71%` | Danger Input Focus Ring | Focus ring on the confirmation field |

### Debug Log

The developer traffic log panel

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--debug-panel-bg` | `220 8% 14% / 0` | Debug Panel Background | Backdrop of the traffic log sheet |
| `--debug-header-bg` | `220 8% 14%` | Debug Panel Header | Header bar of the traffic log sheet |
| `--debug-card-bg` | `220 8% 14%` | Debug Log Card | Background of a single request row |
| `--debug-ok-text` | `142 69% 30%` | Debug Success Text | Timing and success markers in the traffic log |
| `--debug-ok-payload-text` | `142 77% 29%` | Debug Response Text | Body of a successful response |
| `--debug-error-text` | `0 91% 46%` | Debug Error Text | Error markers in the traffic log |
| `--debug-error-alt-text` | `0 94% 46%` | Debug Error Detail Text | Error type and body in the traffic log |
| `--debug-error-bg` | `0 63% 31% / 0` | Debug Error Row | Background of a failed request row |
| `--debug-error-border` | `0 70% 35%` | Debug Error Row Border | Border of a failed request row |
| `--debug-row-hover` | `0 0% 100% / 0.14` | Debug Row (Hover) | A traffic log row while pointed at |
| `--debug-payload-bg` | `0 0% 0% / 0` | Debug Payload Background | Background behind request and response bodies |
| `--debug-section-bg` | `0 0% 0% / 0` | Debug Section Background | Background of the response section |
