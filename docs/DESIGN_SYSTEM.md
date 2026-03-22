# Maestro Tutor Color and Design System

This is the canonical color and UI styling reference for contributors.

## Overview

- Last updated: 2026-03-15
- Active color tokens: 222
- Token groups: 28
- Legacy migration keys supported: 91
- Token model: one token per visual UI element (1:1 element token mapping)
- Source of truth files: `src/app/index.css`, `index.html`, `src/features/theme/config/colorRegistry.ts`, `src/features/theme/config/presetThemes.ts`

## Non-Negotiable Rules

- Every visual element gets its own token. Do not reuse unrelated tokens to "save" variables.
- Do not hardcode color utilities for product UI (examples to avoid: `hover:bg-white/20`, `ring-green-500`, `shadow-[0_0_15px_rgba(...)]`).
- If a mode changes meaningfully (chat mode vs suggestion mode, native vs target, idle vs active), use mode-specific tokens.
- State-specific styling needs state-specific tokens (`-hover`, `-ring`, `-focus`, `-spinner`, `-glow`, etc.) when that state should be independently themeable.
- New UI is not complete until token wiring is done across all token source files.

## Naming Convention

- Pattern: `domain-element-role` (examples: `user-msg-bg`, `live-stop-icon`, `annotation-btn-focus`).
- Typical suffixes:
  - `-bg`: background fill
  - `-text`: text or icon color
  - `-border`: border stroke
  - `-hover`: hover state fill
  - `-ring` or `-focus`: focus treatment
  - `-spinner` or `-glow`: dedicated indicator or glow color

## Required Contributor Workflow

When adding or changing a colorized element:

1. Add or update CSS variable defaults in `src/app/index.css` (`:root`).
2. Register token in Tailwind color map in `index.html` (`tailwind.config.theme.extend.colors`).
3. Add token metadata to `src/features/theme/config/colorRegistry.ts` with friendly name and description.
5. Add token to `src/features/theme/config/presetThemes.ts` (Original preset block).
6. Use the tokenized utility class in JSX/TSX; remove any direct hardcoded color utility or literal color.
7. If replacing legacy token keys, add mapping to `src/features/theme/config/colorRenameMap.ts`.
8. Validate with build + visual pass + Theme Customizer coverage.

## Recent Token Isolation Updates (2026-03-15)

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

## Legacy Migration

- Legacy key map: `src/features/theme/config/colorRenameMap.ts`.
- Persisted settings migration: `src/store/slices/settingsSlice.ts`.
- Imported preset migration: `src/features/theme/utils/themeFileIO.ts`.
- Legacy keys are supported for migration compatibility only; do not use them in new code.

## Full Token Inventory

Generated from `colorRegistry.ts`

### Page Canvas

The full-screen paper and main writing color

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--page-bg` | `210 20% 97%` | Page Background | Main app background behind everything |
| `--page-text` | `220 30% 20%` | Page Text | Default text color used across the app |
| `--paper-surface` | `210 20% 97%` | Paper Surface | Notebook paper areas in the main content |
| `--paper-stripe` | `210 15% 92%` | Paper Stripe | Darker paper stripes and paper depth |
| `--deep-ink` | `230 40% 20%` | Deep Ink | Strong deep-ink text and marks |

### Chat Message Bubbles

Backgrounds and text for each message type

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--user-msg-bg` | `220 30% 20%` | User Message Background | Background of your sent message bubbles |
| `--user-msg-text` | `210 20% 98%` | User Message Text | Text and icons inside your messages |
| `--ai-msg-bg` | `210 25% 99%` | AI Message Background | Background of assistant reply bubbles |
| `--ai-msg-text` | `220 30% 20%` | AI Message Text | Text inside assistant replies |
| `--status-msg-bg` | `210 15% 90%` | Status Message Background | Background of system/status messages |
| `--status-msg-text` | `220 30% 20%` | Status Message Text | Text inside system/status messages |
| `--error-msg-bg` | `350 70% 50%` | Error Message Background | Background of error message bubbles |
| `--error-msg-text` | `350 70% 50%` | Error Message Text | Text color for error messages |
| `--thinking-bubble-bg` | `210 15% 90%` | Thinking Indicator Background | Background of the thinking... bubble |
| `--thinking-bubble-text` | `220 15% 45%` | Thinking Indicator Text | Text in the thinking... bubble |

