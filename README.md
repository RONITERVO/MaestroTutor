# 📚 Maestro: The Gemini Language Tutor

[![React](https://img.shields.io/badge/React-19.2.3-61DAFB?logo=react&style=flat-square)](https://react.dev/)
[![Vite](https://img.shields.io/badge/Vite-7.3.1-646CFF?logo=vite&style=flat-square)](https://vitejs.dev/)
[![Gemini API](https://img.shields.io/badge/Powered%20by-Google%20Gemini-8E75B2?logo=google&style=flat-square)](https://ai.google.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.2-3178C6?logo=typescript&style=flat-square)](https://www.typescriptlang.org/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-3.4-38B2AC?logo=tailwindcss&style=flat-square)](https://tailwindcss.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue?style=flat-square)](./LICENSE)

**Maestro** is a personal AI language tutor designed for full two-way communication. It integrates real-time dual-language **voice**, **vision**, and **text** so that you can communicate however best suits you.

Unlike traditional apps, Maestro allows you to communicate using any combination of inputs—simultaneously.

---

## ✨ Features

### 🎨 Complete Multimodal Freedom
Communication is not restricted to a single format. You have full control over how you interact:
*   **Flexible Input and Output:** Two-way real-time communication with audio, text, images, PDF files, video, or all of them at the same time.
*   **Nonverbal Communication:** If you don't know the words, simply draw on an image or freeze a video frame to highlight an object. Maestro understands these visual annotations and guides you without requiring any spoken communication.

### ⚡ Real-Time Comprehension
Maestro ensures you understand the context through multiple sensory channels:
*   **Visual Cues:** Maestro explains concepts visually, generating images in real-time to help you understand vocabulary or scenarios instantly.
*   **Dual Subtitles:** Chat interactions are accompanied by real-time subtitles in both your native and target languages, so you can always follow the conversation.
*   **Full Context Persistence:** The chat preserves everything in a single message: voice recordings, generated or captured visuals, user drawings, and subtitles per voice, subject, and language. You are not just reading old text; you are re-experiencing the full conversation.

### 🧠 Adaptive Assistance
*   **Smart Suggestions:** Never get stuck. Maestro provides tailored response suggestions based on the current visual and conversational context in both text and audio format.
*   **Quick Translation:** Get instant translations for any sentence you have in mind without adding it as a turn in the chat history.
*   **Zero Latency:** Interactions happen in real-time. You can speak naturally, interrupt, and converse without awkward delays.

### 🔒 Privacy & Control
Maestro operates on a **"Bring Your Own Key"** model. You use your own Google Gemini API key, ensuring your data usage and privacy are under your control.

---

## 🚀 Getting Started

### Prerequisites
*   Node.js (v18+)
*   A Google Cloud Project with the **Gemini API** enabled.
*   An API Key from [Google AI Studio](https://aistudio.google.com/).

### Installation

1.  **Clone the repository**
    ```bash
    git clone https://github.com/RONITERVO/MaestroTutor.git
    cd MaestroTutor
    ```

2.  **Install dependencies**
    ```bash
    npm install
    ```

3.  **Configure Environment (Optional for local dev)**
    You can run with a dev-only API key using `.env`:
    ```env
    # Optional: Dev-only key (do NOT ship this in production builds)
    VITE_API_KEY=your_api_key_here
    ```
    In production and Play Store builds, users will enter their own API key
    inside the app on first launch (BYOK model).

4.  **Run Development Server**
    ```bash
    npm run dev
    ```
    Access the app at `http://localhost:5173`.

---

## 🎮 Usage Guide

### API Key Setup
On first launch, Maestro will prompt you to paste your personal Gemini API key. The key is stored locally on your device and never sent to any backend.

### 1. The Globe Selector
Upon launch, you are greeted by an interactive 3D globe.
*   **Left Wheel:** Select your native language (e.g., English).
*   **Right Wheel:** Select the target language (e.g., Spanish).
*   **Launch:** Click the Plane to start your session.
*   **Avatar:** Customize Maestro's visual persona.

### 2. Interaction Modes
*   **Chat:** Type or use the microphone. Toggle the camera to let Maestro see your world. Attach files or draw on images for context.
*   **Gemini Live:** Activate the red button for a full-duplex audio stream and live camera feed. Speak naturally and interrupt at any time.
*   **Suggestion Mode:** Stuck? Tap for suggestions tailored to your current conversation.
*   **Review:** Scroll back to see full context—audio, transcripts, and images are all preserved.

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1.  Fork the project.
2.  Create a feature branch (`git checkout -b feature/AmazingFeature`).
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4.  Push to the branch (`git push origin feature/AmazingFeature`).
5.  Open a Pull Request.

**Note:** When working on audio hooks, please ensure you **test on both Desktop and Mobile browsers**, as AudioContext behavior varies across devices.

**Risk Assessment & State Management**
*   **Ref Synchronization:** We use a pattern of syncing Store state to local refs to prevent closure staleness in async callbacks.
*   **Transient State:** MediaStream objects are stored in `hardwareSlice` and are excluded from persistence to prevent crashes.

---

## 📄 License

Distributed under the Apache 2.0 License. See `LICENSE` for more information.

---

<div align="center">
  <sub>Built with ❤️ by Roni Tervo</sub>
  <br />
  <sub>Powered by the Gemini API</sub>
</div>
