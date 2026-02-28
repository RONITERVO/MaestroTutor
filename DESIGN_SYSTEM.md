# Maestro Tutor - Design System

A complete reference for the Sketch/Notebook design system used throughout the app.
All UI should feel like a hand-drawn notebook: warm, organic, paper-like.

---

## Color Tokens

All colors are defined as HSL values in CSS custom properties (`src/app/index.css`).

### Core Palette

| Token | HSL | Hex (approx) | Usage |
|-------|-----|---------------|-------|
| `--background` | `39 35% 93%` | `#ece4d4` | Page background, body |
| `--foreground` | `25 15% 20%` | `#3d3228` | Primary text color |
| `--card` | `39 40% 96%` | `#f6f1e8` | Card/panel backgrounds |
| `--card-foreground` | `25 15% 20%` | `#3d3228` | Text on cards |
| `--primary` | `25 20% 25%` | `#4d3d30` | User message bubbles, emphasis |
| `--primary-foreground` | `39 35% 93%` | `#ece4d4` | Text on primary backgrounds |
| `--secondary` | `35 25% 85%` | `#ddd4c4` | Secondary surfaces, bookmark bars |
| `--secondary-foreground` | `25 15% 25%` | `#4d3d30` | Text on secondary |
| `--muted` | `35 20% 88%` | `#e3dcd0` | Disabled/muted backgrounds |
| `--muted-foreground` | `25 10% 45%` | `#807568` | De-emphasized text |
| `--accent` | `18 60% 55%` | `#c06a3a` | Warm orange highlights, CTAs |
| `--accent-foreground` | `39 35% 96%` | `#f6f1e8` | Text on accent |
| `--destructive` | `0 65% 50%` | `#d42d2d` | Destructive actions |
| `--destructive-foreground` | `39 35% 96%` | `#f6f1e8` | Text on destructive |

### Border & Input

| Token | HSL | Hex (approx) | Usage |
|-------|-----|---------------|-------|
| `--border` | `25 18% 70%` | `#b8a998` | Default border color |
| `--input` | `25 18% 75%` | `#c4b6a6` | Input field borders |
| `--ring` | `25 20% 35%` | `#6b5845` | Focus ring color |

### Sketch-Specific Tokens

| Token | HSL | Hex (approx) | Usage |
|-------|-----|---------------|-------|
| `--paper` | `39 35% 93%` | `#ece4d4` | Paper white (off-white) |
| `--paper-dark` | `35 12% 85%` | `#dbd6ce` | Aged/darker paper |
| `--pencil` | `25 15% 25%` | `#4d3d30` | Pencil strokes, dark borders |
| `--pencil-light` | `25 10% 55%` | `#968a7e` | Light pencil, subtle lines |
| `--pencil-mark` | `25 20% 35%` | `#6b5845` | Darker pencil for emphasis |
| `--sketch-shadow` | `25 15% 20%` | `#3d3228` | Shadow color |
| `--eraser` | `0 60% 65%` | `#d45c5c` | Eraser/deletion actions |
| `--watercolor` | `200 45% 55%` | `#5099cc` | Water blue accent (use sparingly) |
| `--ink` | `220 30% 20%` | `#243042` | Dark ink text |

### Semantic Notebook Tokens

| Token | HSL | Hex (approx) | Usage |
|-------|-----|---------------|-------|
| `--highlight` | `43 65% 75%` | `#dfc77a` | Highlighter marker (speaking highlights) |
| `--highlight-text` | `30 40% 20%` | `#473620` | Text on highlight |
| `--correction` | `5 45% 55%` | `#c76858` | Teacher's red pencil (errors, corrections) |

### Color Rules

