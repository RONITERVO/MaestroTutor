# Shared world audio

Users and the in-app agent must be able to define audio for objects and behaviours,
including imported or generated clips, procedural sounds, live sources and
event-driven playback.
Maestro's mouth is one emitter in that system. The familiar chat continues to own
providers, account access, generated media and Live conversation.

This is the accepted implementation direction. General audio assets, emitters,
program capabilities and stream adapters are not yet exposed in the app. Current
native playback handles Maestro's mono speech; artifact music still plays in the
web app. Room reflection output is under development and is not installed by
`MaestroRoom`.

## One model for user and agent

| Part | Responsibility |
| --- | --- |
| Audio source | A stable reference to a clip, generated media, procedural definition, recording or live connection. Decoding and bounded streaming belong to adapters. |
| Emitter | Where and how the source sounds: object or avatar joint attachment, local offset, spatial or nonspatial mode, gain, distance, looping, room effects and semantic role. |
| Playback instance | A transient, owned run with status, cursor, cancellation and completion receipts. Several instances may use one source. |
| Program | Existing events, conditions and actions start or change playback. Buttons, collisions, timers, avatar state and agent requests use the same capability implementations. |

Use the existing catalog to expose inspectable schemas, requirements, resource
channels and results to the visual editor, agent and headless tests. Do not add a
special tool for each kind of sound or let generated scripts create Unity
AudioSources outside this ownership model. Capability names and storage schemas
must be introduced with executable implementations and matching catalog tests.

An imported bark can be attached to a dog and triggered by a button or proximity
event. A radio can play a live stream. A collision rule can scale an impact sound
by collision strength. An animation event can trigger a footstep. The same source
can be reused by several objects without copying the media.

Simple robot beeps and instrument tones can use bounded native sound recipes,
such as oscillators and envelopes, without a separate generation service. Those
recipes need versioned schemas and execution budgets like object recipes; they
must not run arbitrary downloaded audio code.

An emitter references a target and optional joint using stable identities, rather
than retaining a Transform across avatar changes. Missing targets or unavailable
streams produce visible results. Saved work preserves definitions and stable media
references, not active network connections, credentials, raw Live PCM or runtime
handles. Restoration must not silently resume a microphone, live connection or
unfinished playback command.

## Playback ownership and expansion

Give play, pause, resume, stop and parameter changes the same lifecycle rules as
other capabilities. Completion must distinguish starting a stream from reaching
its end: a looping emitter cannot block an entire program waiting for an EOF that
will never come. Return an instance handle and expose started, buffering,
completed, cancelled and failed events. Fence late callbacks by workspace, source
and playback generation.

Stop affects the owned instance. Deleting an object cancels its attached instances.
Stopping a program affects the instances it owns according to the declared
capability contract. None of those operations clears the entire room's reflection
state or stops unrelated music. A room teardown can release the shared renderer
after all native workers have finished.

Reuse decoded assets, bound queued PCM, decode off the render thread and allocate
an explicit voice budget. Reserve conversation intelligibility without silently
stealing an unrelated source and reporting it as successfully completed. Seekable
clips and live streams have different restart and buffering semantics; adapters
must report those capabilities honestly. Supported formats and limits need
device measurements before they become user-facing import promises.

## Conversation capture and mixed audio

Conversation, media, ambience and effects need separate semantic roles. User
volume or ducking choices are independent of source identity and event rules.
Continuous music must not hold Live input closed indefinitely.

`SpeechOutput.isMicrophoneSuppressed()` and speech mailbox protocol 2 separate
microphone readiness from played-sample and drain receipts. A reflection tail must
not falsify PCM completion or trigger a PCM-stall timeout.

The experimental `RoomAudioOutput` observes final listener activity. It can test a
speech-only reflection tail, but cannot attribute a mixed signal to individual
sources. It is therefore not the finished general-audio capture policy and stays
out of the production scene. Browser echo cancellation being requested does not
prove that native Unity sound is included in its reference.

The preferred long-term path is an echo reference covering the actual rendered
native mix, with appropriate integration into the existing Live capture path.
Until a supported implementation is proven on Quest, keep room reflection
activation gated. Any fallback that pauses, ducks or suppresses other audio must
be explicit, observable and tested; a silent permanent microphone gate is not an
acceptable fallback.

## Acceptance before release

- User controls and the agent produce the same persisted emitter definitions,
  action receipts and event traces, with undo for definition edits.
- Clip, looping ambience and live-stream playback coexist; stopping one leaves
  the others running, including their remaining reflections.
- Native speech comes from the current avatar's mouth through model changes,
  movement and joint animation. Ordinary artifact audio keeps its existing chat
  behaviour unless the user or agent attaches it to an emitter.
- A continuous radio or ambience source does not prevent a novice from speaking
  to Live. Test real captured audio, both managed and BYOK, interruption, room
  reflections and headset speakers as well as headphones.
- No recorded/source audio, access token or live connection leaks into world
  snapshots, traces or exported programs. Permissions remain owned by the
  existing capture/provider flows.
- Tests cover buffering, disconnect, invalid formats, cancellation, multiple
  emitters, deleted targets, workspace changes, app suspension and device loss.
  Report source/voice budgets, real Quest timing and coverage separately from
  simulated or muted desktop tests.
