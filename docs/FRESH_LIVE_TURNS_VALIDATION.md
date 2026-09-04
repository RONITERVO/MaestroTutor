# Fresh Live turns — 2026-09-04

Branch: `codex/fresh-live-turns`.

## Behavior

- Conversation and observer transports own one response. The user camera Live
  mode stays selected and locally re-arms between responses. The next connection
  waits for confirmed speech and rebuilds the current system instruction, profile,
  bookmark context and chat history. Observer resumption handles are not reused.
- Provider `turnComplete` starts settlement, rather than immediately committing
  transcripts or discarding buffers. Settlement requires 1.5 seconds of callback
  quiescence, decoded audio and speaker drain. New callbacks invalidate a pending
  drain decision. Transport shutdown still drains already received audio.
- The managed gateway accepts one input turn, keeps accepting late provider
  output/accounting, and retains its existing maximum 120-second window. The
  reservation budgets one turn; settlement continues to charge observed usage.
- Camera Off and generated-image selection prevent physical snapshots and Live
  frames, including delayed frame encoding. Device enumeration no longer briefly
  opens the camera while Off. An automatic Live snapshot cannot override consent.
- Raw and derived avatar uploads are scoped to account identity and API key using
  a digest. Scope changes invalidate both caches. Stale in-flight uploads cannot
  populate the new scope, and concurrent refreshes share an upload operation.
- Headless protocol 1.8 rejects multiple connected turns. Repeated chat-turn
  commands on one profile rebuild saved context and report its count/digest.

## Real provider checks

All three checks used the checked-in generated speech fixture at real input pace
and a headless PCM playback sink at real speaker pace. Input duration was 22.04 s.
All passed the long-response verifier and recovered 47 of 48 expected words.

| Access and flow | Camera frames | Generated speech | Result |
| --- | ---: | ---: | --- |
| BYOK conversation | 0 | 65.72 s | Complete transcript and playback |
| BYOK observer, same saved chat, fresh connection | 0 | 37.32 s | Complete transcript, handoff and playback |
| Managed staging conversation | 1 | 93.16 s | Complete transcript, playback and billing |

The managed playback finished 76.696 seconds after the last audio bytes arrived.
Its actual speaker timeline was 108.176 seconds. Billing evidence matched exactly
one usage row and one charge row: 34 credits / USD 0.033715, zero shortfall and
zero reservation remaining. The deployed staging backend was used for this
provider test; the new gateway admission rule is covered by local gateway tests.

## Regression coverage

- 120 seconds of pending speaker output with a late callback after 119 seconds.
- Late transcripts/audio after `turnComplete`, including correct last-byte timing.
- Separate headless connections whose second prompt contains the first reply.
- Rejection of second input turns before provider/billing work; preservation of
  late provider audio and usage metadata.
- Camera Off with a leftover preview element: no `getUserMedia` and no snapshot.
- Raw/derived avatar refresh across account, key and access-mode changes, stale
  upload completion and simultaneous refresh callers.

The root test suite, gateway tests, Functions unit tests, lint, application build,
gateway build and release-configuration verification were run. Local detailed
outputs are in the ignored `.maestro-debug` directory.

## Limits and rollout

This is not a claim of complete UI/headless parity. Headless supplies synthetic
PCM after device capture; it does not exercise a real microphone, browser Whisper
worker, Android speaker hardware, or camera permission dialogs. The camera hook
and avatar ownership behavior have deterministic regression coverage. The full
120-second playback case uses a controlled clock; the longest real model output
in this run was 93.16 seconds.

The 1.5-second quiet interval is an application settlement heuristic, not a Gemini
guarantee about arbitrarily delayed callbacks. Gemini documents generation
completion separately from transport/session lifetime in its
[session management guide](https://ai.google.dev/gemini-api/docs/live-api/session-management).

No deployment was performed. Ship the frontend, Functions reservation policy and
gateway together; existing clients that try a second turn on one connection are
incompatible with the new gateway limit.
