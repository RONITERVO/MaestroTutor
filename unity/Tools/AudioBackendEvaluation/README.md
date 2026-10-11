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

`-OwnedLifecycle` now requires **13 passing cases**, including the original six.
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
