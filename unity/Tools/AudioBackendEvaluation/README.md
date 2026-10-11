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
