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
web app. Room reflection output and its background map scheduler are under
development and are not installed by `MaestroRoom`. Direct native obstruction
remains active without a reflection map. These audio changes have not been
accepted on the headset.

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

## Acoustic geometry survives view retirement

Direct acoustic inputs have their own bounded lifetime. Internal native-area
retirement transfers admitted virtual meshes from their live components to a
room-owned cache before destroying the render objects. The existing SDK handles
remain owned through that transfer, including during a native map calculation.
No renderer, texture, imported-model instance or destroyed component wrapper is
needed to keep those inputs alive. The cache holds validated vertex/index arrays
and transforms relative to the authored world frame. The journal remains the
only authored definition; there is no additional saved acoustic world.

Retained input reservations allow at most 384 surfaces, 98,304 vertices and 49,152
triangles. These CPU reservations survive an audio-device reset independently of
the existing native scene budgets. Unprepared, invalid or over-budget acoustic
input prevents area retirement; failed preparation releases its reservations.
Physical scanned surfaces stay with their tracking owner and cannot enter this
virtual-object cache.

Reloading identical geometry transfers its native owner to the rebuilt view.
Position edits, Undo and movement of the world frame update retained placement.
A changed saved definition invalidates the cached input until compatible geometry
is restored or native preparation replaces it; the existing omitted-geometry
count reports that gap. Such an input is not a stale wall or proof of clear space.
Deletion, workspace replacement and editor destruction release cache ownership.
SDK work already reading an input keeps its native handle until cancellation has
drained. Passive acoustic facts do not create cache entries or load a model.

This separates acoustic and rendering ownership; it is not an automatic acoustic
culling or regional streaming policy. Cold acoustic admission, sound-demand
footprints, independent replacement of changed dormant geometry, regional
physics/navigation/water support and larger-world budgets remain required before
automatic unloading or selective startup. Production reflection output and map
scheduling remain disabled pending their existing capture/lifecycle acceptance.

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


## Native lifecycle regression evidence

The PlayMode acoustic suites exercise actual engine audio resets while a native
map job owns geometry, map replacement during two-voice mixed playback, and eight
room/mixer replacement cycles with unused-asset collection. They observe the
listener signal as well as PCM consumption, and drain retired native ownership
before disposing callback state. These tests run through the standard
`unity/Tools/Verify-Quest.ps1` runner.

