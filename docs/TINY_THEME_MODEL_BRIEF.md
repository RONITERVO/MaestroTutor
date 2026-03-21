# Tiny Theme Model Brief for Maestro Tutor

This file is the self-contained instruction brief for a small on-device language model that must generate app color themes from a short text prompt without seeing the UI.

It is based on the current app theme system in:

- `src/app/index.css`
- `src/features/theme/config/colorRegistry.ts`
- `src/features/theme/config/colorRenameMap.ts`
- `src/features/theme/utils/themeFileIO.ts`
- `src/app/App.tsx`
- `src/features/chat/components/ChatInterface.tsx`
- `src/features/chat/components/ChatMessageBubble.tsx`
- `src/features/chat/components/InputArea.tsx`
- `src/features/session/components/Header.tsx`
- `src/features/session/components/ApiKeyGate.tsx`

## Mission

Generate a safe theme preset for Maestro Tutor from a word or short sentence such as:

- `cozy forest notebook`
- `arctic lab`
- `sunset cafe`
- `dark ink cyber classroom`

You cannot see the app. You must rely on this document for visual structure, token meaning, layer order, and conflict prevention.

## Output Contract

Return a JSON object with this shape:

```json
{
  "name": "Theme name",
  "description": "Short human description",
  "colors": {
    "page-bg": "210 20% 97%",
    "page-text": "220 30% 20%"
  }
}
```

Rules:

- Use only modern token keys listed in this document.
- Each color value must be a raw HSL triplet string in this exact format: `"H S% L%"`.
- Do not output hex, RGB, `hsl(...)`, CSS variables, comments, or trailing alpha in the value.
- `colors` may be partial. Missing keys fall back to the app defaults.
- Partial themes are safe when you keep the app in the same overall light/dark polarity as default.
- If you flip the entire app to a dark theme, you must theme the main surface packs together. Do not darken only `page-bg` and leave the rest at light defaults.

## Runtime Facts

- Theme colors become CSS variables on `<html>`.
- Missing keys are removed from inline styles and revert to defaults.
- Theme files are imported as JSON with `{ name, description, colors }`.
- Old theme files may still use legacy keys and must be migrated first.
- Some UI classes apply opacity on top of tokens, so token values should be strong base colors, not already faded colors.

## What The App Looks Like

This section exists because you cannot see the UI.

### Overall Screen

- The whole app sits on a notebook-paper style page.
- The page background is full-screen and lightly textured.
- The main content area is a paper surface on top of the page background.
- Everything is vertical and mobile-first.

### Top Layer

- A fixed status flag sits at the top-left edge of the screen.
- That flag can be collapsed or expanded.
- A small circular status animation bubble appears just below the flag.
- When the flag is expanded, top-right floating buttons appear: API key button and debug button.
- These header controls float over the page and must remain readable regardless of what scrolls underneath.

### Main Scroll Area

- Chat messages are in a vertical scroll column.
- There is a large invisible spacer above the first message so the top message can scroll down.
- Messages look like hand-placed paper notes taped onto a notebook page.
- Assistant and user messages are distinct surfaces.
- Tape strips are decorative layers above the message card edges.
- Shadows and borders are part of the handmade look.

### Messages

- User messages are normally the darker and stronger bubble.
- Assistant messages are normally the lighter and calmer bubble.
- Status and thinking bubbles are neutral support bubbles.
- Error messages are separate and must read as warning or failure.
- Swipe trays for delete and bookmark actions appear at the left or right edge of a message.

### Attachments And Focused Media

- Messages can contain images, PDFs, text files, office files, audio, SVG, and mini-games.
- In focused media mode, attachments can become large dark visual surfaces inside the message area.
- Overlay transcript text sits on top of focused images, PDFs, and media.
- Overlay buttons can sit above the focused media in the corners.
- Overlay transcript text must usually be light, because the media layer beneath it is often darkened.

### Important Attachment Surface Types

- `overlay`: text on top of darkened image/video/PDF media. Use light text.
- `audio`: transcript on a separate light audio shell. Use dark text.
- `svg`: transcript on a separate light SVG shell. Use dark text.
- `game`: transcript on a separate light mini-game shell. Use dark text.
- `inline`: transcript inside the normal assistant or user message area. Use the usual bubble contrast.

This is why these token families are separate and must not be collapsed into one color blindly:

- `attachment-overlay-*`
- `attachment-audio-*`
- `attachment-svg-*`
- `attachment-game-*`
- `user-attachment-*`

### Composer Area

