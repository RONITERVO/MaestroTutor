# Shared world audio

Users and the in-app agent must be able to define audio for objects and behaviours,
including imported or generated clips, procedural sounds, live sources and
event-driven playback.
Maestro's mouth is one emitter in that system. The familiar chat continues to own
providers, account access, generated media and Live conversation.

The current implementation exposes reusable procedural and imported WAV sources, object emitters,
finite or looping native playback, independent controls and playback events through
the shared capability catalog. Longer media, other codecs and live stream adapters remain pending. Finished local chat WAV attachments can now be imported as reusable sources
through the same checked native preview. Maestro speech still uses its existing
native speech session; ordinary artifact playback remains in the web app until
the user or agent explicitly imports, attaches and plays a world sound. Room reflection output and its background map scheduler are under
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

Sources and emitters remain in the existing room journal, temporary-room fork,
paired snapshots and portable archives; this adapter adds no saved format. Construction resource
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


### Production playback lease

`NativeSpeechOutput` and `WorldAudio` now share `PcmOutputLease`. Callers retain
provider/instance identity, source definitions and decode reservations; the lease
owns the temporary source, filter, carrier and view. Both paths preserve the same
`SpeechPcmStream` receipts. Gain, pause, pose, availability and completion have one
renderer boundary. Readiness itself never advances a source clock. A closed lease
cannot be restarted, and failed preparation closes its transferred stream.

Retain the view independently of its components so cleanup still removes it after
an AudioSource or filter is lost. Source disappearance must not leave an orphaned
GameObject or generated AudioClip. The focused native regressions verify this,
other-voice isolation, rejected setup and fresh speech generations after view loss.

This production refactor still uses Meta direct spatialization. It does not route
room reflections or install the isolated candidate. `IsDrained(now)` currently
requires sealed source receipts; the candidate adapter must additionally wait for
its native final-block/output-delay receipt, without extending the PCM cursor.
World events and object retention belong to the original exact playback instance.
The browser mailbox's existing close remains Stop, not an implicit natural end.


### Stopped voices retain the shared tail observation — 2026-10-12

The full check on `c6f195b8` caught a playback-lease regression: Stop released the
speech owner's reference to the shared mixer monitor, so its capture gate opened
while a different routed voice could still be audible. Stop now disposes only its
own playback lease and retains the independent monitor observation. Existing
two-voice and room-replacement assertions remain unchanged.

The first repair check reached 19 passing case markers before the repeated-room
fixture reproduced the earlier Meta native crash. Its dump again records a null
read at `MetaXRAudioUnity.dll + 0xa1509`; this does not establish the initiating
cause. The fixture now logs cycle, stage and output format directly to Unity's log
so a crash cannot erase that evidence before NUnit writes XML. With those
additional diagnostics, all 20 targeted cases and eight room cycles pass, along
with catalog equality, the log gate and normal editor exit. The crash remains
unresolved; a subsequent passing run is not its fix. Full app verification and
both native journeys are tracked separately in `.quest-evidence/audio-lease-monitor`.
Production reflections and automatic map scheduling remain disabled.


### Explicit finite speech completion — 2026-10-12

The shared speech output now distinguishes an open `drain()` sample fence from
terminal `finish()`. Finish seals the current reset epoch, is idempotent, and
requires the output renderer's final receipt before natural completion. Reset,
Stop, replacement and disposal cancel that exact owner. Temporary silence and an
empty packet queue cannot end speech. Browser worklet and scheduled outputs keep
their hardware-tail waits; Quest keeps the native PCM cursor separate from its
renderer completion boundary.

Book speech protocol v3 carries the exact final sequence and submitted sample
count, only after native has accepted every browser packet. Completion travels
alone, including for empty speech. The native owner rejects mismatched counts,
combined PCM/completion, stale document/revision/generation and post-finish writes.
Both input-complete and renderer-drained flags are required in v3 receipts; old
protocols cannot open a voice. Shared room activity may still suppress capture
after this particular voice finishes.

TTS and cached replay finish finite input. Live's quiet-window checks remain open
fences; only provider finalization closes admission after already accepted
messages and decoding. Late accounting remains eligible, but late content cannot
write into a sealed voice. An interruption bypasses the ordered completion wait
to cancel it immediately. Turn persistence and delegated actions await the final
renderer receipt, and cancelled speech cannot report a completed turn.