### Message Sub-elements

File attachments, image placeholders, and error indicators within messages

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--ai-msg-placeholder` | `220 25% 30%` | AI Image Placeholder | Placeholder background while AI image loads (focused view) |
| `--ai-file-bg` | `210 15% 90%` | AI File Attachment | Background of file attachments in assistant messages |
| `--ai-file-text` | `220 15% 45%` | AI File Text | Text and icon color in assistant file attachments |
| `--img-error-text` | `350 70% 50%` | Image Error Text | Error text color for image generation failures |

### Attachment Transcript Text

Separate text colors for user and assistant attachment text across inline, audio, overlay, SVG, and mini-game placements

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--user-attachment-inline-text` | `210 20% 98%` | User Inline Attachment Text | Your message text and inline attachment labels next to standard attachments |
| `--user-attachment-audio-text` | `210 20% 98%` | User Audio Attachment Text | Your message text when an audio attachment is shown in the audio shell |
| `--user-attachment-overlay-text` | `210 20% 98%` | User Overlay Attachment Text | Your message text shown over focused images, PDFs, and other media overlays |
| `--user-attachment-svg-text` | `0 0% 0%` | User SVG Attachment Text | Your message text shown in the detached SVG attachment shell |
| `--user-attachment-game-text` | `0 0% 0%` | User Game Attachment Text | Your message text shown with mini-game attachment shells and controls |
| `--attachment-inline-target-text` | `220 30% 20%` | Inline Target Text | Main attachment transcript text shown under attachments, including music replies |
| `--attachment-inline-native-text` | `220 15% 45%` | Inline Native Text | Secondary or native attachment transcript text shown under attachments, including music replies |
| `--attachment-audio-target-text` | `0 0% 0%` | Audio Target Text | Main text in the focused assistant audio scroll wheel |
| `--attachment-audio-native-text` | `0 0% 0%` | Audio Native Text | Secondary or native text in the focused assistant audio scroll wheel |
| `--attachment-overlay-target-text` | `210 20% 98%` | Overlay Target Text | Main transcript text shown over images, PDFs, and focused attachments |
| `--attachment-overlay-native-text` | `210 20% 98%` | Overlay Native Text | Secondary or native transcript text shown over images, PDFs, and focused attachments |
| `--attachment-svg-target-text` | `220 30% 20%` | SVG Target Text | Main transcript text in the focused SVG shell layout |
| `--attachment-svg-native-text` | `220 15% 45%` | SVG Native Text | Secondary or native transcript text in the focused SVG shell layout |
| `--attachment-game-target-text` | `220 30% 20%` | Game Target Text | Main transcript text when the transcript overlaps a mini-game |
| `--attachment-game-native-text` | `220 15% 45%` | Game Native Text | Secondary or native transcript text when the transcript overlaps a mini-game |

### Chat Input Area