- Near the bottom of the scroll content there is a large input card.
- That card has an outer accent shell and an inner text entry surface.
- The app has two modes: chat mode and suggestion mode.
- Those two modes have separate input backgrounds, icon colors, send button colors, and outer container colors.
- Media previews and the live camera preview can appear below the composer.
- An annotation canvas can appear below the composer and temporarily becomes a dark editing viewport with overlay controls.

### Suggestions

- Suggestion chips appear below the input area.
- They are small rounded note-like buttons.
- Their hover, ring, and active colors must remain visible against the page background.

### Full-Screen Modal

- The API key gate is a centered modal panel on top of a dark translucent page overlay.
- The panel itself uses `gate-*` tokens.
- The dark backdrop is not theme-tokenized.
- The modal must remain readable over a dimmed page.

## Visual Stacks And What Conflicts With What

### Stack 1: Page

Layer order:

1. `page-bg`
2. paper texture
3. `paper-surface`
4. message cards and composer
5. floating header controls

Conflicts to avoid:

- `page-bg` and `paper-surface` should not fight each other. They should look related.
- `page-text` and `deep-ink` must both remain readable on `page-bg` and `paper-surface`.
- `paper-stripe` must be close to `paper-surface`, not louder than messages.

### Stack 2: Message Cards

Layer order:

1. page paper
2. message bubble background
3. tape strips
4. transcript text
5. swipe tray buttons

Conflicts to avoid:

- Tape should never become darker or more saturated than the bubble itself.
- User bubble text must never blend into user bubble background.
- Assistant bubble text must never blend into assistant bubble background.
- `ai-msg-bg` cannot be too close to `page-bg`, or assistant notes disappear into the page.
- `user-msg-bg` cannot be too close to `chat-input-bg` if both are visible near each other, or the UI loses hierarchy.

### Stack 3: Focused Media Overlay

Layer order:

1. focused image/video/PDF
2. darkening overlay or dark media
3. transcript text
4. overlay icon buttons

Conflicts to avoid:

- `attachment-overlay-target-text`, `attachment-overlay-native-text`, and `user-attachment-overlay-text` must stay light.
- `overlay-live-error-*` is also on a dark media context and must stay high contrast.
- Do not reuse dark inline text colors for overlay transcript text.

### Stack 4: Audio, SVG, And Game Shells

Layer order:

1. light specialized shell
2. transcript text
3. controls or highlights

Conflicts to avoid:

- `attachment-audio-*`, `attachment-svg-*`, and `attachment-game-*` should usually be dark text on lighter surfaces.
- Do not copy overlay text colors into these tokens unless you are deliberately making the shells dark too.

### Stack 5: Composer

Layer order:

1. outer container: `chat-outer-*` or `sugg-outer-*`
2. inner input surface: `chat-input-*` or `sugg-input-*`
3. icon buttons
4. send button
5. error strips below

Conflicts to avoid:

- `chat-outer-bg` and `chat-input-bg` must be different enough to feel nested.
- `send-btn-bg` must stand out from `chat-input-bg`.
- `send-sugg-btn-bg` must stand out from `sugg-input-bg`.
- `input-focus-ring` must remain visible on both chat and suggestion input surfaces.
- `input-error-text` must be readable on `input-error-bg`.
- `snapshot-error-bg` should read as special input error tint, not the same as the base input surface.

### Stack 6: Floating Header

Layer order:

1. page
2. status flag
3. top-right buttons

Conflicts to avoid:

- Flag text must remain readable regardless of the page behind it.
- API key button states must still communicate missing vs present.
- `flag-busy-*` are used as translucent tint tokens, so matching hue across bg, border, and text is acceptable if the hue itself is strong.

## Hard Rules

- Never make a background and its text token identical or near-identical.
- Never make page paper, assistant bubbles, and neutral panels all exactly the same value.
- Never use dark overlay text on focused media overlays.
- Never use invisible focus rings.
- Never make destructive or recording buttons fade into their surroundings.
- Never let tape become the loudest thing in the UI.
- Never output a token key that does not exist in the modern registry.
- Never collapse separate overlay shell text families into one shared value unless the underlying surfaces are also intentionally made the same.

## Safe Theme Strategy

### Strategy A: Light Theme Or Light-First Theme

This is the safest strategy for a small model.

- Keep `page-bg`, `paper-surface`, `ai-msg-bg`, `sugg-input-bg`, `gate-bg`, and `theme-panel-bg` in the light range.
- Make the user bubble and chat composer the stronger surface.
- Use one accent hue for focus, chips, and main actions.
- Keep danger and recording/live-stop reds distinct and strong.
- Keep overlay transcript text light.

If you are unsure, use this strategy.

### Strategy B: True Dark Theme

Only do this if you can theme the whole surface pack together.

If `page-bg` lightness goes below about `22%`, you should also set at least these:

- `page-text`, `paper-surface`, `paper-stripe`, `deep-ink`
- `ai-msg-bg`, `ai-msg-text`
- `status-msg-bg`, `status-msg-text`
- `thinking-bubble-bg`, `thinking-bubble-text`
- `sugg-input-bg`, `sugg-input-text`, `sugg-input-icon`
- `send-btn-bg`, `send-btn-text`
- `send-sugg-btn-bg`, `send-sugg-btn-text`
- `history-peek-bg`, `history-peek-icon`, `history-btn-bg`, `history-btn-hover`
- `suggestion-bg`, `suggestion-hover`, `suggestion-ring`, `suggestion-active-bg`, `suggestion-active-text`
- `audio-player-bg`, `audio-time-text`
- `web-results-bg`, `web-results-link`
- `gate-bg`, `gate-text`, `gate-muted-text`, `gate-input-bg`, `gate-btn-bg`, `gate-btn-text`, `gate-accent`
- `theme-panel-bg`, `theme-panel-text`, `theme-muted-text`, `theme-input-bg`, `theme-input-border`, `theme-preset-btn`
- `line-border`, `input-outline`, `focus-ring`

If you cannot theme those together, do not publish a true dark theme. Keep the app light-first.

## Recommended Generation Procedure

### Step 1: Extract Mood

Map the prompt into:

- paper temperature: warm, neutral, cool
- contrast level: soft, medium, high
- energy: calm, lively, urgent
- saturation level: muted, balanced, vivid

### Step 2: Pick Anchor Colors

Pick these anchor ideas first:

- paper base
- paper stripe
- main ink
- muted neutral text
- user bubble
- assistant bubble
- main accent
- success/live accent
- danger/recording accent
- overlay light text
- shell dark text
- tape neutral

### Step 3: Fill Structural Tokens First

Fill these before anything decorative:

- page and paper
- user and assistant bubbles
- chat and suggestion composer surfaces
- borders and focus
- suggestion chips
- overlay transcript text
- live/recording controls
- flag states

### Step 4: Derive The Rest

Use related values for decorative and secondary tokens. Keep specialized tokens separate only when the underlying surfaces differ.

### Step 5: Run Safety Checks

Check:

- text vs its background
- nested surfaces vs parent surface
- overlay text vs overlay context
- hover vs resting state
- focus ring vs its surface

### Step 6: Prefer Stability Over Novelty

If a prompt is vague, stay conservative:

- keep surfaces coherent
- keep page readable
- keep overlay text safe
- keep destructive/live controls obvious

## Safe Couplings

These couplings are safe starting points unless the prompt explicitly needs a different behavior.

### Usually Safe To Match Or Closely Derive

- `paper-surface` from `page-bg`
- `paper-stripe` from `paper-surface`
- `deep-ink` from `page-text`, but darker or stronger
- `chat-input-bg` from `user-msg-bg`
- `chat-input-text` and `chat-input-icon` from `user-msg-text`
- `sugg-input-bg` from `ai-msg-bg`
- `sugg-input-text` from `ai-msg-text`
- `thinking-bubble-bg` from `status-msg-bg`
- `thinking-bubble-text` from a muted text color
- `user-attachment-inline-text` from `user-msg-text`
- `user-attachment-audio-text` from `user-msg-text`
- `user-attachment-overlay-text` from a light overlay text color
- `attachment-inline-target-text` from `ai-msg-text`
- `attachment-inline-native-text` from muted assistant text
- `attachment-audio-target-text` from shell dark text
- `attachment-audio-native-text` from shell dark text or muted shell text
- `attachment-svg-target-text` from shell dark text
- `attachment-svg-native-text` from muted shell text
- `attachment-game-target-text` from shell dark text
- `attachment-game-native-text` from muted shell text
- `live-stop-icon`, `vid-stop-icon`, `remove-attach-icon` from their corresponding foreground text

### Usually Keep Separate

- `attachment-overlay-*` vs `attachment-audio-*`
- `attachment-overlay-*` vs `attachment-svg-*`
- `chat-outer-bg` vs `chat-input-bg`
- `sugg-outer-bg` vs `sugg-input-bg`
- `marker-target-bg` vs `marker-native-bg`
- `top-live-error-*` vs `overlay-live-error-*`
- `error-msg-bg` vs `status-msg-bg`
- `save-sugg-bg` vs `clear-sugg-bg`

## Recommended Lightness Gaps

Use these as heuristics, not strict math.

| Pair | Recommended gap |
|---|---|
| text on main surface | 55+ lightness points |
| text on strong or dark chip | 60+ lightness points |
| nested surface vs parent surface | 8 to 22 lightness points |
| hover vs rest | 3 to 8 lightness points |
| paper stripe vs paper | 3 to 8 lightness points |
| focus ring vs touched surface | 18+ lightness points or clear hue shift |
| tape vs bubble beneath | tape should be subtler than bubble |

