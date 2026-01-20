# 📚 Maestro: The Gemini Language Tutor

[![React](https://img.shields.io/badge/React-19.2.3-61DAFB?logo=react&style=flat-square)](https://react.dev/)
[![Vite](https://img.shields.io/badge/Vite-7.3.1-646CFF?logo=vite&style=flat-square)](https://vitejs.dev/)
[![Gemini API](https://img.shields.io/badge/Powered%20by-Google%20Gemini-8E75B2?logo=google&style=flat-square)](https://ai.google.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.2-3178C6?logo=typescript&style=flat-square)](https://www.typescriptlang.org/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-3.4-38B2AC?logo=tailwindcss&style=flat-square)](https://tailwindcss.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue?style=flat-square)](./LICENSE)

**Maestro** is a next-generation, client-side language learning interface powered by Google's **Gemini**.

Unlike traditional flashcard apps, Maestro listens, watches, and remembers. It creates a seamless, real-time conversational environment where you can practice speaking a new language with an AI tutor that understands context, tone, and visual cues from your environment. Maestro does not only understand your environment and reply with **audio** and **text**, but can also reply with **image** representation of any context within the conversation.

---

## ✨ The Experience

Maestro is designed to feel less like a chatbot and more like a human tutor sitting across the table, configurable as you desire. Fluent in many languages.

### 🗣️ Real-Time Voice Conversation
Maestro speech (TTS) and hearing (STT) powered by **Gemini (Live)**, Low-latency.
*   **Hybrid STT/TTS:** Gemini's server-side audio processing for the best balance of latency and accuracy. 
*   **Persona Engine:** Maestro is programmed to be patient, encouraging, and uses audio tags (laughter, sighs, thoughtful pauses) to sound human.

### 👁️ Visual Context & Visual reply & "Smart Re-engagement"
Maestro isn't blind. It uses your camera to understand your world. Maestro chat reply can paint a picture (image) of how maestro sees your world.
*   **Active Observation:** If you go silent, Maestro analyzes your camera feed to find a conversation starter (Example message from maestro "*I see you're drinking coffee, how do you say 'mug' in Spanish?*" [Image of your table or imaginary scene with text overlay pointing at your cup in the table]). Maestro has visual persona in the image responses (eg. Avatar of your choise, or ask Maestro to describe one or give specific description of desired persona).
*   **Image Annotation:** Snap a photo or select one in chat, or freeze a video frame, draw on it, and ask specific questions about objects in the scene.

### 🧠 Deep Memory (Global Profile)
Maestro remembers who you are.
*   **Session Continuity:** Using IndexedDB, chat history is persisted locally.
*   **Learner Profiling:** Maestro maintains a background "Global Profile" that evolves as you chat, tracking your interests, proficiency level, and correction preferences across different sessions (languages).

---

## 🚀 Getting Started

### Prerequisites
*   Node.js (v18+)
*   A Google Cloud Project with the **Gemini API** enabled.
*   An API Key from [Google AI Studio](https://aistudio.google.com/).

### Installation

1.  **Clone the repository**
    ```bash
    git clone https://github.com/RONITERVO/MaestroTutor-GeminiEdition.git
    cd MaestroTutor-GeminiEdition
    ```

2.  **Install dependencies**
    ```bash
    npm install
    ```

3.  **Configure Environment**
    Create a `.env` file in the root directory:
    ```env
    # Required: Your Google Gemini API Key
    VITE_API_KEY=your_api_key_here
    ```

4.  **Run Development Server**
    ```bash
    npm run dev
    ```
    Access the app at `http://localhost:5173`.

---

## 🎮 Usage Guide

### 1. The Globe Selector
Upon launch, you are greeted by an interactive 3D globe.
*   **Optional: Avatar:** Bring Maestro alive visually (avatar, image)
*   **Left Wheel:** Select your native language (e.g., English).
*   **Right Wheel:** Select the target language (e.g., Spanish).
*   **Click the Plane or wait a second:** Launches the session.
*   **Any settings are located here** Come back by clicking the Maestro status flag.

### 2. Modes of Interaction
*   **Chat:** Type or use the microphone, and choose between using your camera or imaginary visual world (both sides image gen). Optionally attach any files, or edit the ones in chat. The AI receives your drawing layered over the image as context.
*   **Gemini Live (Red Button over camera view):** Additional layer on top of the chat experience. Activates the full-duplex audio stream and live camera feed. Speak naturally. You can interrupt Maestro while he speaks.
*   **Suggestion Mode:** If you get stuck there is always suggestions tailored for you specifically in your current chat context. If you are not happy with the suggestions just ask for new one in either one of the languages.
*   **Full context chat messages**: The chat messages always store at least the voice, the transcript and the media (usually image), so that you can review the full context behind previous conversations.

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1.  Fork the project.
2.  Create a feature branch (`git checkout -b feature/AmazingFeature`).
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4.  Push to the branch (`git push origin feature/AmazingFeature`).
5.  Open a Pull Request.

**Note:** Specifically when working on audio hooks (`hooks/speech, but also good practice in general`), please ensure you **test on both Desktop and Mobile browsers**, as AudioContext and many other behavior varies across devices.

---

## 📄 License

Distributed under the Apache 2.0 License. See `LICENSE` for more information.

---

<div align="center">
  <sub>Built with ❤️ by Roni Tervo</sub>
  <br />
  <sub>Powered by the Gemini API</sub>
</div>