Message composer in chat and suggestion modes

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--chat-input-bg` | `220 30% 20%` | Chat Input Background | Input field background in chat mode |
| `--chat-input-text` | `210 20% 98%` | Chat Input Text | Text color in chat mode input |
| `--chat-input-icon` | `210 20% 98%` | Chat Input Icons | Icon button color inside chat input |
| `--chat-input-icon-hover-bg` | `0 0% 100%` | Chat Icon Hover | Hover background for chat-mode icon buttons |
| `--sugg-input-bg` | `210 25% 99%` | Suggestion Input Background | Input field background in suggestion mode |
| `--sugg-input-text` | `220 30% 20%` | Suggestion Input Text | Text color in suggestion mode input |
| `--sugg-input-icon` | `220 15% 45%` | Suggestion Input Icons | Icon button color in suggestion mode |
| `--send-btn-bg` | `210 25% 99%` | Send Button Background | Background of the send message button |
| `--send-btn-text` | `220 30% 20%` | Send Button Text | Text/icon color on the send button |
| `--send-sugg-btn-bg` | `210 25% 99%` | Suggest Send Background | Background of the send/create button in suggestion mode |
| `--send-sugg-btn-text` | `220 30% 20%` | Suggest Send Text | Text/icon color of the suggestion-mode send/create button |
| `--input-focus-ring` | `220 15% 65%` | Input Focus Ring | Ring shown when the input field is focused |
| `--input-error-bg` | `350 70% 50%` | Input Error Background | Background for error messages in the input area |
| `--input-error-text` | `210 20% 97%` | Input Error Text | Text color for input area error messages |
| `--snapshot-error-bg` | `220 25% 30%` | Snapshot Error Background | Background tint for snapshot-related input errors |
| `--chat-outer-bg` | `220 70% 55%` | Chat Mode Container | Outer container background in chat mode |
| `--chat-outer-text` | `210 25% 99%` | Chat Mode Container Text | Text in the outer chat mode container |
| `--sugg-outer-bg` | `210 15% 90%` | Suggestion Mode Container | Outer container background in suggestion mode |

### Chat Interface Chrome

History peek, navigation buttons, and suggestion controls

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--history-peek-bg` | `210 15% 90%` | History Peek Background | Background of the message history peek zone |
| `--history-peek-icon` | `220 15% 65%` | History Peek Eye Icon | Eye icon color in the history peek zone |
| `--history-btn-bg` | `210 25% 99%` | History Button Background | Background of history navigation buttons |
| `--history-btn-hover` | `210 15% 90%` | History Button Hover | Hover color for history navigation buttons |
| `--delete-msg-bg` | `220 70% 55%` | Delete Message Button | Background of the delete message button |
| `--delete-msg-text` | `210 25% 99%` | Delete Message Text | Icon color on the delete message button |
| `--save-sugg-bg` | `220 70% 55%` | Save Suggestion Button | Background of the save suggestion button |
| `--save-sugg-text` | `210 25% 99%` | Save Suggestion Text | Text on the save suggestion button |
| `--clear-sugg-bg` | `350 70% 65%` | Clear Suggestion Button | Background of the clear suggestion button |
| `--clear-sugg-text` | `210 25% 99%` | Clear Suggestion Text | Text on the clear suggestion button |
| `--web-results-bg` | `210 15% 90%` | Web Results Container | Background of the web search results area |
| `--web-results-link` | `190 60% 55%` | Web Results Link | Link color in web search results |

### Audio Player

Playback controls for recorded audio messages

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--audio-player-bg` | `210 15% 90%` | Audio Player Background | Background of the audio playback bar |
| `--audio-play-btn` | `220 70% 55%` | Audio Play Button | Background of the play/pause button |
| `--audio-play-text` | `210 25% 99%` | Audio Play Icon | Icon color on the play/pause button |
| `--audio-bar` | `220 70% 55%` | Audio Progress Bar | Color of the audio progress bar |
| `--audio-time-text` | `220 15% 45%` | Audio Time Display | Time text in the audio player |

### Bookmark Actions

Bookmark save panel and manage buttons

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--bookmark-bg` | `220 70% 55%` | Bookmark Panel | Background of the bookmark save panel |
| `--bookmark-text` | `210 25% 99%` | Bookmark Text | Text and button color in bookmark panel |
| `--bookmark-input-bg` | `210 25% 99%` | Bookmark Input | Background of the bookmark name input field |
| `--bookmark-input-text` | `220 30% 20%` | Bookmark Input Text | Text color in the bookmark name field |
| `--bookmark-divider` | `220 15% 65%` | Bookmark Divider | Line divider between bookmark sections |