If you cannot estimate lightness confidently, keep light surfaces light and dark text dark.

## Token Inventory

Only use these modern keys.

### Page Canvas

`page-bg`, `page-text`, `paper-surface`, `paper-stripe`, `deep-ink`

### Chat Message Bubbles

`user-msg-bg`, `user-msg-text`, `ai-msg-bg`, `ai-msg-text`, `status-msg-bg`, `status-msg-text`, `error-msg-bg`, `error-msg-text`, `thinking-bubble-bg`, `thinking-bubble-text`

### Message Sub-elements

`ai-msg-placeholder`, `ai-file-bg`, `ai-file-text`, `img-error-text`

### Attachment Transcript Text

`user-attachment-inline-text`, `user-attachment-audio-text`, `user-attachment-overlay-text`, `user-attachment-svg-text`, `user-attachment-game-text`, `attachment-inline-target-text`, `attachment-inline-native-text`, `attachment-audio-target-text`, `attachment-audio-native-text`, `attachment-overlay-target-text`, `attachment-overlay-native-text`, `attachment-svg-target-text`, `attachment-svg-native-text`, `attachment-game-target-text`, `attachment-game-native-text`

### Chat Input Area

`chat-input-bg`, `chat-input-text`, `chat-input-icon`, `chat-input-icon-hover-bg`, `sugg-input-bg`, `sugg-input-text`, `sugg-input-icon`, `send-btn-bg`, `send-btn-text`, `send-sugg-btn-bg`, `send-sugg-btn-text`, `input-focus-ring`, `input-error-bg`, `input-error-text`, `snapshot-error-bg`, `chat-outer-bg`, `chat-outer-text`, `sugg-outer-bg`

### Chat Interface Chrome

`history-peek-bg`, `history-peek-icon`, `history-btn-bg`, `history-btn-hover`, `delete-msg-bg`, `delete-msg-text`, `save-sugg-bg`, `save-sugg-text`, `clear-sugg-bg`, `clear-sugg-text`, `web-results-bg`, `web-results-link`

### Audio Player

`audio-player-bg`, `audio-play-btn`, `audio-play-text`, `audio-bar`, `audio-time-text`

### Bookmark Actions

`bookmark-bg`, `bookmark-text`, `bookmark-input-bg`, `bookmark-input-text`, `bookmark-divider`

### Suggestions List

`suggestion-bg`, `suggestion-hover`, `suggestion-ring`, `suggestion-double-ring`, `suggestion-active-bg`, `suggestion-active-text`

### Session Controls

`profile-label-text`, `profile-input-accent`, `scroll-wheel-target-accent`, `globe-native-accent`, `globe-target-accent`, `maestro-avatar-glow`, `profile-btn-bg`, `profile-btn-text`, `profile-accept-bg`, `profile-accept-text`, `mode-toggle-bg`, `mode-toggle-text`, `save-chat-text`, `ctrl-muted-text`

### Header

`debug-btn-bg`, `debug-btn-text`, `debug-btn-muted`, `loading-spinner`

### Live Session Idle Button

`live-idle-btn-bg`, `live-idle-btn-text`, `live-idle-sugg-btn-bg`, `live-idle-sugg-btn-text`, `live-idle-spinner`

### Media Attachments

`media-chat-bg`, `media-sugg-bg`, `media-empty-bg`, `media-empty-text`, `camera-toggle-text`

### API Key Gate

`gate-bg`, `gate-text`, `gate-muted-text`, `gate-input-bg`, `gate-btn-bg`, `gate-btn-text`, `gate-error-text`, `gate-accent`

### Theme Customizer

`theme-panel-bg`, `theme-panel-text`, `theme-muted-text`, `theme-input-bg`, `theme-input-border`, `theme-preset-btn`

### CTA Buttons

`cta-btn-bg`, `cta-btn-text`

### Annotation Save Button

`annotation-btn-bg`, `annotation-btn-text`, `annotation-btn-hover`, `annotation-btn-focus`

### Translation Highlight

`marker-target-bg`, `marker-target-text`, `marker-native-bg`, `marker-native-text`

### Notebook Marks

`pencil-stroke`, `pencil-emphasis`, `sketch-line`, `sketch-shadow`, `watercolor-wash`, `correction-pen`

### Message Tape Effect

`tape-bg-light`, `tape-bg-mid`, `tape-bg-dark`, `tape-border`, `tape-shadow`, `tape-inset`, `tape-wrinkle`, `tape-highlight`, `tape-crease`

### Borders And Focus

