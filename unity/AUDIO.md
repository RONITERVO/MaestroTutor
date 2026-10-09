# Shared world audio

Users and the in-app agent must be able to define audio for objects and behaviours,
including imported or generated clips, procedural sounds, live sources and
event-driven playback.
Maestro's mouth is one emitter in that system. The familiar chat continues to own
providers, account access, generated media and Live conversation.

The current implementation exposes reusable procedural and imported WAV sources, object emitters,
finite or looping native playback, independent controls and playback events through
the shared capability catalog. Longer media, other codecs and live stream adapters remain pending. Maestro speech still
uses its existing native speech session, and artifact music still plays in the
web app. Room reflection output is under development and is not installed by
`MaestroRoom`. These audio changes have not been accepted on the headset.

## Current executable slice

- `audio.source.edit` saves/removes a named source, with a generated stable ID,
  current revision checks and Undo. Source kind `tone` provides sine,
  triangle or seeded noise, a frequency sweep and an attack/release envelope.
  Kind `clip` references exact imported bytes by SHA-256 and their inspected duration;
  it never contains a path, URL, credential or PCM buffer. Source facts use version 2
  and a fixed record with both blocks; only the selected `kind` is executable.
- `audio.import` uses the shared native file-picker owner. `select` returns before
  the system chooser takes focus. `audio.import.selection` exposes a checked,
  unsaved preview. `accept` requires that request ID and exact asset hash, saves
  the private file, then creates a reusable source with Undo, without playing it.
  `cancel` abandons an unused preview. `refresh` verifies the library off-thread;
  its receipt returns the first checked file and `audio.library` pages two files
  with names, hashes and durations. Pagination stays within shared value budgets.
  Untrusted file names are bounded display data, never instructions.
- `object.audioEmitter.edit` attaches a source to an object root, recipe part or
  Maestro joint. Gain, spatial mode, attenuation distances and semantic role are
  saved together. Copying an object keeps its exact source reference but starts
  no playback. A referenced source cannot be removed until its emitters are edited.
- `audio.play` owns one finite playback instance until actual PCM consumption and
  the device output tail complete. Program cancellation, emitter removal/editing,
  target deletion, workspace changes and app/audio suspension terminate that
  instance. Movement and grip channels are independent. Source edits only affect
  subsequent plays. Existing buttons and event programs can invoke this action.
- `audio.start` returns an exact playback instance after the renderer consumes
  its first samples. Its explicit `lifetime: "room"` hands that instance to the
  room, so the starting program can continue or finish while the sound plays.
  `loop: true` repeats the captured source and envelope; `false` plays it once.
  Stopping preparation cancels it. Stopping the starting task after handoff does
  not retract room-owned playback.
- `audio.control` pauses, resumes, stops or changes the gain of that exact
  room-owned instance, using its current revision. Pause preserves queued PCM
  and resampling phase. Gain is transient; it does not edit the saved emitter.
  Stale controls cannot stop a replacement sound. A stopped or failed sound cannot
  be resumed, and focus/device loss never restarts one automatically.
- `audio.source.list`, `audio.source.definition`, `object.audioEmitters`,
  `object.audioEmitter` and `audio.playback` expose the same definitions and runtime
  observations to book controls, programs and the agent. Completion/failure belongs
  to the owning action's receipt; observed playback is not proof of audible output.
- `audio.instances` discovers active world sounds; `audio.instance` reads one
  exact identity, revision, phase, consumed cursor, gain and lifetime. These facts
  do not include the separate conversation renderer. `audio.instance.changed`
  delivers lifecycle/control transitions to existing event-driven programs.
  Its `after` revision selects the next change. Sixteen changes per instance and
  the latest 32 terminal instances are retained. Overflow and expired identities
  fail visibly, so a missed change cannot silently look like successful playback.

Room format 32 stores sources and emitters in the existing journal and temporary-room
fork; paired snapshots use v31 and portable archives use v30. Construction resource
bundles preserve their source definitions and remap source IDs together with emitters.
Asset hashes remain exact. Portable workspaces include the private audio library,
check payload hashes and WAV metadata, and report missing sound references in export,
selection, review and recovery receipts. They contain no runtime handles or queued
samples. A construction module on its own contains references, not embedded audio;
its destination needs the same library assets or a complete workspace transfer.

The first file adapter accepts ordinary RIFF/WAVE PCM 8/16/24/32-bit or IEEE float32,
mono/stereo, 8–96 kHz, .03–30 seconds and up to 32 MiB. Extensible/compressed WAV,
MP3/OGG, longer media and live streams need further adapters. The private library
holds at most 32 files/128 MiB. Original bytes are preserved; playback deliberately
downmixes stereo and resamples with an anti-aliasing filter to the existing 24 kHz
mono transport. It is not a stereo music player. Two cancellable decoding workers
bound simultaneous input buffers. RIFF sizes, frame alignment, sample format,
duration and finite float samples are checked before publication and playback.

Selection, checking and an unused preview hold workspace preservation. Cancellation
or teardown drains readers before releasing their selected file. An accepted file
write can finish before a later source edit fails or is cancelled; inspect the import
and refresh the library before retrying. Undo/temporary-room discard removes the
source edit, not the saved library asset. Import, save, restore and reconnect never
autoplay. Missing/corrupt payloads and mismatched durations fail visibly.

Current bounds are 32 sources, four emitters per object, 32 emitters per room and
eight simultaneous world voices in addition to speech. Tone duration is 0.03–30
seconds at 24 kHz mono. Decoding runs on a bounded, cancellable worker path;
concurrent instances share the decoded source. Each voice reuses the bounded PCM
transport, reusable producer buffer and DSP consumption receipts. Looping does not
allocate another copy on every cycle. No clock-only completion, automatic
voice stealing or new provider calls are involved. Role labels currently preserve
intent for the future mix policy; they do not grant microphone privileges.

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

The procedural adapter implements preparing, playing, paused, completed, stopped,
cancelled and failed phases. Buffering/disconnection phases will be introduced
with executable stream adapters. A room-owned sound has one active instance per
emitter. The eight-voice budget refuses a new start instead of replacing another
sound. Paused voices continue to occupy their slot. Playback handles and event
history are transient and never restored from a saved room.

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