### Suggestions List

Translation suggestion items

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--suggestion-bg` | `210 15% 90%` | Suggestion Background | Background of individual suggestion lines |
| `--suggestion-hover` | `210 15% 92%` | Suggestion Hover | Hover background for suggestion lines |
| `--suggestion-ring` | `220 70% 55%` | Suggestion Focus Ring | Focus ring around selected suggestion |
| `--suggestion-double-ring` | `220 70% 55%` | Suggestion Confirm Ring | Focus ring for suggestions on double-click/confirm interaction |
| `--suggestion-active-bg` | `220 70% 55%` | Creating Suggestion | Background while a suggestion is being created |
| `--suggestion-active-text` | `210 25% 99%` | Creating Suggestion Text | Text while a suggestion is being created |

### Session Controls

Profile editing, mode toggle, and sidebar controls

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--profile-label-text` | `190 60% 55%` | Profile Label Text | Profile heading label color |
| `--profile-input-accent` | `190 60% 55%` | Profile Input Accent | Accent color for profile input fields and borders |
| `--scroll-wheel-target-accent` | `142 71% 45%` | Scroll Wheel Target Accent | Scroll focus ring for the non-native language wheel |
| `--globe-native-accent` | `190 60% 55%` | Globe Native Accent | Border and glow for the native-language marker on the globe |
| `--globe-target-accent` | `142 71% 60%` | Globe Target Accent | Border and glow for the target-language marker on the globe |
| `--maestro-avatar-glow` | `22 53% 49%` | Maestro Avatar Glow | Glow color around the maestro avatar when an image is present |
| `--profile-btn-bg` | `220 30% 20%` | Profile Button | Background of profile edit/label buttons |
| `--profile-btn-text` | `210 20% 98%` | Profile Button Text | Text on profile edit/label buttons |
| `--profile-accept-bg` | `220 25% 30%` | Profile Accept Button | Background of the profile accept/confirm button |
| `--profile-accept-text` | `210 20% 97%` | Profile Accept Text | Text on the profile accept button |
| `--mode-toggle-bg` | `220 30% 20%` | Mode Toggle Container | Background of the All/This mode toggle |
| `--mode-toggle-text` | `210 20% 98%` | Mode Toggle Text | Text on mode toggle buttons |
| `--save-chat-text` | `220 25% 30%` | Save Chat Label | Text color for the save chat action label |
| `--ctrl-muted-text` | `220 15% 45%` | Controls Muted Text | Dimmed text in session controls |

### Header

Debug button and loading indicator in the app header

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--debug-btn-bg` | `220 30% 20%` | Debug Button | Background of the debug logs button |
| `--debug-btn-text` | `210 20% 98%` | Debug Button Text | Text on the debug logs button |
| `--debug-btn-muted` | `220 15% 45%` | Debug Button Muted | Muted text state of the debug button |
| `--loading-spinner` | `220 15% 65%` | Loading Spinner | Color of the loading spinner indicator |

### Live Session Idle Button

Live session button when no session is active

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--live-idle-btn-bg` | `220 30% 20%` | Live Idle Button | Live session button background when idle |
| `--live-idle-btn-text` | `210 20% 98%` | Live Idle Button Text | Live session button text when idle |
| `--live-idle-sugg-btn-bg` | `210 15% 90%` | Live Suggest Idle Background | Live session button background when idle in suggestion mode |
| `--live-idle-sugg-btn-text` | `220 30% 20%` | Live Suggest Idle Text | Live session button text when idle in suggestion mode |
| `--live-idle-spinner` | `210 20% 98%` | Live Idle Spinner | Spinner color while connecting a live session |

### Media Attachments