`line-border`, `input-outline`, `focus-ring`

### Maestro Flag: Hold

`flag-hold-bg`, `flag-hold-border`, `flag-hold-text`

### Maestro Flag: Speaking And Typing

`flag-speaking-bg`, `flag-speaking-border`, `flag-speaking-text`, `flag-typing-bg`, `flag-typing-border`, `flag-typing-text`

### Maestro Flag: Listening, Observing, Idle

`flag-listening-bg`, `flag-listening-border`, `flag-listening-text`, `flag-observing-bg`, `flag-observing-border`, `flag-observing-text`, `flag-engaging-bg`, `flag-engaging-border`, `flag-engaging-text`, `flag-idle-bg`, `flag-idle-border`, `flag-idle-text`, `flag-busy-bg`, `flag-busy-border`, `flag-busy-text`

### API Key Button

`apikey-ok-bg`, `apikey-ok-hover`, `apikey-ok-text`, `apikey-missing-bg`, `apikey-missing-hover`, `apikey-missing-text`

### Microphone Recording Button

`mic-record-bg`, `mic-record-icon`, `mic-record-ring`, `mic-stt-bg`, `mic-stt-icon`, `mic-pulse-outer`, `mic-pulse-inner`

### Live And Attachment Recording Controls

`live-badge-bg`, `live-badge-text`, `live-badge-dot`, `live-stop-bg`, `live-stop-hover`, `live-stop-text`, `live-stop-icon`, `vid-stop-bg`, `vid-stop-hover`, `vid-stop-text`, `vid-stop-icon`, `remove-attach-bg`, `remove-attach-hover`, `remove-attach-icon`, `rec-dot`, `rec-error-bg`, `rec-error-text`, `top-live-active-bg`, `top-live-active-hover`, `top-live-active-text`, `top-live-error-bg`, `top-live-error-hover`, `top-live-error-text`, `overlay-live-error-bg`, `overlay-live-error-hover`, `overlay-live-error-text`

### Action Confirmation Panels

`action-load-bg`, `action-load-text`, `action-delete-bg`, `action-delete-text`, `action-export-bg`, `action-export-text`, `action-combine-bg`, `action-combine-text`, `action-trim-bg`, `action-trim-text`, `delete-shortcut-hover-bg`, `delete-shortcut-hover-text`, `trim-shortcut-hover-bg`, `trim-shortcut-hover-text`

### Voice Identity

`voice-zephyr`, `voice-puck`, `voice-charon`, `voice-kore`, `voice-fenrir`

## What To Leave At Default If You Are Unsure

These can safely stay at default if your prompt is short and you already themed the main structural surfaces:

- `voice-*`
- `action-*`
- `theme-panel-*`
- `profile-*`
- `globe-*`
- `scroll-wheel-target-accent`
- `maestro-avatar-glow`

But do not leave overlay transcript text, main surfaces, or composer surfaces unresolved.

## Migration Code For Older Theme Files

Use this exact migration logic as a starting point before generating or publishing a theme. This maps old shared keys into the current per-element token system.

