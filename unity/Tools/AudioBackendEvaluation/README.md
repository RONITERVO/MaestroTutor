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
Preparation runs on one worker before publishing that immutable response. Audio
callbacks hold a read lease; retirement forbids new leases and disposal waits for
existing readers. The control thread polls completion without joining a worker or
blocking the audio callback. Multiple resets coalesce into one latest request.

Six PlayMode cases pass in the isolated Windows project:

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

The six cases include several assertions from the bullets above. Teardown checks
that all 17 created graphs are closed and that native validation emits no warnings
or errors. A separate regression first reproduced the stale-error failure, then
passed after generation-aware error handling. The held-reader check deliberately
holds the same lease on the test thread; it does not stall Unity's audio device.
Actual reflected rendering and reset measurement use Unity's real audio thread.

This is a bounded lifecycle prototype, not a second production renderer. It uses
two synthetic tones and one fixed room/listener pose, with simulation completed
before DSP publication. It does not establish continuously updating simulation,
moving HRTFs, speech/media streams, perceptual gain or IR timing, device suspension,
Android/IL2CPP, Quest budgets or Live echo cancellation. Native response replacement
and shared app voice/PCM integration must retain this ownership contract before a
shipping backend decision. No app schema, catalog, dependency or setting changes.
