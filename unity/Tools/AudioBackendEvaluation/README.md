# Native audio backend evaluation

This optional Windows C++ probe evaluates a candidate for Maestro's editable
world acoustics. It does not install a Unity package, change the current spatializer,
produce speaker output, query a Quest, or call a provider. It is not part of the
release gate and cannot clear the existing Meta native crash or Quest/Live gates.

Download `steamaudio_4.8.1.zip` from the
[official pinned release](https://github.com/ValveSoftware/steam-audio/releases/tag/v4.8.1).
The runner requires that archive's published SHA-256. It uses installed MSVC C++
build tools and Windows SDK headers; `CompilerRoot` and `WindowsSdkRoot` can override
discovery. Run from a PowerShell terminal:

```powershell
./unity/Tools/AudioBackendEvaluation/Run-Probe.ps1 -SdkArchive C:/path/to/steamaudio_4.8.1.zip
```

Each invocation creates a fresh ignored `.quest-evidence/audio-backend/<id>` folder.
`result.json` records exact source hashes, the SDK digest, compiler selection,
terminal result and scope limitations. Failed compilations, native validation
warnings, abnormal exits and a 30-second process deadline remain failed evidence.
No previous evidence or installed software is removed. No vendor binary is checked
into Maestro or included in the app by this tool.

The C API probe uses actual native geometry and output samples, with validation
enabled, SSE2, one simulation thread, two sources, 1,024 rays, eight bounces,
first-order Ambisonics and a 0.25-second convolution response at 24 kHz. Across 12
fresh context/simulator cycles it checks:

- Adding, removing and repositioning a wall changes direct occlusion.
- A runtime-created enclosed room produces finite nonzero reflected samples.
- Removing one simulation source preserves the other source's reflected output.
- Removing the room removes reflections; reconstructing it restores them.
- A background simulation completes and joins before its inputs are released.

The last check cancels further scheduling and waits for an in-flight call; it is
not interruption of the native call. The two-source check measures output before
and after source removal, not concurrent Unity DSP tails. The observed maximum
call duration is a small desktop diagnostic with validation overhead and other
host workloads, not a Quest budget, benchmark comparison or worst-case bound.

The evaluation does not cover Unity's audio thread, HRTF perception, stream
buffering, real microphone echo handling, Android/IL2CPP, suspension/device resets,
large worlds, runtime imported meshes, instanced moving geometry or headset
performance. These require adapter-specific tests and real-device acceptance.
Keep the saved-world and capability contracts described in [AUDIO.md](../../AUDIO.md)
independent of the chosen backend. Review the SDK's Apache-2.0 license and all
redistributed dependency notices before any shipping integration.

## Isolated Unity integration evaluation

A separate optional runner tests the official Unity integration without installing
it into Maestro. It requires PowerShell 7.4+, Python 3.11+ and the licensed Unity
6000.3.24f1 editor. Download `steamaudio_unity_4.8.1.zip` from the same official
release; this is a different archive from the C API SDK above.

```powershell
./unity/Tools/AudioBackendEvaluation/Run-UnityProbe.ps1 `
  -UnityArchive C:/path/to/steamaudio_unity_4.8.1.zip `
  -Editor C:/path/to/6000.3.24f1/Editor/Unity.exe `
  -Python python
```

The runner checks the published archive digest before extracting the unmodified
vendor scripts, assets and Windows CPU audio libraries into a fresh ignored
`.quest-evidence/audio-unity/<id>/project`. It copies the app's audio settings into
that isolated project and changes only that copy's spatializer. Unity may resolve
the pinned test-framework package. No existing app or project is overwritten.
An active Unity Editor blocks the run; it is never killed to make room for the
probe. Each owned child has a ten-minute deadline, and only the build helper's
private ADB endpoint is cleaned up. No connected devices are queried.

The fixture measures finite samples at Unity's real listener, then silences them.
It creates synthetic PCM and runtime room/wall geometry, without microphones,
user recordings or providers. It checks direct obstruction, reflection-only
output, destruction of one voice while another continues, room deletion and
reconstruction, and reset recovery after recreating the ephemeral PCM clip. It
also requires a changed device sample rate to update the simulation configuration.

**Current result: two passing cases and one failing requirement.** Geometry,
independent voices and reset with the same format pass. After a real Unity reset
from 48 kHz to 24 kHz, the candidate manager still reports 48 kHz. Output remains
nonzero, which alone cannot prove format-correct rendering. The failed case and
nonzero runner exit are intentional evidence of an unresolved integration gap;
they are not converted into success or skipped. The runner retains exact fixture,
preparer, runner and source audio-setting hashes alongside editor exits and XML.

This evaluates the stock package, not a completed Maestro adapter. An integration
must handle audio-configuration changes through an owned drain/rebuild/rebind
lifecycle and verify format-correct output, source identities and remaining voices.
Neither this evaluation nor the C API probe establishes Android/Quest performance,
Live echo handling or an accepted replacement for the current production renderer.

## Explicit lifetime evaluation

Run the independent ownership experiment with the same pinned archive:

```powershell
./unity/Tools/AudioBackendEvaluation/Run-UnityProbe.ps1 `
  -UnityArchive C:/path/to/steamaudio_unity_4.8.1.zip `
  -Editor C:/path/to/6000.3.24f1/Editor/Unity.exe `
  -Python python -OwnedLifecycle
```

This mode extracts only the unmodified C API binding and Windows `phonon.dll`.
It does not load the stock Unity manager or its global spatializer plugin. The
original default-mode failure remains independently reproducible.
`PreparedGraph` owns a native context, room, two simulation sources, convolution
responses, HRTF decoders and buffers for an exact sample rate and block size.
Initial preparation runs on one worker before publishing the graph. Audio
callbacks hold a read lease; retirement forbids new leases and disposal waits for
existing readers. The control thread polls completion without joining a worker or
blocking the audio callback. Multiple resets coalesce into one latest request.

The initial six PlayMode cases passed in the isolated Windows project:

- Real Unity resets at 48 kHz / 1,024 samples, 24 kHz / 256 samples and 48 kHz /
  512 samples rebuild the native format. Listener measurements contain the
  intended reflected 440 Hz and 660 Hz tones before they are silenced.
- Stopping one voice leaves the other audible in the measured output. Reset
  interrupts both and rejects commands from the old generation; only explicit
  restart produces new audio. Logical voice IDs remain unchanged.
- A held reader lease prevents disposal and replacement until it is released.
- Superseded preparation retains its native ownership until the worker finishes,
  discards that result, and prepares only the latest request.
- An injected preparation failure releases ownership and permits explicit retry.
- Close during preparation cannot publish or replay. A superseded worker error
  remains available as retired evidence without poisoning the newer configuration.

Those six cases include several assertions from the bullets above. Their teardown
confirmed that all 17 created graphs were closed and that native validation emits no warnings
or errors. A separate regression first reproduced the stale-error failure, then
passed after generation-aware error handling. The held-reader check deliberately
holds the same lease on the test thread; it does not stall Unity's audio device.
Actual reflected rendering and reset measurement use Unity's real audio thread.

This is a bounded lifecycle prototype, not a second production renderer. The
moving-response extension below updates the original fixed-pose experiment.

### Moving responses and consumer handoff

The moving-response extension established **13 passing cases**, including the original six.
The PCM extension below raises the current runner requirement to 22.
The pinned library has a single-producer/single-consumer convolution response
handoff. The adapter keeps sample rate, block size, IR duration and channel count
fixed within a graph. It reads the source-owned IR pointer before concurrent work;
ordinary native output fields are not reread while the worker writes them.

One leased worker applies the latest immutable listener/emitter/room snapshot.
The control thread coalesces intermediate requests and waits for an audio callback
to consume the completed response before scheduling another. No worker or native
context is created per incoming pose. Head rotation updates the decoder directly.
A geometry change silences outdated reflected output until its response is ready.
A reset preserves the latest desired scene, including a deleted room; it does not
silently rebuild the original fixture room. A failed current update retires its
graph and requires explicit recovery. Retired failures remain diagnostic evidence.

The added cases measure native output at the real Unity listener while:

- Moving each emitter and the listener out of and back into the enclosed room.
- Rotating the listener, reversing the measured left/right level difference
  without rerunning room simulation.
- Removing and rebuilding the room three times without restarting either voice.
- Coalescing twenty incoming edits while a worker remains owned; outdated
  geometry remains silent until the latest response is consumed.
- Changing the device format while an update worker is retained, then handling
  its stale failure without resurrecting deleted geometry.
- Failing an active update, interrupting output and explicitly recovering.
- Muting a voice through multiple scene updates and resuming its latest response.
- Holding audio consumption without blocking the audio thread, allowing at most
  one unconsumed response, then consuming it and applying the latest queued scene.

Skipping muted DSP consumers reproduced a failure in the new restart case.
Feeding them zero input and consuming their responses without mixing their output
corrects it. The final 13-case run closes all 26 graphs, with zero native validation
warnings/errors. The held-worker gates retain ownership after a real simulation;
they do not claim interruption inside the synchronous SDK call. The consumer hold
is fixture instrumentation, not physical headset suspension acceptance.

The source review follows the pinned [producer](https://github.com/ValveSoftware/steam-audio/blob/v4.8.1/core/src/core/simulation_manager.cpp),
[response buffer](https://github.com/ValveSoftware/steam-audio/blob/v4.8.1/core/src/core/triple_buffer.h)
and [DSP consumer](https://github.com/ValveSoftware/steam-audio/blob/v4.8.1/core/src/core/overlap_save_convolution_effect.cpp).

This still uses two synthetic tones and one procedurally defined room topology.
Imported/instanced geometry, application PCM/speech/media, perceptual quality,
continuous-motion latency and budgets, Android/IL2CPP, Quest suspension/performance
and Live echo cancellation remain integration gates. A future dry-speech path must
remain separate from the reflected-output readiness gate. Native effect reset and
voice replacement must not replay old convolution tails or consume a new stream
before its output is ready. No app schema, catalog, dependency or setting changes.


### Application PCM and utterance ownership

`-OwnedLifecycle` now requires **22 passing cases**. The isolated project copies
`Assets/Maestro/Runtime/Book/SpeechPcmStream.cs` unchanged from the app and hashes
that input in the receipt. Synthetic PCM16 packets exercise the actual bounded
24 kHz queue, ordering checks, resampling and rendered-sample receipts. This is
not generated speech, a provider session or a deployed app integration.

Each emitter can own a fresh `PcmVoice` handle. Replacement, Stop, Close and device
reset close old handles; late packets and Stop commands cannot affect a newer
utterance. Direct HRTF output and reflected output mix separately. A pending room
response suppresses only reflections. The audio consumer resets each utterance's
native history and acknowledges its serial; a subsequent simulation publishes a
response for that reset. Until then it consumes responses with zero reflected
input while the direct PCM path continues. This also avoids relying on native
convolution reset to preserve its prior IR, which it does not.

The last output boundary rechecks room validity and each voice identity. Stop
between processing and submission discards only that voice; room edits discard
only outdated reflections. Audio already handed to the device remains subject to
its ordinary buffer latency. Natural queue exhaustion feeds zeros to drain the
native tail, without adding reflected samples to the source playback cursor.
Stream completion and explicit Stop are therefore different operations.

Nine new cases cover measured stereo PCM and receipt advancement, late packets,
dry output during pending geometry, wet-only replacement and another continuing
voice, actual 24/48 kHz resets, rejected callback sizes, Close, the first silent
replacement block, edits/Stop at submission, and natural tail completion. The
boundary and natural-tail probes first suspend the fixture consumer, then invoke
the same native render method with controlled DSP times; other cases measure
Unity's real audio callback at the listener. A negative run omitting the direct
history reset failed the first-buffer silence check (sample -0.0153901838). The
corrected 22-case run closes all 37 graphs with no native validation diagnostics.
The older stochastic reflection test now checks exchanged ear levels against
unchanged orientation, instead of requiring an arbitrary absolute imbalance.

No provider credentials, microphone, headset or physical playback is used. No
shipping dependency, app renderer, schema or catalog is changed. Imported scene
integration, propagation/obstruction parity, production speech/media lifecycle,
Android/IL2CPP, sustained cost, perceptual output and Live echo handling still
require verification. That checkpoint used fixed procedural topology and two sources. The bounded
admission checks below extend the source count; world geometry remains separate.


### Shared pause and completion contract

The preceding `-OwnedLifecycle` checkpoint required **24 passing cases**. The copied app
transport now exposes explicit source-input completion and callback admission.
Finite source completion is tested through the same sealed-input cursor used by
`WorldAudio`; the candidate does not maintain a second completion queue. Pausing a
sealed source preserves queued samples while another voice and room-response
updates continue. Resume renders the remaining source at its original position.

The new submission-boundary case pauses after native DSP and verifies that an
already admitted PCM block is not discarded. Later paused callbacks are silent
and do not consume source samples. A negative run replacing admission with the
latest pause flag produces zero output after consuming that block and fails the
case. Correct source passes all 24 cases, with 39 graphs released and no native
validation diagnostics. The updated natural-tail case also asserts sealed input,
rejected later packets and exact source completion independently of the wet tail.

These are Windows candidate results. They do not replace the complete app native
suite after changing the shared source. Source and mirror verification, full
EditMode/PlayMode results and the original book/headless journeys are recorded in
`.quest-evidence/audio-stream-contract`. The renderer is still outside the app;
Android, production geometry, bounded multi-source admission, acoustic-tail
retirement, Live capture and physical Quest acceptance remain required.


### Finite tails and owner release

The preceding `-OwnedLifecycle` checkpoint required **29 passing cases**. The candidate uses the pinned
native direct, reflection and decoder tail APIs after sealed source playback.
Completion is published after the final output boundary and checked against the
output clock before the exact handle can be released with its final source
receipt. Stopped or replaced voices cannot claim completion for their successor.

Five added cases check final-block timing, completed voices consuming later
responses without replay, a continuing second voice, paused tails, replacement
with an empty open stream, actual device reset during draining, and room removal
while an obsolete tail still exists. The run `d82d2951f5f4` passes all 29 cases,
releases all 45 native graphs and reports zero native validation diagnostics.
Detailed receipts are in `.quest-evidence/audio-tail-retirement`.

Tail callbacks cannot acknowledge impulse responses they have not consumed;
silent Apply resumes that handoff once the voice has drained. Source PCM counts
do not include echoes. These contracts are detailed in
[AUDIO.md](../../AUDIO.md#natural-renderer-completion). The candidate remains
isolated; app geometry, production source budgets, Android/IL2CPP and Quest/Live
acceptance are still required.

A negative probe publishing completion before the final submission boundary
fails the new timing check (28 pass, one expected failure). Exact hashes verify
restoration of every input used by the passing run.

### Existing streams and bounded voice admission

The preceding `-OwnedLifecycle` checkpoint required **34 passing cases**. An owner accepts an existing
application `SpeechPcmStream` by exclusive transfer, preserving already queued
samples, the producer's completion flag and the same playback receipt. It does
not copy them into a second queue. A stream can be claimed only once across
candidate owners; rejecting an alias leaves its original owner intact. Fresh
input rejected for invalid settings, stale generation, unavailable output or a
full budget is closed. The caller must relinquish any previous render consumer;
this transfer API is not a hot handoff between two concurrently running backends.

Each graph has a fixed, validated capacity of 1–32 voices; the default remains two
for the original fixtures. Native effects and buffers allocate only for that
capacity. Instance IDs select exact bounded slots, and replacement handles cannot
stop or finish their successors. Immutable scene frames hold one pose per slot.
Their topology cannot change within a graph, and every slot participates in reset
acknowledgment, response refresh, completion, retirement and ownership counts.
This is renderer capacity, not a new production reservation policy.

The five additional cases cover transferred prequeued/sealed input and real
output, duplicate ownership, unavailable inputs, nine simultaneous sources,
budget refusal, reuse of the ninth slot with new reflected audio, actual Unity
audio-device reset, immutable poses and rejected topology changes. Nine sources
exercise the current app combination of eight world-audio owners plus speech;
the highest slot is checked using reflections alone. Rejected admission cannot
evict another voice, and reset closes all old streams without replay.

Run `f63ce690e507` passes all 34 cases with normal editor/wrapper exits, 53 native
graphs created and released, and zero native validation diagnostics. The 19
recorded input hashes match the verified source. Receipts are retained in
`.quest-evidence/audio-voice-admission` and the isolated run directory.

This remains a Windows evaluation. Production reservation enforcement, mutable
gain and distance/nonspatial semantics, accepted world geometry and obstruction,
Android/IL2CPP, sustained cost and Quest/Live acceptance still need integration
and verification. The app's renderer and reflection gates remain unchanged.

### Presentation controls and Unity reference measurements

`-OwnedLifecycle` now requires **41 passing cases**. Exact voice handles support
mutable gain without reopening the input or recalculating the room response.
The final submission applies gain to direct and reflected output together,
including already processed tails. Zero gain continues the source clock; Pause
retains its position. Invalid values and stale handles cannot change a successor.

Spatial settings are immutable for one playback. Direct distance falloff matches
the app's Unity logarithmic rolloff with Rolloff Scale one: a plateau inside the
minimum distance followed by inverse-distance amplitude. The pinned Unity
reference checks minima of one and two metres at distances 0, 0.5, 1, 2, 4 and 8,
with maxDistance three. It confirms attenuation continues beyond maxDistance,
consistent with [Unity's documented logarithmic behaviour](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource-maxDistance.html).
Do not treat that field as a hard audibility boundary for future region retention.
Reflected propagation already has native distance response and does not receive
a duplicate direct-distance factor. A coincident source uses a finite front
orientation rather than an undefined direction.

Nonspatial mono input uses equal-power stereo centre routing, with no HRTF or
room contribution. It retains the same queue, output clock and completion path.
A real AudioSource/mono-clip reference verifies level and equal channels before
and after listener/source/room movement. The first 41-case run exposed a
1.896875 power ratio when using the native HRTF effect with blend zero. The
[SDK describes blend zero as unspatialized](https://valvesoftware.github.io/steam-audio/doc/capi/binaural-effect.html),
but that did not reproduce Unity's mono panning level. Bypassing that effect and
using the measured centre-pan gain fixed the mismatch; reference assertions were
not weakened. The failed run `2be893bf6c21` remains retained (40 pass, one fail).

A borrowed transform binding refreshes after the avatar's LateUpdate. Parent
translation, rotation and scale therefore move the source without a manual pose
command. Unity-to-native coordinates are converted at that boundary. Unchanged
poses do not publish a new revision. Disabled/destroyed anchors stop only their
exact owner; old bindings retire without stopping replacements or destroying
the caller's hierarchy. This demonstrates the pose/lifetime contract required by
the future production adapter; it is not installed in the application.

Run `e92851831b1d` passes all 41 cases, releases all 60 native graphs, has no native
validation diagnostics and exits normally. All 22 recorded source hashes match.
The reference comparisons and failed/passing runs are retained in
`.quest-evidence/audio-presentation`. The preceding corrected run `e0f4bc3ae258`
also passed, before adding the custom near-distance comparison. All 907 current
application C# files still match the fully verified `c93a98c6` inputs.

Production renderer selection, shared reservations, canonical geometry and
obstruction, shared capture monitoring, Android/AOT and physical/performance
acceptance remain open. Production reflections and automatic map scheduling stay
disabled; these results do not resolve the retained Meta map-worker crash.