```ts
/**
 * Legacy theme migration starting point.
 * Input and output are both { [key: string]: "H S% L%" } maps.
 */
export const COLOR_RENAME_MAP: Record<string, string[]> = {
  background: ['page-bg'],
  foreground: ['page-text', 'ai-msg-text', 'status-msg-text', 'sugg-input-text', 'bookmark-input-text', 'gate-text', 'theme-panel-text'],
  paper: ['paper-surface', 'input-error-text', 'profile-accept-text', 'annotation-btn-text'],
  'paper-dark': ['paper-stripe', 'suggestion-hover'],
  ink: ['deep-ink'],

  primary: ['user-msg-bg', 'chat-input-bg', 'profile-btn-bg', 'mode-toggle-bg', 'debug-btn-bg', 'live-idle-btn-bg'],
  'primary-foreground': ['user-msg-text', 'chat-input-text', 'chat-input-icon', 'profile-btn-text', 'mode-toggle-text', 'debug-btn-text', 'live-idle-btn-text'],

  secondary: ['status-msg-bg', 'thinking-bubble-bg', 'ai-file-bg', 'sugg-outer-bg', 'history-peek-bg', 'history-btn-hover', 'web-results-bg', 'audio-player-bg', 'suggestion-bg', 'media-sugg-bg'],
  'secondary-foreground': ['status-msg-text'],

  accent: ['chat-outer-bg', 'delete-msg-bg', 'save-sugg-bg', 'audio-play-btn', 'audio-bar', 'bookmark-bg', 'suggestion-ring', 'suggestion-active-bg', 'media-chat-bg', 'media-empty-bg', 'gate-btn-bg', 'gate-accent', 'cta-btn-bg', 'camera-toggle-text'],
  'accent-foreground': ['chat-outer-text', 'delete-msg-text', 'save-sugg-text', 'clear-sugg-text', 'audio-play-text', 'bookmark-text', 'suggestion-active-text', 'media-empty-text', 'gate-btn-text', 'cta-btn-text'],

  card: ['ai-msg-bg', 'sugg-input-bg', 'send-btn-bg', 'history-btn-bg', 'bookmark-input-bg', 'gate-bg', 'gate-input-bg', 'theme-panel-bg', 'theme-input-bg', 'theme-preset-btn'],
  'card-foreground': ['ai-msg-text'],
  popover: ['theme-panel-bg'],
  'popover-foreground': ['theme-panel-text'],

  muted: ['theme-panel-bg'],
  'muted-foreground': ['thinking-bubble-text', 'ai-file-text', 'sugg-input-icon', 'audio-time-text', 'ctrl-muted-text', 'debug-btn-muted', 'gate-muted-text', 'theme-muted-text'],

  destructive: ['error-msg-bg', 'error-msg-text', 'gate-error-text'],
  'destructive-foreground': ['gate-btn-text'],

  border: ['line-border'],
  input: ['input-outline'],
  ring: ['focus-ring'],

  pencil: ['pencil-stroke', 'pencil-emphasis', 'ai-msg-placeholder', 'profile-accept-bg', 'save-chat-text', 'annotation-btn-bg'],
  'pencil-light': ['sketch-line', 'input-focus-ring', 'history-peek-icon', 'bookmark-divider', 'loading-spinner', 'theme-input-border'],
  'pencil-mark': ['pencil-emphasis'],
  watercolor: ['watercolor-wash', 'web-results-link', 'profile-label-text', 'profile-input-accent'],
  highlight: ['marker-target-bg', 'marker-native-bg'],
  'highlight-text': ['marker-target-text', 'marker-native-text'],
  'marker-bg': ['marker-target-bg', 'marker-native-bg'],
  'marker-text': ['marker-target-text', 'marker-native-text'],
  correction: ['correction-pen', 'img-error-text', 'input-error-bg'],
  eraser: ['clear-sugg-bg'],
  'sketch-shadow': ['sketch-shadow'],

  'status-hold-bg': ['flag-hold-bg'],
  'status-hold-border': ['flag-hold-border'],
  'status-hold-text': ['flag-hold-text'],
  'status-speaking-bg': ['flag-speaking-bg'],
  'status-speaking-border': ['flag-speaking-border'],
  'status-speaking-text': ['flag-speaking-text'],
  'status-typing-bg': ['flag-typing-bg'],
  'status-typing-border': ['flag-typing-border'],
  'status-typing-text': ['flag-typing-text'],
  'status-listening-bg': ['flag-listening-bg'],
  'status-listening-border': ['flag-listening-border'],
  'status-listening-text': ['flag-listening-text'],
  'status-observing-bg': ['flag-observing-bg'],
  'status-observing-border': ['flag-observing-border'],
  'status-observing-text': ['flag-observing-text'],
  'status-observing-high-bg': ['flag-engaging-bg'],
  'status-observing-high-border': ['flag-engaging-border'],
  'status-observing-high-text': ['flag-engaging-text'],
  'status-idle-bg': ['flag-idle-bg'],
  'status-idle-border': ['flag-idle-border'],
  'status-idle-text': ['flag-idle-text'],
  'status-busy-bg': ['flag-busy-bg'],
  'status-busy-border': ['flag-busy-border'],
  'status-busy-text': ['flag-busy-text'],

  'api-key-valid-bg': ['apikey-ok-bg'],
  'api-key-valid-hover-bg': ['apikey-ok-hover'],
  'api-key-valid-text': ['apikey-ok-text'],
  'api-key-missing-bg': ['apikey-missing-bg'],
  'api-key-missing-hover-bg': ['apikey-missing-hover'],
  'api-key-missing-text': ['apikey-missing-text'],

  'recording-mic-armed-bg': ['mic-record-bg'],
  'recording-mic-armed-text': ['mic-record-icon'],
  'recording-mic-armed-ring': ['mic-record-ring'],
  'recording-mic-listening-bg': ['mic-stt-bg'],
  'recording-mic-listening-text': ['mic-stt-icon'],
  'recording-mic-pulse-outer': ['mic-pulse-outer'],
  'recording-mic-pulse-inner': ['mic-pulse-inner'],

  'recording-live-chip-bg': ['live-badge-bg'],
  'recording-live-chip-text': ['live-badge-text'],
  'recording-live-chip-dot': ['live-badge-dot'],
  'recording-live-stop-bg': ['live-stop-bg'],
  'recording-live-stop-hover-bg': ['live-stop-hover'],
  'recording-live-stop-text': ['live-stop-text'],
  'recording-live-stop-icon': ['live-stop-icon'],
  'recording-local-stop-bg': ['vid-stop-bg'],
  'recording-local-stop-hover-bg': ['vid-stop-hover'],
  'recording-local-stop-text': ['vid-stop-text'],
  'recording-local-stop-icon': ['vid-stop-icon'],
  'recording-remove-bg': ['remove-attach-bg'],
  'recording-remove-hover-bg': ['remove-attach-hover'],
  'recording-remove-text': ['remove-attach-icon'],
  'recording-indicator-dot': ['rec-dot'],
  'recording-inline-error-bg': ['rec-error-bg'],
  'recording-inline-error-text': ['rec-error-text'],

  'live-session-button-active-bg': ['top-live-active-bg'],
  'live-session-button-active-hover-bg': ['top-live-active-hover'],
  'live-session-button-active-text': ['top-live-active-text'],
  'live-session-button-error-bg': ['top-live-error-bg'],
  'live-session-button-error-hover-bg': ['top-live-error-hover'],
  'live-session-button-error-text': ['top-live-error-text'],
  'live-overlay-button-error-bg': ['overlay-live-error-bg'],
  'live-overlay-button-error-hover-bg': ['overlay-live-error-hover'],
  'live-overlay-button-error-text': ['overlay-live-error-text'],

  'action-load': ['action-load-bg'],
  'action-load-text': ['action-load-text'],
  'action-danger': ['action-delete-bg'],
  'action-danger-text': ['action-delete-text'],
  'action-export': ['action-export-bg'],
  'action-export-text': ['action-export-text'],
  'action-combine': ['action-combine-bg'],
  'action-combine-text': ['action-combine-text'],
  'action-trim': ['action-trim-bg'],
  'action-trim-text': ['action-trim-text'],
  'action-danger-shortcut-hover-bg': ['delete-shortcut-hover-bg'],
  'action-danger-shortcut-hover-text': ['delete-shortcut-hover-text'],
  'action-trim-shortcut-hover-bg': ['trim-shortcut-hover-bg'],
  'action-trim-shortcut-hover-text': ['trim-shortcut-hover-text'],
};

const LEGACY_KEYS = new Set(Object.keys(COLOR_RENAME_MAP));

const DERIVED_COLOR_FALLBACKS: Array<{ source: string; targets: string[] }> = [
  {
    source: 'ai-msg-text',
    targets: ['attachment-inline-target-text', 'attachment-game-target-text'],
  },
  {
    source: 'ai-file-text',
    targets: ['attachment-inline-native-text', 'attachment-game-native-text'],
  },
  {
    source: 'attachment-inline-target-text',
    targets: ['attachment-audio-target-text'],
  },
  {
    source: 'attachment-inline-native-text',
    targets: ['attachment-audio-native-text'],
  },
  {
    source: 'attachment-game-target-text',
    targets: ['attachment-svg-target-text'],
  },
  {
    source: 'attachment-game-native-text',
    targets: ['attachment-svg-native-text'],
  },
  {
    source: 'user-msg-text',
    targets: [
      'user-attachment-inline-text',
      'user-attachment-audio-text',
      'user-attachment-overlay-text',
      'user-attachment-svg-text',
      'user-attachment-game-text',
      'attachment-overlay-target-text',
      'attachment-overlay-native-text',
    ],
  },
];

const expandDerivedColorTokens = (colors: Record<string, string>): Record<string, string> => {
  const expanded = { ...colors };

  for (const { source, targets } of DERIVED_COLOR_FALLBACKS) {
    const sourceValue = expanded[source];
    if (typeof sourceValue !== 'string' || !sourceValue.trim()) continue;

    for (const target of targets) {
      if (!(target in expanded)) {
        expanded[target] = sourceValue;
      }
    }
  }

  return expanded;
};

export const hasLegacyColorKeys = (colors?: Record<string, string>): boolean => {
  if (!colors) return false;
  return Object.keys(colors).some((key) => LEGACY_KEYS.has(key));
};

export const migrateLegacyColorMap = (colors?: Record<string, string>): Record<string, string> => {
  if (!colors) return {};

  const migrated: Record<string, string> = {};

  for (const [key, value] of Object.entries(colors)) {
    if (!LEGACY_KEYS.has(key) && typeof value === 'string') {
      migrated[key] = value;
    }
  }

  for (const [legacyKey, value] of Object.entries(colors)) {
    if (typeof value !== 'string') continue;
    const targets = COLOR_RENAME_MAP[legacyKey];
    if (!targets || targets.length === 0) continue;

    for (const targetKey of targets) {
      if (!(targetKey in migrated)) {
        migrated[targetKey] = value;
      }
    }
  }

  return expandDerivedColorTokens(migrated);
};
```

