# Maestro Language Tutor (Gemini Edition)

A fully client-side React application for language learning powered by the Google Gemini API. This application features real-time voice conversation (Gemini Live), image-based context awareness, and personalized tutoring without requiring a dedicated backend server.

![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)

## Features

- **Gemini Live Conversation**: Real-time, low-latency voice interaction with the AI tutor.
- **Multimodal Context**: Send images or live video feeds to the AI for context-aware language practice.
- **Image Generation**: The AI can generate images to illustrate concepts or continue a visual story.
- **Personalized Tutoring**: Maintains a local profile of your learning progress and preferences.
- **Privacy Focused**: Chat history and settings are stored locally in your browser (IndexedDB).
- **Client-Side Only**: Runs entirely in the browser; communicates directly with Google Gemini API.

## Prerequisites

- **Node.js** (v18 or higher recommended)
- **Google Gemini API Key**: You need a valid API key from [Google AI Studio](https://aistudio.google.com/).

## Installation

1.  **Clone the repository:**

    ```bash
    git clone https://github.com/your-username/gemini-language-tutor.git
    cd gemini-language-tutor
    ```

2.  **Install dependencies:**

    ```bash
    npm install
    ```

3.  **Environment Setup:**

    Create a `.env` file in the root directory and add your Google Gemini API key:

    ```env
    API_KEY=your_google_gemini_api_key_here
    ```

    *Note: The application expects the key to be available via `process.env.API_KEY` during the build or runtime injection.*

## Running the Application

To start the development server:

```bash
npm run dev
```

Open your browser and navigate to the URL shown (usually `http://localhost:5173`).

## Building for Production

To build the application for production deployment:

```bash
npm run build
```

The output will be in the `dist` directory.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## Copyright

Copyright 2025 Roni Tervo