Media preview containers and camera toggle

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--media-chat-bg` | `220 70% 55%` | Media Preview (Chat) | Media attachment preview background in chat mode |
| `--media-sugg-bg` | `210 15% 90%` | Media Preview (Suggest) | Media attachment preview background in suggestion mode |
| `--media-empty-bg` | `220 70% 55%` | No Attachment Icon BG | Placeholder icon background when no media attached |
| `--media-empty-text` | `210 25% 99%` | No Attachment Icon | Placeholder icon color when no media attached |
| `--camera-toggle-text` | `220 70% 55%` | Camera Toggle Active | Camera toggle button active text color |

### API Key Gate

Setup screen for API key configuration

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--gate-bg` | `210 25% 99%` | Setup Panel Background | Background of the API key setup panel |
| `--gate-text` | `220 30% 20%` | Setup Panel Text | Primary text in the setup panel |
| `--gate-muted-text` | `220 15% 45%` | Setup Panel Muted | Supporting text in the setup panel |
| `--gate-input-bg` | `210 25% 99%` | Setup Input Background | Input field background in setup panel |
| `--gate-btn-bg` | `220 70% 55%` | Setup Action Button | Action button background in setup panel |
| `--gate-btn-text` | `210 25% 99%` | Setup Action Text | Action button text in setup panel |
| `--gate-error-text` | `350 70% 50%` | Setup Error Text | Error message text in setup panel |
| `--gate-accent` | `220 70% 55%` | Setup Icon Accent | Accent color for icons in setup panel |

### Theme Customizer

Theme customization panel colors

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--theme-panel-bg` | `210 25% 99%` | Theme Panel Background | Background of the theme customizer panel |
| `--theme-panel-text` | `220 30% 20%` | Theme Panel Text | Text in the theme customizer panel |
| `--theme-muted-text` | `220 15% 45%` | Theme Muted Text | Helper text in the theme customizer |
| `--theme-input-bg` | `210 25% 99%` | Theme Input Background | Color input field background |
| `--theme-input-border` | `220 15% 65%` | Theme Input Border | Color input field border |
| `--theme-preset-btn` | `210 25% 99%` | Theme Preset Button | Preset theme selector button background |

### CTA Buttons

Call-to-action buttons in message bubbles

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--cta-btn-bg` | `220 70% 55%` | CTA Button Background | Background of call-to-action buttons like Setup Billing |
| `--cta-btn-text` | `210 25% 99%` | CTA Button Text | Text on call-to-action buttons |

### Annotation Save Button

Save/confirm button for image annotations

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--annotation-btn-bg` | `220 25% 30%` | Annotation Save Button | Background of the annotation save button |
| `--annotation-btn-text` | `210 20% 97%` | Annotation Save Text | Text on the annotation save button |
| `--annotation-btn-hover` | `220 25% 24%` | Annotation Save Hover | Hover background of the annotation save button |
| `--annotation-btn-focus` | `220 25% 30%` | Annotation Save Focus Ring | Focus ring color of the annotation save button |

### Translation Highlight

Active word highlighting during audio playback

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--marker-target-bg` | `60 85% 80%` | Target Highlight Background | Background for actively spoken target-language text |
| `--marker-target-text` | `220 30% 20%` | Target Highlight Text | Text color for actively spoken target-language text |
| `--marker-native-bg` | `190 80% 84%` | Native Highlight Background | Background for actively spoken native-language text |
| `--marker-native-text` | `220 30% 20%` | Native Highlight Text | Text color for actively spoken native-language text |

### Notebook Marks