The focused checks pass 88 web cases and 21 native speech/lease/mailbox cases,
including actual DSP callback completion, exact counters, long browser bursts,
empty input, reset and stale receipts. The final Live lifecycle pass covers 40
cases after tightening Stop: no second finish, completed turn or spurious error.
Usage received during the final tail is retained even if Stop invalidates the
queued content. A regression reproduces transport close overtaking an accepted
`turnComplete` callback; teardown now flushes that queued finalizer before
releasing the reply. Android reports 128 passes and two optional private-file skips;
the release browser AAR builds and lint has zero errors (six warnings). Catalog
equality and normal native exits pass. Full verification at `c93a98c6` passed
1,140 EditMode and 1,263 PlayMode cases with the three expected private-asset
skips. Both native headless and original-book journeys passed with normal client,
editor and terminal exits; all 907 native source/mirror hashes match the frozen
input. These journeys use local scripted providers. CI passed 2,880 app/shared,
100 Functions, 57 billing and 31 Live gateway tests plus the build/release gates.
Exact receipts are retained in `.quest-evidence/speech-finish`.

The preceding monitor repair at `0b662089` completed full verification: 1,140
EditMode and 1,258 PlayMode passes, three expected private-asset skips, and both
native headless/original-book journeys with client/editor/terminal exit zero.
All 907 native source and mirror hashes match the frozen input. These journeys
use local scripted provider responses; no paid provider or headset was accessed.

This is a production completion contract, not installation or acceptance of the
isolated reflection backend. Production reflection output and automatic map
scheduling remain disabled. The retained Meta native map-worker crash is
unresolved; Android/IL2CPP and physical Quest/Live acceptance remain separate gates.

### Existing audio streams and bounded voices — 2026-10-12

The isolated renderer accepts an exclusively transferred application PCM stream
and bounded playback-instance IDs. It preserves queued input, completion and
source receipts without a second queue. Aliased streams cannot gain a second
owner; rejected fresh inputs close without evicting existing playback. Graph
capacity and per-slot poses are immutable, while all slots participate in native
response, reset, tail and retirement handling.