The Windows full-suite crash in `MetaXRAudioUnity.dll` recurred on source
`941cea77`: the repeated room/mixer test did not obtain its initial map within the
test deadline, then a native map worker faulted. The saved minidump identifies a
null read in `AudioSceneIRComputeCustomPoints`' native call tree, not its cause.
Application lifetime/cancellation and SDK behavior still need to be distinguished.
Focused passes and runs excluding the mixer fixture do not satisfy the full gate.
Exact evidence and scope are in
[the release coverage ledger](../docs/QUEST_AGENT_RELEASE_COVERAGE.md#native-audio-lifetime-stress-coverage--2026-10-10).

A separate cleanup regression reproduced the fixture's early return while an
initial map still owned native inputs. Teardown now drains that ownership even
without a later replacement gate. All 23 focused lifecycle/production-hold cases
pass, including eight mixer replacement cycles. This fixes test isolation only;
it does not establish the native crash's cause or lift the production hold.
The subsequent unchanged full verifier on `72a9444a` passed 1,136 EditMode and
1,255 PlayMode tests, with three expected optional private-asset skips and both
native integration journeys completing cleanly. This is a passing complete run,
not proof that the intermittent fault has been resolved.

## Audio backend lifecycle decision — 2026-10-11

[Meta's SDK release notice](https://developers.meta.com/vr/downloads/package/meta-xr-audio-sdk/)
identifies 85.0 as its final release, with future compatibility and bug fixes not
guaranteed. The project currently pins 85.0.0. This notice does not establish the
cause of the crash, and replacing a backend is not an accepted fix without tests.

Keep saved sources, emitters, stable asset references, acoustic surface intent,
playback receipts and human/agent capabilities owned by Maestro. Native handles,
plugin settings, baked map files and renderer-specific budgets are derived runtime
state. Do not introduce them into portable worlds or programs. A backend change
must preserve source identity, world placement, exact Stop ownership and lifecycle
receipts without requiring users to rebuild their creations.

The next audio integration boundary should own geometry registration, listener and
emitter updates, direct obstruction, reflection preparation and output teardown.
Its support/readiness must be observable; unsupported reflection preparation cannot
silently report success. Retain geometry and callback ownership until computation
finishes, and fence both simulation and audio-thread consumers before freeing
resources. A timeout requests cancellation; it does not prove either consumer has
released its inputs. Device resets must also invalidate cached native contexts.

Evaluate Steam Audio as a candidate before activating production reflections.
Its [Unity documentation](https://valvesoftware.github.io/steam-audio/doc/unity/index.html)
describes dynamic geometry affecting propagation, and its
[platform requirements](https://valvesoftware.github.io/steam-audio/doc/unity/getting-started.html)
include Android ARM64. Those are capabilities to investigate, not evidence of our
Quest performance or compatibility. The evaluation must use runtime-created and
imported geometry, cold geometry, moving emitters, multiple simultaneous voices,
room replacement, actual audio resets, cancellation and native teardown. Compare
measured output and CPU/memory cost, including Live's echo/capture behavior, before
choosing a replacement. No second production backend is installed by this decision.

Preserve the current directional-speech path and explicit reflection hold during
that evaluation. Passing desktop checks is not permission to enable reflections:
real Quest timing, audible behavior and multi-emitter Live acceptance remain
required. The full native regression gate remains mandatory and unchanged.


### Isolated native candidate evidence

The optional [native evaluator](Tools/AudioBackendEvaluation/README.md) now checks
Steam Audio 4.8.1 without changing the Unity project or its current renderer. The
runner checks the official SDK archive digest, compiles the probe using installed
Windows C++ tools, and retains a unique result with exact source hashes. Vendor
binaries remain local ignored evidence.

Twelve fresh context/simulator cycles passed with runtime-created geometry,
finite reflected samples, wall addition/removal/repositioning, source removal,
room deletion/reconstruction and joined background simulation. The first draft
was correctly rejected by native parameter validation; explicit valid unused
hybrid/volumetric defaults resolved those warnings. The retained passing run uses
right-handed SDK coordinates, validation enabled, one simulation thread and two
sources. Its small desktop timings are not Quest budgets or worst-case bounds.

This provides evidence for an explicitly owned native context and editable
geometry. It does not establish Unity DSP lifetime, concurrent per-voice tails,
HRTF perception, imported/instanced meshes, Android/IL2CPP compatibility, Live echo
handling or device performance. Runtime simulation calls are synchronous: cancel
further scheduling, discard stale generations and join an active call before
freeing its inputs. An adapter must additionally fence audio-thread consumers.
The current Meta renderer, production reflection hold and full native gate remain
unchanged. Evaluate the Unity integration boundary next; do not switch a shipping
world to this candidate on the basis of the standalone probe.

### Unity candidate result and reset requirement

The isolated Windows Unity 6000.3.24f1 project now tests the unmodified Steam Audio
4.8.1 integration at the actual audio listener. Direct wall obstruction,
reflection-only output, one-source destruction without losing the other source,
room deletion/reconstruction and a same-format audio reset passed. Samples are
measured before silencing; this is native DSP evidence, not perceptual listening
or Quest acceptance.

The changed-format requirement **fails**: after Unity applies 24 kHz, the manager's
simulation configuration remains at 48 kHz. Nonzero output after that reset is not
evidence of correct timing or spectral behavior. The stock package therefore does
not meet the adapter's reset contract yet. Keep the failing test runnable and
retain its nonzero exit; do not mask it with a geometry-only pass. The optional
[Unity runner](Tools/AudioBackendEvaluation/README.md#isolated-unity-integration-evaluation)
recreates the isolated project from the pinned archive and records exact hashes.

The next adapter must explicitly observe audio-configuration changes, retire
simulation/audio consumers, rebuild format-dependent resources and rebind live
sources from Maestro-owned identities. In-flight cancellation, pause/focus changes
and source/room replacement must use the same lifecycle. Test actual configuration
and output after each transition; a successful reset call alone is insufficient.
This is a requirement for the candidate, not a diagnosis or fix of the current
Meta fault. No app backend or production reflection setting changes here.


### Explicit lifetime prototype

The optional evaluator now has a separate `-OwnedLifecycle` mode. It uses the
pinned C API library directly, keeping the stock integration's failed reset test
unchanged. The prototype owns preparation, response/buffer lifetime and audio
consumer leases. A configuration change immediately invalidates voice commands,
retires the old graph, waits without blocking for its worker/readers, and creates
resources for the actual rate and block size. Repeated changes coalesce. Retired
errors remain diagnostic evidence without becoming the current configuration's
failure. No native handles enter saved source definitions.

The initial six isolated Unity PlayMode cases passed, including real 48/24/48 kHz and
1,024/256/512-sample resets, independently measured reflected voices, silence
until explicit restart, late-command rejection, reader-held retirement,
superseded preparation, failed preparation/retry and Close before completion.
All 17 graphs closed with clean native validation. The stale-worker-error case
was red before the generation check, then green. These are desktop lifecycle and
measured-output results; the test tones are silenced at the listener.

The initial prototype prepared its response for a fixed room and pose before any
DSP reader entered.
That made response ownership testable before the moving-response extension below.
Application integration, per-voice PCM, resource cost, perceptual output and Live
capture remain integration gates. The
current app still uses its existing renderer with reflections disabled. The
prototype does not resolve the earlier Meta fault or establish a Quest replacement.
See [reproduction and limits](Tools/AudioBackendEvaluation/README.md#explicit-lifetime-evaluation).


### Moving response ownership

The optional candidate now passes 13 Unity cases, extending the six above with
moving emitters/listener, independent head rotation, repeated room deletion and
reconstruction, coalesced edits, a paused DSP consumer, muted-voice restart and
update failure/reset ownership. All 26 graphs close with clean native validation.
The measured stereo level difference reverses on a 180-degree head turn without
recomputing the room. This is measured desktop output, not perceptual acceptance.

The pinned native convolution implementation uses a single producer and consumer
per response. One leased worker changes geometry/inputs; the audio callback owns
effect processing and acknowledges complete response consumption. Another worker
cannot publish until that acknowledgment, so paused consumers cannot accumulate
stale responses. Muted voices consume zero-input responses without mixing output.
A regression retaining the earlier skip-muted behavior failed before this change.

Keep desired scene identity outside native lifetime. Device reset rebuilds the
latest scene, including deleted boundaries. Geometry changes suppress outdated
reflections while replacement is pending; this gate must not suppress future dry
speech. Head rotation is independent of slower response computation. Current
worker failure interrupts and retires the affected graph; stale failure cannot
poison a replacement. Scene/format resources remain retained until both worker and
DSP ownership have drained.

Two synthetic voices and one authored room topology establish this protocol.
They do not establish production PCM handoff, imported geometry, continuous-motion
quality/latency, scene budgets, Android or Quest/Live acceptance. Before app
integration, verify voice replacement and Stop against real `SpeechPcmStream`
receipts and native convolution history. No candidate is installed in the app;
production reflections and automatic scheduling remain disabled.


### Application PCM boundary in the candidate

The owned evaluator now passes 22 cases using the unchanged application
`SpeechPcmStream` implementation, copied and hashed into its isolated project.
The candidate consumes ordered 24 kHz PCM16 through the same resampling and
rendered-sample receipts as the app. Test packets are synthesized signals, not
real provider speech. Source PCM accounting remains independent of native
reflection duration: natural completion can leave a fading tail, while explicit
Stop truncates that voice and invalidates its packet handle.

A new utterance resets direct, convolution and decoder history on the audio
consumer. Acknowledged reset serials request a fresh response on the existing
single worker; only that response enables wet input. Zero-input native consumption
continues while dry HRTF speech renders immediately. An in-flight older simulation
cannot satisfy a newer utterance's reset. Geometry validity gates only the wet
mix, and final submission rechecks both geometry and per-voice ownership. Closing
one voice leaves other voices intact. Retiring a graph closes all its PCM handles.
No callback allocates native effects or prepares scene geometry.

The 22-case run closes 37 graphs with clean native validation. It includes actual
Unity audio-format changes and stereo listener measurements, plus controlled
same-render-path probes for the first replacement block, late Stop/room edit at
submission, and natural tail/receipt separation. Deliberately skipping the native
direct-history reset reproduces old samples in a fresh empty utterance; the
restored reset passes. This extends the candidate's evidence without integrating
it into `NativeSpeechOutput` or accepting it as the Quest renderer.

Next establish production adapter ownership, direct-obstruction/attenuation and
imported geometry parity, source limits and Android/IL2CPP support. Full chat/Live
interruption and mixed-audio capture, real speech perception, sustained resources
and physical Quest acceptance remain required. The existing renderer and its
production reflection hold are unchanged; the previous Meta fault is unresolved.
See [reproduction](Tools/AudioBackendEvaluation/README.md#application-pcm-and-utterance-ownership).


### Shared PCM completion and pause admission

`SpeechPcmStream` now owns explicit input-complete, paused and closed state for
both app owners and the isolated candidate. `TryComplete(lastSequence)` seals the
exact accepted packet sequence and rejects later writes without discarding queued
samples. It is idempotent for that sequence. An empty or temporarily starved queue
is not implicitly completed. `Cursor.SourceDrained` becomes true only after sealed
input has actual playback receipts for every submitted source sample. Faulted or
closed streams cannot report successful natural completion.

Finite `WorldAudio` sources seal input after their last accepted producer chunk
and finish through `SourceDrained`. Looped sources remain writable. Existing exact
instance IDs, room/action ownership, pause/resume, source revisions, completion
notices and Stop behavior remain intact. This is transient transport state, with
no saved room or tool-schema change. The catalog update records only two runtime
source-provenance hashes. Browser speech remains open until its existing owner
closes it; neither silence nor an empty network queue invents an end-of-turn signal.

`Render` returns block admission, not an indication that its samples are nonzero.
A paused, stopped, duplicate or invalid callback is not admitted. A valid underrun
can still admit renderer-owned tail processing. Once Render has consumed a block,
a later Pause cannot drop that block without losing speech. The candidate carries
that admission to final mixing; subsequent paused blocks consume no source PCM
and produce no output. It still feeds zero input through native response consumers
so room changes do not become stale during pause. Resume keeps the queued source
position. Stop remains stronger: it closes the handle and discards that voice's
not-yet-submitted output.

The candidate passes 24 cases and releases all 39 graphs without native validation
diagnostics. A negative run gating an already consumed block on the latest pause
flag reproduces silent packet loss at submission; the correct admission contract
passes. Source completion and acoustic-tail retirement remain distinct. Production
reflection output is still disabled, and a future renderer must preserve natural
tails, bounded reservations and per-source activity without extending source PCM
counters or suppressing unrelated world sound during Live.


### Natural renderer completion

The current isolated candidate passes 29 cases, including five added tail and
owner-release cases. It distinguishes source playback receipts from downstream
binaural, convolution and Ambisonics decoder completion. Sealed input enters a
finite drain phase; the native `GetTail` calls retain their final returned block,
even when that call reports completion. The owner can release the exact voice
only after the last block passes submission and its output delay elapses. Release
returns the final source receipt; Stop, replacement and format reset invalidate
the old handle immediately. Neither operation can complete a newer utterance.

A temporary underrun stays open. Pause suppresses tail output while finite DSP
history drains; resuming cannot replay the suppressed echo. Invalid/backwards DSP
times do not advance tail state. A room edit invalidates echoes belonging to its
older geometry, even after a new simulation is ready. Tail-only native calls do
not consume a newly published impulse response, so they must not acknowledge it.
After draining, silent Apply calls resume consumption without reopening output;
a continuing second voice is checked across later scene updates and owner release.
This can defer another simulation through the bounded tail. The present fixture
uses a 0.25-second response and two voices; production scheduling and source-budget
behavior still need integration rather than inheriting those fixture limits.

The 29-case run releases all 45 native graphs with zero native validation
diagnostics. Both actual Unity callbacks and controlled calls to the same render
path are exercised with synthetic PCM. This is Windows evidence, not Android,
physical audibility or Live acceptance. No replacement renderer is installed in
the app, and the existing reflection hold remains. The next integration work is
production source/reservation ownership, real geometry and obstruction parity,
Android/IL2CPP, per-source capture activity and Quest validation.

A negative probe publishing completion before the final submission boundary
fails the new timing check (28 pass, one expected failure). Exact hashes verify
restoration of every input used by the passing run.