Sketch lines, watercolor wash, and correction marks

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--pencil-stroke` | `220 25% 30%` | Pencil Stroke | Dark sketch strokes and strong notebook outlines |
| `--pencil-emphasis` | `220 25% 30%` | Pencil Emphasis | Emphasized pencil strokes and markups |
| `--sketch-line` | `220 15% 65%` | Sketch Line | Thin sketchy outlines and subtle notebook lines |
| `--sketch-shadow` | `220 30% 20%` | Sketch Shadow | Shadows in hand-drawn elements |
| `--watercolor-wash` | `190 60% 55%` | Watercolor Wash | Soft watercolor accent wash |
| `--correction-pen` | `350 70% 50%` | Correction Pen | Correction/error red pen color |

### Message Tape Effect

Translucent tape strips, wrinkles, and lifted tape shading on message bubbles

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--tape-bg-light` | `50 30% 92%` | Tape Light Fill | Lightest translucent tone in the tape gradient |
| `--tape-bg-mid` | `50 20% 95%` | Tape Mid Fill | Mid translucent tone in the tape gradient |
| `--tape-bg-dark` | `50 30% 90%` | Tape Dark Fill | Darkest translucent tone in the tape gradient |
| `--tape-border` | `40 20% 80%` | Tape Border | Thin border line around each tape strip |
| `--tape-shadow` | `220 30% 20%` | Tape Shadow | Outer shadow beneath tape strips and lifted corners |
| `--tape-inset` | `50 20% 95%` | Tape Inset Glow | Soft inner glow inside tape strips |
| `--tape-wrinkle` | `40 25% 85%` | Tape Wrinkle | Crease tint used in wrinkled tape strips |
| `--tape-highlight` | `0 0% 100%` | Tape Highlight | Glossy highlight streak inside tape strips |
| `--tape-crease` | `40 20% 70%` | Tape Crease Shadow | Lower crease shadow in wrinkled tape strips |

### Borders and Focus

Global outlines and focus glow

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--line-border` | `210 15% 82%` | Default Border | Most borders and separator lines |
| `--input-outline` | `210 15% 82%` | Input Outline | Text input outlines |
| `--focus-ring` | `220 40% 40%` | Focus Glow | Glow shown when controls are focused |

### Maestro Flag: Hold

Top-left flag when you pause maestro

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--flag-hold-bg` | `292 84% 61%` | Hold Background | Flag background in hold mode |
| `--flag-hold-border` | `292 84% 61%` | Hold Border | Flag border in hold mode |
| `--flag-hold-text` | `0 0% 100%` | Hold Text | Flag icon/text in hold mode |

### Maestro Flag: Speaking and Typing

Top-left flag while maestro is actively responding

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--flag-speaking-bg` | `220 70% 55%` | Speaking Background | Flag background while maestro is speaking |
| `--flag-speaking-border` | `220 70% 55%` | Speaking Border | Flag border while maestro is speaking |
| `--flag-speaking-text` | `210 25% 99%` | Speaking Text | Flag icon/text while maestro is speaking |
| `--flag-typing-bg` | `220 70% 49%` | Typing Background | Flag background while maestro is typing |
| `--flag-typing-border` | `220 70% 49%` | Typing Border | Flag border while maestro is typing |
| `--flag-typing-text` | `210 25% 99%` | Typing Text | Flag icon/text while maestro is typing |

### Maestro Flag: Listening, Observing, Idle

Top-left flag for passive or waiting states

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--flag-listening-bg` | `220 25% 30%` | Listening Background | Flag background while maestro is listening |
| `--flag-listening-border` | `220 25% 30%` | Listening Border | Flag border while maestro is listening |
| `--flag-listening-text` | `210 20% 97%` | Listening Text | Flag icon/text while maestro is listening |
| `--flag-observing-bg` | `210 15% 90%` | Observing Background | Flag background while observing quietly |
| `--flag-observing-border` | `210 15% 82%` | Observing Border | Flag border while observing quietly |
| `--flag-observing-text` | `220 15% 45%` | Observing Text | Flag icon/text while observing quietly |
| `--flag-engaging-bg` | `220 68% 53%` | About To Engage Background | Flag background when maestro is about to engage |
| `--flag-engaging-border` | `220 68% 53%` | About To Engage Border | Flag border when maestro is about to engage |
| `--flag-engaging-text` | `210 25% 99%` | About To Engage Text | Flag icon/text when maestro is about to engage |
| `--flag-idle-bg` | `210 15% 92%` | Idle Background | Flag background when nothing is running |
| `--flag-idle-border` | `210 15% 82%` | Idle Border | Flag border when nothing is running |
| `--flag-idle-text` | `220 15% 45%` | Idle Text | Flag icon/text when nothing is running |
| `--flag-busy-bg` | `190 60% 55%` | Busy Background | Flag background tint while background tasks are active |
| `--flag-busy-border` | `190 60% 55%` | Busy Border | Flag border tint while background tasks are active |
| `--flag-busy-text` | `190 60% 55%` | Busy Text | Flag icon/text while background tasks are active |