All 34 candidate cases pass, including the current nine-source app combination,
reflected output from the ninth slot, slot reuse, stale handles and actual device
reset. All 53 native graphs are released with zero validation diagnostics and
normal editor/wrapper exits; 19 input hashes match. Exact evidence is retained in
`.quest-evidence/audio-voice-admission`. See the
[reproducible evaluator](Tools/AudioBackendEvaluation/README.md#existing-streams-and-bounded-voice-admission).

This is still isolated Windows evidence. The graph's configurable capacity does
not replace production reservations, and exclusive transfer is not concurrent
backend handoff. App gain/distance/spatial semantics, canonical geometry and
obstruction, Android/AOT and Quest/Live acceptance remain required. No candidate
backend or production reflections are enabled; the retained Meta crash remains
unresolved.

### Candidate presentation parity — 2026-10-12

The isolated renderer now supports exact-handle gain changes and immutable
spatial settings. Final output gain applies to dry and wet tails without replay
or resimulation; gain zero advances playback while Pause does not. Nonspatial
mono playback bypasses HRTF and room contribution and matches Unity's centre
panning. Direct logarithmic distance response is compared with real Unity output
for default and custom minimum distances, including a coincident source and
positions beyond maxDistance. Native reflected propagation is not attenuated a
second time by that direct-distance factor.

Borrowed anchors follow real parent translation, rotation and scale after avatar
updates. Unchanged poses do not resimulate. Disabled/lost anchors and stale
bindings cannot cancel a replacement voice or destroy the caller's transform.
The first new run caught nonspatial output at 1.896875 times the reference power;
the fixed path preserves the original assertion and playback receipt contract.

All 41 cases pass with 60 native graphs released, zero native diagnostics, normal
editor/wrapper exits and 22 matching input hashes. Evidence and the failed run
are retained in `.quest-evidence/audio-presentation`; see the
[reference measurements and scope](Tools/AudioBackendEvaluation/README.md#presentation-controls-and-unity-reference-measurements).
The application is unchanged. Production adapter/reservations, accepted geometry,
obstruction, capture monitoring, Android/AOT and Quest/Live acceptance remain
required. No production reflection backend is enabled, and the Meta fault remains
unresolved.


### Candidate immutable geometry boundary — 2026-10-12

The isolated renderer now consumes copied mesh/placement values through the same
`IAcousticGeometry` definition used by active surfaces and dormant geometry. A
native mesh can serve multiple exact instances. Moving, rotating or scaling an
instance retains the mesh and source playback; replacing topology retires its
old resources under the existing worker lease. Any geometry revision fences
obsolete reflected output, including an edit at the final submission boundary.

All 47 candidate cases pass, with 67 graphs, 76 mesh owners and 77 instance owners
released, zero native diagnostics, matching input hashes and normal process exits.
Tests include destroying the original view, mutating borrowed arrays, asymmetric
coordinate conversion and reset during a retained worker. The first failed run
and its corrected physical test conditions are retained. See the
[reproducible geometry evidence](Tools/AudioBackendEvaluation/README.md#immutable-mesh-instances-from-accepted-geometry).

The production geometry bridge is still required: active/dormant ownership,
preparation budgets and unavailable boundaries must retain their current semantics.
This is a renderer input boundary, not a second persistent scene or automatic
world streaming. Direct obstruction, material authoring, capture monitoring,
Android/AOT and Quest/Live acceptance remain open. No application runtime, saved
schema, capability, reflection gate or device installation changed.

### Candidate Android AOT build — 2026-10-12

The isolated candidate now separates runtime, tests and build tools. Its native
callback is static and annotated for IL2CPP; ARM uses the pinned NEON baseline.
The 48-case Windows regression passes with all 69 graphs, 78 mesh owners and 79
instance owners closed, zero diagnostics, matching source hashes and normal
exits. A synthetic nine-stream player probe verifies native samples, exact
completion and ownership without claiming audio-device playback.

A fresh ARM64/IL2CPP development APK builds with High stripping, zero build errors
or warnings, and 31 matching input hashes. Verification checks all 46 runtime C
exports, packaged dependencies and generated callback registration. The native
library exactly matches a reproducibly stripped copy of the pinned SDK. The
initial verifier failure and corrected full run are retained. See the
[reproduction instructions and evidence](Tools/AudioBackendEvaluation/README.md#android-arm64-build-and-aot-boundary).

This is a separate debug-signed probe, not an application package or Android
runtime acceptance. The 907 app sources remain unchanged. Production integration,
shared capture, actual Android audio, sustained Quest performance and Live
acceptance remain required; no reflection gate, release key or device changed.

### Shared geometry publication — 2026-10-12

`RoomAcoustics.DescribeGeometry()` now exposes passive descriptions of its existing
active and retained inputs. Stable transient keys survive view retirement and
restoration; the feed is independent of SDK handles and performs no mesh reads,
uploads or native-view loading. Room lifetime, placements, definitions and pending
fixed boundaries are explicit without changing the saved world or capability API.

The candidate adapter uses that feed and the shared cooperative preparation budget.
It coalesces definition changes, reuses unchanged payloads and validates separate
scan/virtual capacity before allowing staging to grow. Unknown boundaries hold
wet output at final submission while direct PCM continues. Disposal and device
reset preserve the hold. Four focused app lifecycle cases and all 55 isolated
native cases pass, with complete graph/mesh/instance closure and verified input
hashes. Full application verification passes 1,140 EditMode and 1,265 PlayMode
cases with three expected private-asset skips, both native headless/original-book
journeys, native catalog equality and normal process exits. All 914 source/mirror
hashes match; the journeys use local scripted providers. See the
[implementation boundary and retained measurement failure](Tools/AudioBackendEvaluation/README.md#shared-room-publication-and-bounded-acquisition).

The current isolated ARM64/IL2CPP debug build also passes with High stripping,
zero errors/warnings, 35 verified input hashes and normal exits. Native payload
and callback checks pass; the shared bridge has not executed on Android and
compilation does not establish behavior after stripping. No app package changed.

This supplies the geometry bridge, not production renderer selection. The current
Meta direct path remains in use. Its existing residency-transfer admission still
checks SDK context/handles and needs separation before a backend swap. Native
upload timing, production reservations,
obstruction, capture, Android runtime and physical/performance acceptance remain
gates; production reflections and automatic maps remain disabled.

### Atomic candidate presentation — 2026-10-12

A concurrent native-output test reproduced a publication race: the renderer could
see a new frame before its required geometry revision, admit the old room response
and output reflected samples. The candidate now publishes frame, revision and
availability as one immutable state. DSP readiness and final submission read that
state coherently; ordinary listener rotation preserves valid output.

The unchanged tests pass after the fix: all 57 cases, 79 graphs, 96 mesh owners
and 97 instance owners close normally, with zero diagnostics and matching input
hashes. The previously leaking block and real cross-thread submission cases now
produce exactly zero samples; repaired geometry restores output. See the
[reproduction and scope](Tools/AudioBackendEvaluation/README.md#atomic-presentation-and-concurrent-submission).
The updated ARM64/IL2CPP debug candidate also builds with High stripping, zero
errors/warnings, 36 matching inputs and verified native payload/signature. Its
Android runtime and physical audio path remain untested.

All 914 application/project C# inputs remain unchanged from the prior full native
and book/headless verification. This does not install the candidate or enable
reflections. SDK-independent retention, production integration, obstruction,
capture, native upload timing and physical Android/Quest/Live acceptance remain.

### CPU publication readiness — 2026-10-12

Prepared dormant geometry now finishes its shared pending-boundary state when
its current CPU arrays are registered under their exact owners. It no longer
waits for Meta's context, native handles or upload capacity. Renderer-specific
admission remains separate: native map requests synchronize their inputs and
refuse missing boundaries, even when another renderer can read the complete CPU
publication. No native upload, resource release or map-worker lifetime changed.

Two focused native regressions first reproduced a ready boundary remaining
pending. The identical tests pass after changing only the residency readiness
check and its caller. They cover a competing room owning Meta's context, actual
native surface-capacity exhaustion, valid readable retained geometry, placement
changes, native recovery and view restoration. The catalog's only changes are
the two runtime source fingerprints; capability definitions are unchanged.
Evidence is retained in `.quest-evidence/audio-cpu-ready`.
Full verification passes 1,140 EditMode and 1,267 PlayMode cases, with three
expected private-asset skips. Native headless and original-book journeys both
pass using local scripted providers, with normal client/editor exits. All 914
C# source/mirror hashes and the native/shared catalog agree. An accidentally
queued duplicate journey was cancelled after these required runs; its separate
record is retained and excluded from passing evidence.

This separates publication readiness, not all retention admission. Transferring
an existing view into the retained cache still requires Meta's admitted/cached
input; a bounded CPU acquisition fallback remains necessary before a backend
swap. The candidate renderer is not installed. Production reflections and
automatic maps remain disabled; the unresolved native worker fault and physical
Android/Quest/Live acceptance remain open.

### Bounded CPU transfer before native retirement — 2026-10-12

`RoomEditor.RetireNativeArea(area, cancellation)` now prepares acoustic inputs
asynchronously before adopting the existing retirement transaction. It does not
require Meta's context, admitted native handles or a previous native upload.
Header-only mesh admission and copying share the existing preparation window
(four units per frame or two milliseconds between indivisible units). CPU
reservations precede payload copies and retain the existing limits: 384 virtual
surfaces, 98,304 vertices and 49,152 triangles. This bounds batches, not the
worst-case time of one Unity mesh read.

Every live view stays available while preparation yields. Cancellation, a brief
runtime hold, changed saved dependencies, placements, acoustic owners or surface
membership reject the complete transfer. Earlier reservations drain if a later
object fails. Competing preparations use the same capacity and frame budget;
adoption revalidates the connected closure before removing any identity.
Empty objects also retain a membership guard. There is no parallel authored
scene or change to saved world, actions, events or facts.

CPU publication preserves the exact registered geometry key independently of
native upload metadata. A busy Meta map worker keeps its older handles until it
returns; a transferred newer CPU definition never relabels those old inputs as
current. Restoration can return exact geometry ownership to live views while the
renderer is unavailable. The synchronous cached overload remains an internal
fast path; both paths adopt the same retirement transaction.

This is an internal loading prerequisite, not an enabled automatic unload policy
or installed replacement renderer. Restore-time mesh comparison and native upload
timing still need their own budgeting work. Production reflections and automatic
maps remain disabled. The unresolved Meta worker crash, Android/Quest audio,
capture/Live and performance acceptance remain open.

Eleven new PlayMode cases cover unavailable-renderer retirement/restoration,
readable payloads and exact identities, cancellation, accepted edits, runtime
placement, new surface membership, brief holds, capacity refusal/retry, competing
preparations, partial-area failure and an actual busy native worker. The final
complete run passes 1,140 EditMode and 1,278 PlayMode cases, with three expected
private-asset skips. Both native headless and original-book journeys pass with
local scripted providers and normal client/editor/wrapper exits. All 917 C#
source/mirror hashes match (913 app files plus four third-party inputs), and the
native/shared catalog agrees. Its only changes are five runtime source entries.

Evidence is retained in `.quest-evidence/audio-transfer`. Earlier compiler and
fixture failures remain separate: the construction fixture first exceeded its
existing size limit; the partial-failure fixture then assumed generated-ID order.
The corrected fixture selects the last member of the actual closure and retains
its assertion that earlier reservations existed and were released. The final
full run has no failures. These are Windows native results, not Android/Quest,
paid-provider or Store acceptance. The isolated candidate's source inputs and
prior evaluation/build evidence are unchanged.

## Room-scoped PCM backend admission — 2026-10-12

Speech and authored world sounds keep their existing application `PcmOutputLease`.
Its SDK-independent core now selects an explicit room-scoped `PcmOutputBackend`
once at admission. Selection and emitter controls run on the Unity main thread;
producer/audio-callback synchronization stays inside the shared stream and native
owner. The Unity/Meta direct implementation lives in a separate
partial file and remains the application default. An absent, disabled or
unconfigured selected backend refuses admission; it never silently falls back
or reroutes an already transferred source. Provider generations, saved audio
bindings, decode reservations, capture policy and event ownership remain with
their existing callers.

A permanent ownership claim lives on `SpeechPcmStream`, covering aliases across
the application lease and direct candidate bindings. Rejected aliases cannot
close the true owner's stream. Failed fresh admissions close their inputs;
selection loss closes all of its leases, even if one renderer's disposal throws.
Replacing a selection requires its leases to stop first, and disposing a stale
selection handle cannot stop the replacement. Source playback and downstream
renderer completion are both required before a lease reports natural completion.

The isolated candidate adapts that actual core, selector and PCM source, copied
byte-for-byte. It does not include the Unity/Meta default-adapter partial. Its
selection borrows the native graph and owns only its voices/anchors: prequeued
input stays paused until Start, current parent motion follows through LateUpdate,
and gain, pause, exact Stop, source loss and device reset use the existing native
ownership path. It adds no dummy per-voice AudioSource. The player probe roots the
same adapter and app lease for ARM64/IL2CPP High stripping verification, while
rendering its test signal into private memory rather than device speakers.

This is an evaluated integration boundary, not production renderer promotion.
The candidate is not installed in the app. Shared capture and Live routing,
production reservations, direct obstruction, Android runtime/perceptual checks
and Quest performance acceptance remain required. Production reflections and
automatic maps remain disabled; the retained Meta map-worker fault is unresolved.

The isolated evaluation passes 64 cases, including seven new lease/player-entry
cases, with 88 graphs, 105 mesh owners and 106 instance owners closed and zero
native diagnostics. All 39 recorded inputs and 35 copied C# files match. Its
Windows listener captures real rendered samples and mutes them before speakers.
The main-thread player lease entry also passes on Windows.

The separate Android ARM64/IL2CPP development build passes with High stripping,
zero errors/warnings and 41 matching recorded inputs. Verification finds all four
lease/adapter/player-entry types in generated IL2CPP and checks 46 required native
functions, the reverse callback, dependencies, absence of test assemblies and
exact reproducible stripping of the pinned SDK binary. APK SHA-256 starts
`7570A6DE`; this APK was not installed or executed on Quest.

Evidence: `.quest-evidence/audio-lease`, candidate run `6845e0194de2`, Android run
`38988bddbf62`. The earlier 62/63 run `4e2e2e1677b6` is retained: its new tail
fixture disabled audio processing before the response handshake could complete.
The corrected fixture waits for readiness while the pre-start source stays
paused, then takes manual control; the runtime was unchanged for that failure.

The complete app verification passes 1,140 EditMode and 1,285 PlayMode cases,
including all seven backend-lifetime cases, with three expected private-asset
skips. Native headless journey `d336708f627b4c7e830d043356bc38c3` and original-book
journey `0f1becde3bf442bcba64a64fd58b832f` both pass with normal client, editor and
wrapper exits and local scripted providers. All 920 C# source/mirror hashes match
(916 app files plus four third-party inputs). Native/shared catalog equality and
the source gate pass; action/event/fact definitions are unchanged and only four
runtime source entries change. The focused application run passes 28 cases.
No headset, paid-provider, release-key, deployment or Meta operation was used.


## Bounded rendered echo reference — 2026-10-12

The app listener tap now offers an explicitly owned `EchoReferenceStream` alongside
its existing activity monitor. The optional stream preserves interleaved mono or
stereo samples without clipping or changing output, and repackets arbitrary DSP
blocks into 10 ms frames. Its fixed ring holds at most half a second of PCM. Reads
and writes allocate no managed memory after admission. Consumed samples, gaps and
closed owners clear their retained PCM; no samples enter a saved world, program,
trace or provider request.

Each frame carries a render timestamp, sequence, epoch and the latest paired DSP
and monotonic observation clocks. Invalid samples/formats, missing render intervals,
overflow and stale observations report a gap instead of inventing silent frames.
The capture adapter must reset its canceller on a new epoch or closure and align
microphone timing separately. These timestamps do not establish acoustic delay,
hardware latency, clock-drift correction or echo cancellation.

One listener accepts one explicit reference owner. Monitor replacement does not
cancel it; disposal of an old owner cannot close a newer one. Component/listener
loss, pause, focus loss and device reconfiguration close the reference. Resuming
never reopens capture. Ordinary production playback does not open this optional
stream, and the existing Live microphone policy remains unchanged.

Coverage ends at the Unity listener filter: downstream filters, Android mixing,
WebView artifact audio and the physical speaker/microphone path may differ.
Integration needs one existing capture owner, a supported canceller, calibrated
render/capture alignment and actual Quest microphone/provider acceptance. Continuous
ambience must not become a permanent Live input gate. Reflections and candidate
backend promotion remain gated until that complete path is verified.


The complete app checks pass 1,149 EditMode and 1,289 PlayMode cases, plus both
offline native and original-book journeys. The isolated candidate passes 67 native
cases and its ARM64/IL2CPP High-stripping build. Evidence is retained in
`.quest-evidence/audio-reference`, with exact input hashes and earlier failed
fixture attempts. These checks establish the reference's buffer, render and
ownership behavior; microphone cancellation and Quest acceptance remain open.


## One capture owner for original Live and STT — 2026-10-12

The original application now consumes a platform-independent microphone contract:
prepared mono PCM16 at 16 kHz, one delivery consumer, explicit flush and terminal
close. The browser adapter alone owns its microphone tracks, AudioContext and
capture worklet. Direct Live startup, local Whisper pre-connect detection and
Live STT use that same ownership path; gated capture transfers without opening a
second microphone. The Live controller supplies its capture factory to the trigger.
No browser graph handles cross the handoff or remain in session orchestration.

Pending acquisition is cancellable even though browser permission itself is not.
Late streams and prepared owners close without replacing current capture. Shutdown
stops delivery before draining downstream packet work; flush failures still release
the microphone. STT also fences starts that lose to Stop or a newer request before
acquisition, and cannot overwrite the current owner with a stale trigger. Closing
a handoff discards its retained PCM and detaches its consumer.

The original speech gates, packetization, context, provider connection, usage and
transcript handling remain in place. Browser echo-cancellation constraints remain
requests to the browser; this contract does not establish native echo cancellation.
No native capture backend is selected. A Quest adapter still needs supported DSP,
render/capture clock alignment, gap recovery, all-output reference coverage,
permission/session ownership and physical/provider acceptance. Production reflection
and microphone gating policies are unchanged.

Verification and local generated-audio evidence are retained in
`.quest-evidence/echo-processing`. The full web/shared suite passes 2,894 cases
(14 new capture/ownership cases), lint, type checking and the production build.
The source boundary guard passes. Existing Unity and Android candidate evidence
remains separate; this increment changes no C# or native audio binary.

The compiled browser fixture also passes in real headless Chrome with generated
WAV microphone input: direct capture delivers nonzero PCM, the trigger confirms
19,200 samples, and the connection handoff preserves 3,200 buffered samples plus
continuing input. Both real microphone tracks end, no samples arrive after close,
and no page errors or external requests occur. The detector reply is scripted;
this is not a Whisper quality or provider test. Run `npm run test:capture:browser`
with Chrome installed; CI runs the same compiled fixture. No physical microphone
or speakers are used. Earlier development-server import stalls are retained and
excluded from passing evidence; the final probe uses a production-compiled fixture
and an ephemeral loopback server with a bounded runtime.


## Native echo-processing candidate evaluation — 2026-10-12

`Tools/EchoProcessing` now source-builds pinned standalone WebRTC APM 2.1 and its
matched Abseil dependency. The isolated C ABI accepts explicit paired 10 ms render
and capture frames, processes actual AEC3, and produces mono PCM16 at 16 kHz.
It preserves playback samples and requires a fresh epoch after missing/stale
frames or invalid input. Reset and destruction retire the owned DSP; no native
microphone or app adapter is selected. See the [reproduction guide](Tools/EchoProcessing/README.md).

The three reproducible evaluation profiles retain eight bounded generated-audio
scenarios plus ownership checks. The default `stock` profile preserves the prior
APM settings. `stationary32` uses longer filters and stationarity handling.
`measured32` adds caller-supplied calibrated delay, 4 ms headroom and faster coarse
adaptation while preserving the long filter. All remain outside the app. Each
also passes three steady-state cases per locally synthesized English/Spanish voice.

Stock processing fails noisy near-end audio and all five onset profiles.
The stationary profile passes those checks but fails both abrupt acoustic-path
changes and 18 of 24 additional delay/reflection variants. Internal traces showed
late delay changes resetting a recovered filter, and louder reflections can hide
weaker direct paths. The measured profile passes all 40 generated scenarios,
including the same changing paths and supplied-delay errors of -4/0/+4 ms.
Neither modeled timing nor these finite offsets prove actual Quest calibration,
continuous drift tolerance, immediate recovery or perceptual speech quality.

CI evaluates all profiles, reports exact failing quality cases and hard-gates
established passing groups; all measured-profile diagnostics are mandatory.
Core contract errors, crashes, missing results and regressions fail the run.
The measured profile records zero known generated-quality failures while physical
speech and Quest acceptance remain false. See the reproduction guide for thresholds
and coverage. Source hashes, rejected variants and failed attempts remain in
`.quest-evidence/echo-native`, `.quest-evidence/echo-quality` and
`.quest-evidence/echo-path`. A corrected band-limited fixture avoids microphone
aliasing; historical metrics from before that correction are not directly comparable.

Promotion still requires native microphone ownership, measured clock/delay/drift
alignment, all-audible-output coverage, perceptual speech checks, pause/permission
recovery and actual Quest/original-Live acceptance. Cross-compilation for Android
ARM64/API 32 is separate from device execution and Unity packaging. This candidate
cannot clear the separate Meta acoustic-map worker crash or enable production
reflections/full duplex.


## Timestamped Android capture boundary — 2026-10-12

The isolated evaluation now includes a native input owner and an Android AAudio
implementation. It prepares mono/float/48 kHz input, starts explicitly, assembles
partial reads, and retains hardware frame positions and monotonic timestamps
separately from observation time. Startup unavailability waits within a deadline;
invalid timestamps, overrun, sample gaps or disconnects close the device and fault
the owner. Pending input cannot escape after closure, and nothing reopens itself.

Twenty scripted native cases exercise this ownership/timing boundary without
opening a microphone. The Android probe cross-links the real AAudio factory and
timestamp imports but remains unexecuted. See the EchoProcessing reproduction guide
for requested preset, buffering, freshness limits and thread ownership. No app
permission or focus hook, native bridge, measured render-clock alignment, drift
resampling or sole-capture lease integration exists yet. The module therefore
remains outside the app, with the original browser/Live capture owner unchanged.


## Timestamped Android playback endpoint — 2026-10-12

The isolated native evaluation now also owns a callback-driven AAudio output for
an already spatialized stereo/48 kHz final mix. It records the exact callback PCM,
including known silence during queue underfill, in a bounded reference ring.
One worker submits audio, drains references and observes hardware presentation;
the callback only transfers samples and signals faults. Closing fences callbacks
before clearing storage. Device xruns, gaps, reference overflow and invalid clocks
retire the endpoint rather than fabricating continuity.

Completion requires hardware presentation to reach the final source frame.
Callback consumption alone is insufficient; stale, foreign or superseded clock
observations cannot complete playback. Capture and playback share the platform
frame/time type, but clock pairing, measured delay and drift correction are still
separate work. This prevents treating a Unity listener observation as speaker time.

The runner includes 22 scripted playback groups and 120,000-frame concurrent
transfer, alongside the existing 20 capture cases. Neither opens hardware.
Android cross-compilation includes the actual AAudio callback/timestamp functions.
The endpoint is outside Unity Assets and does not replace the current audio path.
Connecting the final mix, original output/capture leases and complete audible-source
coverage, then verifying real Quest lifecycles and speech, remain promotion gates.


## Clock-aligned duplex candidate and tonal failure — 2026-10-12

The isolated native worker now connects the input/output frame contracts to the
existing APM ABI. It fits separate hardware clocks, derives delay using the actual
reverse/capture call times, and continuously resamples capture to the render
cadence using the pinned upstream sinc implementation. Sequence gaps, invalid
owners, stale clocks, missing input and queue overflow retire the paired epoch.
The candidate has no device acquisition or app backend selection.

Twenty-seven contract cases and three broadband quality configurations exercise
constant clock skew up to 1,800 ppm relative, timestamp jitter and actual APM
processing. The measured profile achieves about 39–49 dB suppression while
preserving near speech, with modeled delay errors below .65 ms. These results
assume bounded timestamp error; they are not physical calibration or long-session
acceptance. See the EchoProcessing guide for exact limits and measurement windows.

The runner retains cold tones, tones after broadband training, and a reflection
change during those tones, across the same three clock configurations. The
measured profile fails 3, 1 and 3 cases respectively. All thresholds stay unchanged;
broadband success cannot substitute for these failures. Controlled private source
changes isolate the narrow-tone guards, but are not shipped. The screened SpeexDSP
alternative also fails required scenarios and is not selected. See the
[diagnostic decision](Tools/EchoProcessing/TONAL_DIAGNOSIS.md).

Resolving tonal echo, the shared PCM ownership/lifecycle bridge, final-mix and
all-audible reference coverage, and actual Quest/Live acceptance remain required.

## Native capture fragments — 2026-10-12

`CaptureInput.FlushPending` publishes the actual valid prefix of a pending short
read with its original timestamp and exact sample count. It leaves the owner open;
`Close` remains terminal cancellation. The duplex worker accepts contiguous chunks
of 1–480 samples, preserving identical PCM and source positions across whole or
fragmented inputs. Neither a short fragment nor a lack of future microphone samples
is interpreted as silence. See the [input contract](Tools/EchoProcessing/README.md#accepted-input-fragments-and-flush).

This boundary does not acknowledge the original app's `PcmCapture.flush` promise:
a downstream delivery fence and DSP-delay treatment are still needed. No app backend
selection or end-to-end last-word-preservation claim follows from scripted tests.

## Finished chat audio as a world source

`audio.chat.library` pages the newest eight available, completed local WAV
attachments in the current conversation after its bookmark. `audio.chat.item`
resolves one exact hash to its current `offerSet`. Generated audio notes/music and
user attachments retain declared origin and message identity in the offer. These
labels are untrusted data. TTS caches, private Live turn recordings, pending music
chunks, URLs and unsupported local formats are not offered by this adapter.

Use `audio.import` with `operation: "selectChat"`, that `offerSet`, and exact
`assetHash`. The ordinary import status shows receiving, checking and preview.
The native WAV parser verifies the original bytes and their hash before preview.
The ordinary `accept` step saves the private content-addressed file and creates a
source with one Undo. It does not attach an emitter or play the sound. Existing
emitter controls, `audio.play`, `audio.start`, buttons and event-driven programs
then use the same source. Undo removes the source edit while preserving its file.
The generated book controls provide the same select/load-current/accept actions.

Chat images and sounds share generation fencing, ordered 16 KiB chunks, duplicate
acknowledgment handling and transfer limits. The unchanged 32 KiB room wire gives
commands priority and alternates media priority when both transfers are pending.
Only selected bytes enter native memory. A changed conversation, bookmark, source
set, room, focus or interrupted transfer revokes an unaccepted chat selection.
Accepted writes finish under the existing import ownership rules; inspect their
receipts before retrying. Merely listing or receiving offers changes no room file.

The original app still owns generation, managed/BYOK access and its messages.
There is no second provider client, implicit URL download, recording or automatic
playback. WAV duration/codec/library/voice limits above still apply: a long or
invalid WAV may be offered but fails checked preview with a visible reason. More
codecs, longer media and live sources remain required adapters. This transfer
implementation is not audible, microphone coexistence or Quest acceptance.
