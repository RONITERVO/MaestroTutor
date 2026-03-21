<div align="center">

# ✈️ Maestro Language Tutor

**The AI tutor that hears you, sees your world, and thinks in two languages at once.**

[![Live Demo](https://img.shields.io/badge/Try%20It%20Live-4285F4?style=for-the-badge&logo=googlechrome&logoColor=white)](https://chatwithmaestro.com)
&nbsp;
[![Android](https://img.shields.io/badge/Android%20App-3DDC84?style=for-the-badge&logo=android&logoColor=white)](#getting-started-developer)
&nbsp;
[![Gemini API](https://img.shields.io/badge/Gemini%20API-8E75B2?style=for-the-badge&logo=google&logoColor=white)](https://ai.google.dev/)

[![React](https://img.shields.io/badge/React%2019-61DAFB?style=flat-square&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript%205-3178C6?style=flat-square&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Vite](https://img.shields.io/badge/Vite%207-646CFF?style=flat-square&logo=vite&logoColor=white)](https://vitejs.dev/)
[![Tailwind](https://img.shields.io/badge/Tailwind%203-38B2AC?style=flat-square&logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)
[![Version](https://img.shields.io/badge/version-1.2.2-informational?style=flat-square)](./package.json)
[![License](https://img.shields.io/badge/Apache%202.0-blue?style=flat-square)](./LICENSE)

</div>

---

## A session in 30 seconds

> You open Maestro. A **3D globe** rotates slowly in front of you — the entire world, lit by your local time of day. Country flags drift across its surface, distributed from pole to pole. You spin it with your finger, let it coast. You find 🇯🇵 on the right wheel. Japanese. The left wheel stays on 🇬🇧 English. A dashed arc draws itself between the two flags, and a tiny ✈️ plane begins to trace the route. You tap the plane.
>
> The globe dissolves. A **notebook** opens. Your tutor is ready.
>
> You type a sentence in English. Maestro replies in Japanese — then below it, in English — each phrase highlighted in soft yellow, each translation in light blue. You tap a line. It **speaks**. You hold the mic and reply. Maestro listens. You stumble. You look at the **Suggestions**  — three Japanese phrases appear, each one exactly right for this moment. You tap one and it expands: the English meaning slides open. It speaks out loud. You speak, and the conversation moves on.
>
> You flip on the camera. You hold up your coffee cup. You don't know the word. You draw a circle on the photo. Maestro names it, explains it, uses it in a sentence, and **generates a visual or auditory aid—such as a game, an image, or even music** of a coffee shop to anchor the memory. You tap the red button. Now you're in a **live duplex call** — no turns, no long waiting — you speak, Maestro speaks, you interrupt, it pauses. Natural. You hold your phone up. It sees your room and weaves it into the lesson. 

You see how the conversation builds on paper — with text, audio, and extra elements to aid teaching with images, minigames, charts, music in the same chat message you see on the chat page paper.
>
> This is Maestro.

---

## The Globe: choosing your languages

When you first arrive, a full-screen interactive globe greets you. This is your session selector — and it sets the tone for everything that follows.

| What you see | What it does |
|---|---|
| 🌍 **3D globe** | Rotatable by drag or scroll. Inertia carries it after you let go. Lit by real local time — day on one side, night on the other. |
| 🏳️ **Language flags** | Every supported language is a flag, distributed evenly across the globe. Click one to select it. |
| 🛞 **Left scroll wheel** | Your **native language** — the one you already know. Snaps to center; the globe rotates to show that flag. Highlighted in **cyan**. |
| 🛞 **Right scroll wheel** | Your **target language** — the one you're learning. Highlighted in **green**. |
| ✈️ **Flight path** | Once both languages are chosen, a dashed arc connects them with an animated plane flying the route. Once the plane arrives the lesson can start automatically. |
| 🧑‍🎨 **Avatar** | Choose or upload the Maestro persona that will accompany your sessions. |

> **Tip:** Tap the **Maestro status flag** in the top-left corner to open/dismiss the globe at any time during a session.

---

## The Notebook: your conversation space

Every conversation lives in a paper-textured notebook. Messages are rendered as physical note cards — organic, hand-sketched borders, translucent tape strips at the corners, layered depth shadows. Each turn in the conversation is a document you can revisit enhanced with audio, text, images, games, music, charts and more.

### What a message looks like (when text only style without enhancing content)

```
┌─────────────────────────────────────────────────────────────┐  ← tape strip
│                                                              │
│  You:  "How do I say 'the coffee is cold'?"                 │
│                                                              │
└──────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐  ← tape strip
│                                                             │
│  ████████████████████████████████                           │  ← target text (yellow highlight)
│  コーヒーが冷めています。                                     │
│                                                             │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░                       │  ← native text (cyan highlight)
│  "The coffee has gone cold."                                │
│                                                             │
└──────────────────────────────────────────────────────────────┘
```

- **Yellow highlight** = target language (what you're learning)
- **Cyan highlight** = native language (your translation)
- **Tap any line** to hear it spoken aloud in the correct language

### Rich attachments — everything in context

Maestro doesn't just remember text. Every message preserves a full picture of the moment it happened:

| Attachment type | What's preserved |
|---|---|
| 🎤 **Voice recording** | Your or Maestro's raw audio, stored with the message — playable in-line |
| 📸 **Camera snapshot** | A photo taken at the moment you spoke or by you using the camera manually, stored with the turn |
| 🖼️ **Images** | Generated images bring the text to life with visual content tuned to the context. Some look hand drawn, others realistic.|
| 📄 **PDFs** | Inline viewer with annotation support — draw directly on the page, same as draw in images. |
| 🎮 **SVG mini-games** | Maestro can choose to create interactive language games for any reply, rendered in the same chat bubble as the response.|
| 💻 **Code / files** | Paste a large code snippet or select from device storage and it auto-converts to a file attachment |
| 🎵 **Music** | Maestro can choose to set the mood with music. |

### Swipe actions

- **Swipe left** on your message → delete it
- **Swipe right** on Maestro's message → delete or **bookmark** it
- **Bookmark** a message to hide everything above it — like a new chapter. Scroll back when you need history.

---

## Gemini Live: the full-duplex call

The **red button** on the camera view starts a live, bidirectional audio + video session. Exactly the same Maestro features as normal chat, but No turns. No waiting. Just conversation. Every verbal exchange saved normally in chat, with possible teaching aid in form of image, game, chart or music. The only drawback is that you can't send files yourself, other than your camera feed and voice that are captured automatically as you speak.

```
          ┌──────────────────────┐
          │   ● LIVE  00:34      │
          │                      │
          │   [camera preview]   │
          │                      │
          │     ■ stop           │
          └──────────────────────┘
```

**During a Live session:**
- Your voice is streamed continuously via a 16 kHz audio worklet — Maestro hears you in real time
- Your **camera feed** is simultaneously streamed — Maestro sees your environment
- You can interrupt at any point; Maestro will stop mid-sentence and adjust
- **Zero latency** — the model processes as you speak

**After each turn:**
- Your audio is saved as a WAV recording and attached to the chat in the same message
- The camera captures a photo of you at the moment you finished speaking
- Maestro's audio response is time-aligned to its text, so playback highlights each phrase as it's spoken
- Reply **suggestions** are generated automatically for the next turn

**Silent observer** _(background)_: A separate, invisible Gemini Live session quietly monitors your surroundings when you're idle. If you've been away from the conversation, Maestro may glance at the camera and re-engage you with something relevant to where you are.

---

## Suggestions: never get stuck

Suggested replies appear below the input after every message. They're generated in the target language, tailored to the exact context of your conversation.

```
                            ┌────────────────────────────┐
                            │  はい、ありがとうございます  │
                            └────────────────────────────┘
                            ┌───────────────────────────┐
                            │  もう一度言ってください   │
                            └───────────────────────────┘
                            ┌────────────────────┐
                            │  よくわかりません  │
                            └────────────────────┘
```

- **Tap once** → the English translation slides open below and the phrase is spoken aloud

### Quick Translate

Get an instant translation for any sentence you have in mind — without adding it as a turn in the conversation history.

Tap the **translate icon** beside the input to enter Suggestion Mode:

1. Tap mic and the mic activates and listens in your **native language** or just type.
2. Speak naturally — describe what you want to say
3. Maestro generates the target-language version as a custom suggestion
4. Tap it to speak, speak or type, send, and continue chatting.

---

## Vision: let Maestro see

Toggle the **camera icon** in the input bar to open a live camera preview. Once enabled:

- Every message you send is automatically accompanied by a **snapshot** of what your camera sees
- Maestro uses visual context to make responses more relevant to your environment

**Drawing mode** — don't know the word? Don't use words:
1. Capture a photo or freeze a video frame or just tap to open an empty canvas.
2. Draw on it — circle an object, underline text, mark something of interest
3. Maestro interprets the annotation and explains, names, and teaches around it

**Teaching aid generation:**
Maestro can create images, games, charts and music on its own to accompany explanations. Ask about a word and it might reply with a generated scene.

---

## Voice characters

Five voice personalities — each with a unique color identity displayed as a ring around the avatar cluster in session controls:

| Voice | Color | Feel |
|---|---|---|
| **Zephyr** | 🩵 Teal | Airy, smooth |
| **Puck** | 💛 Gold | Warm, expressive |
| **Charon** | 🩶 Silver | Calm, measured |
| **Kore** | 💙 Blue | Clear, precise |
| **Fenrir** | ❤️ Red | Bold, energetic |

Cycle through voices using the wing button on the right of the avatar cluster. Each voice has a different character — try a few and pick the one that keeps you engaged.

**Speaking behavior:**
- Target-language lines are spoken first
- Native-language lines can optionally follow, tap any native line to disable speaking on all of them.
- All generated speech is cached — replaying a phrase costs no API call
- Tap any individual line in a message to re-speak just that line

---

## Personalize: colors, theme, and your avatar

### 🎨 Theme Customizer

Tap the palette icon in session controls to open the **Paint Colors** panel. Every visual element in the app is wired to an independent color, all live-editable:

- **Page canvas** — background, paper texture, notebook lines
- **Message bubbles** — backgrounds, text, tape strips, shadow depth
- **Input area** — chat mode vs. suggestion mode, icon states
- **Translation highlights** — yellow (target) and cyan (native) marker colors
- **Live session** — badge, stop button, recording indicators
- **Voice identities** — each voice's ring color
- ...and much more

Changes apply instantly. Save named presets. Export as `.json` to share. Import from a file. Two built-ins: **Notebook** (defaults) and **Original** (explicit baseline).

### 🧑‍🎨 Your Maestro

Upload any image as Maestro's avatar. It's more than cosmetic — the avatar is sent to the AI as part of the session context, so Maestro "sees itself" and can present consistently as that persona. Swap back to a default avatar from the built-in manifest at any time.

### 👤 Your profile

You can always tap the pencil icon in session controls to add or modify your name, background, or learning goals. — You don't have to write any of it yourself. Maestro will personalize this profile as you speak to get to know who you are.

---

## Privacy and your API key

Maestro operates on a **Bring Your Own Key** model.

- You supply your own **Google Gemini API key** from [Google AI Studio](https://aistudio.google.com/)
- The key is stored locally on your device using secure storage — never sent to any backend
- All AI calls go directly from your device to the Gemini API
- No account, no server, no telemetry

The API key button in the top-right corner shows green when a key is present, red when missing. Tap it anytime to update your key or view setup instructions.

<details>
<summary>Getting a Gemini API key (step-by-step)</summary>

1. Go to [Google AI Studio](https://aistudio.google.com/)
2. Sign in with a Google account
3. Click **Get API key** → **Create API key**
4. Copy the key
5. Paste it into the Maestro key prompt on first launch

The free tier is generous for personal learning. If you hit image generation limits, the in-app CTA will guide you to enable billing, or you can switch to a camera + Live session instead.

Step-by-step screenshots are available in [`/docs/`](./docs/).

</details>

---

## Getting started (developer)

### Prerequisites

- **Node.js** v18 or higher
- A **Google Gemini API key** — [get one free from AI Studio](https://aistudio.google.com/)

### Web (development)

```bash
git clone https://github.com/RONITERVO/MaestroTutor.git
cd MaestroTutor
npm install
npm run dev
```

Open `http://localhost:5173`. On first launch, paste your Gemini API key. The key is stored locally only.

> **Dev shortcut:** create a `.env` file in the project root to skip the key prompt during development:
> ```env
> VITE_API_KEY=your_api_key_here
> ```
> Do **not** ship builds with this key present — production and Android builds use the in-app BYOK prompt.

### Web (deploy to GitHub Pages)

```bash
npm run deploy
```

### Android

```bash
npm run cap:android    # build + sync Capacitor
npx cap open android   # open in Android Studio
```

Requires Android Studio with a connected device or emulator.

---

## Contributing

Contributions are welcome. The codebase follows a strict feature-slice architecture — all logic lives in `src/features/`, the app shell is a pure composition root with no business logic.

**Before you start:**

- Read [`docs/DESIGN_SYSTEM.md`](./docs/DESIGN_SYSTEM.md) — every color in the UI is a named CSS token. New elements require new tokens, not hardcoded values.
- Check [`docs/DEV_CHEATSHEET.md`](./docs/DEV_CHEATSHEET.md) for patterns around state management, ref synchronization, and audio lifecycle.

**Key architectural notes:**

- **Smart refs** — state from Zustand is synced into refs inside async callbacks to prevent closure staleness
- **Activity tokens** — a string-set busy system prevents speech, re-engagement, and live sessions from colliding
- **Dual-resolution images** — every image is stored twice: full-res for the Gemini Files API, compressed for IndexedDB persistence
- **TTS cache** — all synthesized speech is cached by `(text, language, provider, voice)` — replaying never costs an API call
- **Test on mobile** — AudioContext, MediaStream, and speech recognition behave differently on Safari iOS vs Chrome Android vs desktop

**Workflow:**

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -m 'feat: short description'`
4. Push and open a Pull Request

---

## License

Distributed under the [Apache 2.0 License](./LICENSE).

---

<div align="center">

Built with care by **Roni Tervo**

*Powered by the Gemini API*

</div>