### API Key Button

Top-right button that shows key present/missing

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--apikey-ok-bg` | `161 94% 30%` | Key Present Background | API key button background when key exists |
| `--apikey-ok-hover` | `161 94% 25%` | Key Present Hover | API key button hover color when key exists |
| `--apikey-ok-text` | `0 0% 100%` | Key Present Text | API key button text/icon when key exists |
| `--apikey-missing-bg` | `347 77% 50%` | Key Missing Background | API key button background when key is missing |
| `--apikey-missing-hover` | `347 77% 45%` | Key Missing Hover | API key button hover color when key is missing |
| `--apikey-missing-text` | `0 0% 100%` | Key Missing Text | API key button text/icon when key is missing |

### Microphone Recording Button

Hold-to-record and listening mic states

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--mic-record-bg` | `0 72% 51%` | Mic Recording Background | Mic button while hold-to-record is active |
| `--mic-record-icon` | `0 0% 100%` | Mic Recording Icon | Mic icon while hold-to-record is active |
| `--mic-record-ring` | `0 72% 51%` | Mic Recording Ring | Ring around mic while hold-to-record is active |
| `--mic-stt-bg` | `0 72% 56%` | Mic Listening Background | Mic button while speech-to-text is listening |
| `--mic-stt-icon` | `0 0% 100%` | Mic Listening Icon | Mic icon while speech-to-text is listening |
| `--mic-pulse-outer` | `0 72% 51%` | Mic Pulse Outer | Outer pulse ring while hold-to-record is active |
| `--mic-pulse-inner` | `0 72% 51%` | Mic Pulse Inner | Inner pulse ring while hold-to-record is active |

### Live and Attachment Recording Controls