## Optional Safety Normalizer

If you can run a small post-process step after generating colors, use something like this to repair the worst conflicts. This is not a full design engine. It is only a minimal guardrail.

```ts
const readLightness = (hsl: string): number => {
  const parts = hsl.trim().split(/\s+/);
  if (parts.length < 3) return 50;
  return parseFloat(parts[2]) || 50;
};

const withLightness = (hsl: string, nextL: number): string => {
  const parts = hsl.trim().split(/\s+/);
  if (parts.length < 3) return hsl;
  const h = parts[0];
  const s = parts[1];
  const l = Math.max(0, Math.min(100, Math.round(nextL)));
  return `${h} ${s} ${l}%`;
};

const ensureTextContrast = (
  colors: Record<string, string>,
  bgKey: string,
  fgKey: string,
  lightFallback = '0 0% 98%',
  darkFallback = '220 30% 18%',
  minGap = 55
) => {
  const bg = colors[bgKey];
  const fg = colors[fgKey];
  if (!bg || !fg) return;

  const bgL = readLightness(bg);
  const fgL = readLightness(fg);
  if (Math.abs(bgL - fgL) >= minGap) return;

  colors[fgKey] = bgL < 45 ? lightFallback : darkFallback;
};

export const normalizeGeneratedTheme = (input: Record<string, string>) => {
  const colors = migrateLegacyColorMap(input);

  ensureTextContrast(colors, 'page-bg', 'page-text');
  ensureTextContrast(colors, 'paper-surface', 'deep-ink');
  ensureTextContrast(colors, 'user-msg-bg', 'user-msg-text');
  ensureTextContrast(colors, 'ai-msg-bg', 'ai-msg-text');
  ensureTextContrast(colors, 'status-msg-bg', 'status-msg-text');
  ensureTextContrast(colors, 'thinking-bubble-bg', 'thinking-bubble-text', '0 0% 98%', '220 15% 35%', 35);
  ensureTextContrast(colors, 'chat-input-bg', 'chat-input-text');
  ensureTextContrast(colors, 'sugg-input-bg', 'sugg-input-text');
  ensureTextContrast(colors, 'send-btn-bg', 'send-btn-text');
  ensureTextContrast(colors, 'send-sugg-btn-bg', 'send-sugg-btn-text');
  ensureTextContrast(colors, 'input-error-bg', 'input-error-text');
  ensureTextContrast(colors, 'gate-bg', 'gate-text');
  ensureTextContrast(colors, 'gate-btn-bg', 'gate-btn-text');
  ensureTextContrast(colors, 'theme-panel-bg', 'theme-panel-text');
  ensureTextContrast(colors, 'annotation-btn-bg', 'annotation-btn-text');
  ensureTextContrast(colors, 'marker-target-bg', 'marker-target-text', '220 30% 18%', '0 0% 98%', 40);
  ensureTextContrast(colors, 'marker-native-bg', 'marker-native-text', '220 30% 18%', '0 0% 98%', 40);
  ensureTextContrast(colors, 'overlay-live-error-bg', 'overlay-live-error-text');
  ensureTextContrast(colors, 'top-live-error-bg', 'top-live-error-text');
  ensureTextContrast(colors, 'live-stop-bg', 'live-stop-text');
  ensureTextContrast(colors, 'vid-stop-bg', 'vid-stop-text');
  ensureTextContrast(colors, 'remove-attach-bg', 'remove-attach-icon');

  if (colors['paper-surface'] && colors['paper-stripe']) {
    const baseL = readLightness(colors['paper-surface']);
    const stripeL = readLightness(colors['paper-stripe']);
    if (Math.abs(baseL - stripeL) < 3) {
      colors['paper-stripe'] = withLightness(colors['paper-stripe'], baseL > 50 ? baseL - 4 : baseL + 4);
    }
  }

  return colors;
};
```

## Final Publishing Checklist

Before publishing a generated theme, verify all of these are true:

- The theme JSON uses modern token names.
- All color strings are raw HSL triplets.
- User bubble text is readable.
- Assistant bubble text is readable.
- Page text is readable.
- Composer text is readable in both chat and suggestion modes.
- Overlay transcript text is readable on dark media.
- Send buttons stand out from the input surface.
- Suggestion ring and focus ring are visible.
- Header flag remains readable.
- Live and recording controls remain obvious.
- Tape is subtle.
- If the theme is dark, the full surface pack was themed together.

If any of those fail, repair the theme instead of publishing it.