**DO:**
- Use warm earth tones (browns, creams, tans) from the token system
- Use `--highlight` for active/speaking state highlights (like a real marker on paper)
- Use `--correction` for errors and corrections (like a teacher's red pen)
- Use `--pencil` / `--pencil-mark` for emphasis, confirmations, success states
- Use `--paper` instead of pure `white` for light backgrounds
- Keep `--eraser` for delete actions
- Use `--accent` (warm orange) for primary CTAs

**DON'T:**
- Use `sky-`, `blue-`, `green-`, `emerald-`, `fuchsia-`, `purple-`, `cyan-`, `teal-`, `lime-`, `indigo-`, `violet-` Tailwind colors
- Use `rose-` or `pink-` (use `--correction` or `--eraser` instead)
- Use pure `white` or `#ffffff` (use `--paper` or `--card`)
- Use pure `black` (use `--foreground` or `--pencil`)

---

## Typography

Three handwriting fonts, loaded via Google Fonts.

| Token | Font Family | Weight | Usage |
|-------|------------|--------|-------|
| `font-sketch` | Caveat | 400, 600, 700 | Headings, splash screen, emphasis |
| `font-hand` | Patrick Hand | 400 | Body text, default UI font |
| `font-architect` | Architects Daughter | 400 | Labels, secondary headings |

### Font Sizing (Container Query Units)

Message bubbles use container queries (`containerType: inline-size`) for responsive text:

| Size | `cqw` | Usage |
|------|-------|-------|
| Target language | `4cqw` | Primary translated text |
| User message | `3.8cqw` | User-authored text |
| Native language | `3.55cqw` | Translation/native text |
| Metadata | `2.8cqw` | Labels, timestamps, counts |

---

## Shape System

Every sketchy-bordered element should have a **unique organic shape**. No two adjacent elements should look alike.

### How It Works

1. **12 CSS shape variants** are defined as `.sketch-shape-0` through `.sketch-shape-11` in `index.css`
2. Each sets a `--sketch-radius` custom property with a unique asymmetric `border-radius`
3. `.sketchy-border` and `.sketchy-border-thin` read from `--sketch-radius` (with a fallback)
4. A JS utility (`src/shared/utils/sketchyShape.ts`) provides:
   - `sketchShapeClass(index)` - returns a class name like `sketch-shape-5` (deterministic, no adjacent collisions)
   - `sketchShapeStyle(seed)` - returns an inline `{ borderRadius: '...' }` style for **truly unique** per-element shapes

### Shape Variant Table

| Class | Border Radius | Visual Character |
|-------|--------------|------------------|
| `sketch-shape-0` | `255px 15px 225px 15px / 15px 225px 15px 255px` | Top-left & bottom-right rounded |
| `sketch-shape-1` | `15px 255px 15px 225px / 225px 15px 255px 15px` | Top-right & bottom-left rounded |
| `sketch-shape-2` | `185px 25px 235px 20px / 20px 210px 25px 185px` | Gentle asymmetry |
| `sketch-shape-3` | `25px 195px 15px 240px / 240px 20px 195px 15px` | Reversed gentle asymmetry |
| `sketch-shape-4` | `225px 20px 195px 25px / 25px 185px 20px 225px` | Wide top-left curve |
| `sketch-shape-5` | `20px 225px 25px 195px / 195px 25px 225px 20px` | Wide top-right curve |
| `sketch-shape-6` | `245px 18px 210px 22px / 22px 235px 18px 245px` | Tall, narrow feel |
| `sketch-shape-7` | `18px 245px 22px 210px / 210px 22px 245px 18px` | Reversed tall feel |
| `sketch-shape-8` | `200px 30px 240px 12px / 12px 200px 30px 240px` | Organic blob |
| `sketch-shape-9` | `30px 200px 12px 240px / 240px 12px 200px 30px` | Reversed blob |
| `sketch-shape-10` | `215px 22px 250px 18px / 18px 215px 22px 250px` | Stretched diagonal |
| `sketch-shape-11` | `22px 215px 18px 250px / 250px 18px 215px 22px` | Reversed stretched |

### Usage Patterns

**Chat message bubbles:** Use `sketchShapeStyle(messageIndex)` for truly unique per-message shapes.

```tsx
import { sketchShapeStyle } from '../shared/utils/sketchyShape';

const bubbleShape = useMemo(() => sketchShapeStyle(messageIndex), [messageIndex]);
// Apply: style={{ ...bubbleShape }}
```

**Static UI elements (buttons, cards, inputs):** Use fixed `sketch-shape-N` classes, choosing a different number for each element in the same view.

```html
<div class="sketchy-border sketch-shape-3">Card A</div>
<button class="sketchy-border-thin sketch-shape-7">Button</button>
<input class="sketchy-border-thin sketch-shape-1" />
```

**Circular elements (icon buttons, avatars):** Keep using `rounded-full`. These are intentionally perfect circles and don't need sketchy shapes.

### Shape Rules

**DO:**
- Give every message bubble a unique shape
- Use different `sketch-shape-N` numbers for adjacent UI elements
- Use `sketchShapeStyle()` for dynamically generated lists
- Use `sketchShapeClass()` if you only need a CSS class

**DON'T:**
- Use the same `sketch-shape-N` on two elements side by side
- Apply sketchy shapes to circular icon buttons (keep `rounded-full`)
- Use `rounded-sketchy` (the old single-shape Tailwind class) -- it's deprecated

---

## CSS Utility Classes

Defined in `src/app/index.css`.

### Borders

| Class | Description |
|-------|-------------|
| `.sketchy-border` | 2px pencil border + organic radius (reads `--sketch-radius`) |
| `.sketchy-border-thin` | 1.5px pencil-light border + organic radius |
| `.sketchy-underline` | Wavy SVG underline (pencil stroke) |

### Decorative

| Class | Description |
|-------|-------------|
| `.paper-texture` | Adds SVG noise overlay for paper grain |
| `.notebook-lines` | Horizontal ruled lines (like notebook paper) |
| `.tape-effect` | Scotch tape decoration at top center |
| `.torn-paper` | Jagged torn-edge clip-path on top and bottom |

### Scrollbar

Custom styled scrollbar matching the paper aesthetic:
- Track: `#ece4d4` (paper color)
- Thumb: `#8a7e72` (pencil-light tone)
- Thumb hover: `#6b5e50` (darker pencil)

---

## Animations

Defined in `index.html` (Tailwind config) and `src/app/index.css`.

### Tailwind Animations

| Class | Duration | Description |
|-------|----------|-------------|
| `animate-wobble` | 3s infinite | Gentle rotation wobble (notebook page flutter) |
| `animate-float` | 4s infinite | Floating + subtle rotation |
| `animate-pencil-scribble` | 1.5s | Width grows 0% to 100% (writing effect) |
| `animate-fade-up` | 0.5s | Fade in + slide up (message entrance) |
| `animate-sketch-in` | 0.4s | Scale + rotate entrance (element appearing) |

### CSS Animations

| Class | Duration | Description |
|-------|----------|-------------|
| `.animate-voice-swap` | 180ms | Scale bounce for voice swap feedback |
| `.animate-voice-ripple` | 450ms | Expanding ripple for voice activity |
| `.animate-flag-wave` | 3s infinite | 3D flag waving for language indicators |

### Clip-Path

| Class | Description |
|-------|-------------|
| `.moon-left` | Left-half circle clip (avatar overlays) |
| `.moon-right` | Right-half circle clip (avatar overlays) |

---

## Component Styling Guide

### Message Bubbles

- **User messages**: `bg-primary bg-opacity-90 text-primary-foreground` + unique `sketchShapeStyle(index)`
- **Assistant messages**: `bg-card bg-opacity-90 text-foreground sketchy-border-thin` + unique shape
- **Error messages**: `bg-destructive/10 text-destructive` + unique shape
- **Status messages**: `bg-accent/10 text-accent` + unique shape
- **Speaking highlight**: `bg-highlight text-highlight-text` (warm marker yellow)

### Buttons

- **Primary action**: `bg-accent text-accent-foreground hover:bg-accent` + `sketch-shape-N`
- **Secondary**: `bg-secondary text-foreground` + `sketchy-border-thin` + `sketch-shape-N`
- **Destructive**: `bg-eraser text-accent-foreground` + `sketchy-border-thin`
- **Confirm/Success**: `bg-pencil text-paper hover:bg-pencil-mark` (not green)
- **Invalid/Error**: `bg-correction text-paper` (not rose/red Tailwind)
- **Icon buttons**: `rounded-full` (no sketchy shape)

### Inputs

- Container: `sketchy-border` + `sketch-shape-N`
- Fields: `sketchy-border-thin` + `sketch-shape-N`
- Focus ring: `ring-pencil-light` (not blue or green)

### Error States

- Icon color: `text-correction`
- Text: `text-correction` (light mode) or `text-paper/80` (on dark overlay)
- Background: `bg-correction/30` (subtle wash)

### Warning States

- Text: `text-paper/80`
- Background: `bg-pencil-mark/30`

### Status Indicators

- Active/Listening: `bg-pencil` + `border-pencil-mark`
- Hold/Paused: `bg-pencil-light` + `border-pencil-mark`
- Observing: `bg-accent` + `border-accent`

---

## Responsive Breakpoints

Uses Tailwind's default breakpoints:

| Prefix | Min Width | Usage |
|--------|-----------|-------|
| `sm:` | 640px | Wider message bubbles |
| `md:` | 768px | Medium layout adjustments |
| `lg:` | 1024px | Desktop-width messages |

Message bubble max-widths:
- Mobile: `max-w-[90%]`
- `sm:`: `max-w-[80%]`
- `md:`: `max-w-[70%]`
- `lg:`: `max-w-[65%]`

---

## File Reference

| File | Purpose |
|------|---------|
| `src/app/index.css` | CSS variables, shape variants, decorative classes, animations |
| `index.html` | Tailwind config (colors, fonts, keyframes, animations) |
| `src/shared/utils/sketchyShape.ts` | Shape generation utility (`sketchShapeClass`, `sketchShapeStyle`) |
| `src/features/chat/components/ChatMessageBubble.tsx` | Message bubble rendering |
| `src/features/chat/components/ChatInterface.tsx` | Message list and bookmark UI |
| `src/features/chat/components/InputArea.tsx` | Composer and input controls |
| `src/features/chat/components/BookmarkActions.tsx` | Bookmark UI elements |
| `src/features/session/components/ApiKeyGate.tsx` | API key entry screen |
| `src/features/session/components/Header.tsx` | Top header bar |
| `src/features/session/components/CollapsedMaestroStatus.tsx` | Status indicators |