Live chip, stop squares, remove buttons, and inline recording errors

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--live-badge-bg` | `0 72% 51%` | Live Badge Background | The small LIVE badge background in live preview |
| `--live-badge-text` | `0 0% 100%` | Live Badge Text | Text color inside the LIVE badge |
| `--live-badge-dot` | `0 0% 100%` | Live Badge Dot | Blinking dot inside the LIVE badge |
| `--live-stop-bg` | `0 72% 51%` | Live Stop Button Background | Round stop button background while live session is active |
| `--live-stop-hover` | `0 72% 45%` | Live Stop Button Hover | Round stop button hover color while live session is active |
| `--live-stop-text` | `0 0% 100%` | Live Stop Button Foreground | Foreground color on live-session stop button |
| `--live-stop-icon` | `0 0% 100%` | Live Stop Square Icon | Square stop icon for live-session stop button |
| `--vid-stop-bg` | `0 72% 56%` | Video Stop Button Background | Round stop button background while local video recording is active |
| `--vid-stop-hover` | `0 72% 48%` | Video Stop Button Hover | Round stop button hover while local video recording is active |
| `--vid-stop-text` | `0 0% 100%` | Video Stop Button Foreground | Foreground color on local-video stop button |
| `--vid-stop-icon` | `0 0% 100%` | Video Stop Square Icon | Square stop icon for local-video recording stop button |
| `--remove-attach-bg` | `0 72% 51%` | Remove Attachment Background | Round X button background for removing an attachment |
| `--remove-attach-hover` | `0 72% 45%` | Remove Attachment Hover | Round X button hover color for removing an attachment |
| `--remove-attach-icon` | `0 0% 100%` | Remove Attachment Icon | X icon color on remove-attachment button |
| `--rec-dot` | `0 72% 51%` | REC Dot | Tiny REC indicator dot while local recording is active |
| `--rec-error-bg` | `0 72% 51%` | Recording Error Background | Inline error background related to recording/live issues |
| `--rec-error-text` | `0 0% 100%` | Recording Error Text | Inline error text related to recording/live issues |
| `--top-live-active-bg` | `0 72% 51%` | Live Button Active Background | Live-session button background when session is active |
| `--top-live-active-hover` | `0 72% 45%` | Live Button Active Hover | Live-session button hover color when session is active |
| `--top-live-active-text` | `0 0% 100%` | Live Button Active Text | Live-session button text when session is active |
| `--top-live-error-bg` | `220 70% 55%` | Top Live Button Error Background | Top live button background when retry is needed |
| `--top-live-error-hover` | `220 70% 50%` | Top Live Button Error Hover | Top live button hover color when retry is needed |
| `--top-live-error-text` | `220 30% 20%` | Top Live Button Error Text | Top live button text when retry is needed |
| `--overlay-live-error-bg` | `220 74% 59%` | Overlay Live Button Error Background | Overlay LIVE button background when retry is needed |
| `--overlay-live-error-hover` | `220 74% 53%` | Overlay Live Button Error Hover | Overlay LIVE button hover color when retry is needed |
| `--overlay-live-error-text` | `210 25% 99%` | Overlay Live Button Error Text | Overlay LIVE button text when retry is needed |

### Action Confirmation Panels

Panels for load, delete, export, combine, and trim actions

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--action-load-bg` | `217 91% 60%` | Load Panel | Panel background for load/import actions |
| `--action-load-text` | `214 95% 93%` | Load Panel Text | Text color in load/import panels |
| `--action-delete-bg` | `0 72% 51%` | Delete Panel | Panel background for delete/reset actions |
| `--action-delete-text` | `0 86% 97%` | Delete Panel Text | Text color in delete/reset panels |
| `--action-export-bg` | `188 95% 43%` | Export Panel | Panel background for export actions |
| `--action-export-text` | `188 100% 94%` | Export Panel Text | Text color in export panels |
| `--action-combine-bg` | `263 70% 50%` | Combine Panel | Panel background for merge/combine actions |
| `--action-combine-text` | `263 70% 93%` | Combine Panel Text | Text color in merge/combine panels |
| `--action-trim-bg` | `25 95% 53%` | Trim Panel | Panel background for trim actions |
| `--action-trim-text` | `33 100% 96%` | Trim Panel Text | Text color in trim panels |
| `--delete-shortcut-hover-bg` | `0 75% 54%` | Delete Shortcut Hover Background | Hover background of the small delete shortcut button |
| `--delete-shortcut-hover-text` | `0 86% 97%` | Delete Shortcut Hover Icon | Hover icon color of the small delete shortcut button |
| `--trim-shortcut-hover-bg` | `28 95% 56%` | Trim Shortcut Hover Background | Hover background of the small trim shortcut button |
| `--trim-shortcut-hover-text` | `34 100% 97%` | Trim Shortcut Hover Icon | Hover icon color of the small trim shortcut button |

### Voice Identity

Color ring identity for each voice character

| CSS Variable | Default HSL | Friendly Name | Description |
|---|---|---|---|
| `--voice-zephyr` | `188 79% 41%` | Voice: Zephyr | Identity color for Zephyr voice |
| `--voice-puck` | `43 96% 56%` | Voice: Puck | Identity color for Puck voice |
| `--voice-charon` | `220 15% 65%` | Voice: Charon | Identity color for Charon voice |
| `--voice-kore` | `217 91% 60%` | Voice: Kore | Identity color for Kore voice |
| `--voice-fenrir` | `350 70% 50%` | Voice: Fenrir | Identity color for Fenrir voice |

