# STT ink, Gemini 3.8 and minimum music fee

The STT composer hides local Whisper words. After speech confirmation it shows
three ink marks, adds one per second of newly captured speech (up to 24), and
replaces the marks when Gemini input or corrected output transcription arrives.
Capture callbacks drive updates; buffered replay cannot count the audio twice.
Stopping, completion, errors and cancellation clear the preview. Marks never
enter the transcript, message text or recorded-utterance transcript.

Text generation and auxiliary requests use gemini-3.8-flash. The managed backend
maps installed clients requesting gemini-3.7-flash to 3.8 before allowlisting and
pricing. The registry retains 3.7 for historical provider usage records.
Google lists the same promotional rates: $0.75 input and $3.75 output per million
tokens through 2026-12-31. Recheck the scheduled 2027 increase before that date.
Source: https://ai.google.dev/gemini-api/docs/pricing

Managed Lyria RealTime uses a 1-credit service fee, reduced from 120 credits.
This is the smallest positive fee in the current credit system. It is not a
claim that Google publishes a Lyria RealTime price; provider pricing remains
explicitly unpriced. The environment setting MANAGED_MUSIC_SESSION_CREDITS
controls the fee. Production and staging must both deploy the value 1.
Existing authentication, rate limits, concurrent-session limits, bounded audio,
reservation release on failure and settlement rules still apply.

Validation includes an STT hook regression for hidden words, captured-second
progress and cancellation; the full-app fake-microphone replay in STT mode;
model registry equality and legacy-client mapping tests; a real Gemini 3.8
provider request; and a managed music request with ledger verification.

Run the STT UI replay with MAESTRO_UI_REPLAY_MODE=stt and the same staging-only
credentials and recorded WAV required by scripts/run-live-ui-replay.mjs.
