# App-owned evaluation only; appended to a verified private upstream copy.
echo_flags = common_cxxflags + ['-DMAESTRO_ECHO_BUILD']
if get_option('maestro_echo_profile') == 'stationary32'
  echo_flags += ['-DMAESTRO_ECHO_STATIONARY_EVALUATION', '-DWEBRTC_APM_DEBUG_DUMP=0']
endif
if get_option('maestro_echo_profile') == 'measured32'
  echo_flags += ['-DMAESTRO_ECHO_MEASURED_EVALUATION', '-DWEBRTC_APM_DEBUG_DUMP=0']
endif
probe_flags = get_option('maestro_echo_profile') == 'measured32' ? ['-DMAESTRO_ECHO_MEASURED_EVALUATION'] : []
echo_lib = shared_library('maestro_echo', 'EchoProcessor.cpp',
  dependencies: audio_processing_dep,
  cpp_args: echo_flags,
  gnu_symbol_visibility: 'hidden', install: false)
echo_probe = executable('maestro_echo_probe', 'Probe.cpp',
  link_with: echo_lib, cpp_args: probe_flags, install: false)
test('generated-audio-echo', echo_probe, timeout: 90)

# Separate input ownership/timestamp boundary; no app integration or microphone
# acquisition occurs when running its scripted probe.
capture_deps = []
if host_machine.system() == 'android'
  capture_deps += meson.get_compiler('cpp').find_library('aaudio')
endif
capture_lib = static_library('maestro_capture', ['CaptureInput.cpp', 'AndroidCaptureDevice.cpp', 'CaptureEffects.cpp'],
  dependencies: capture_deps, install: false)
capture_probe = executable('maestro_capture_probe', 'CaptureProbe.cpp',
  link_with: capture_lib, dependencies: capture_deps, install: false)
test('scripted-capture-ownership', capture_probe, timeout: 30)

playback_deps = capture_deps + [dependency('threads')]
playback_lib = static_library('maestro_playback', ['PlaybackOutput.cpp', 'AndroidPlaybackDevice.cpp'],
  dependencies: playback_deps, install: false)
playback_probe = executable('maestro_playback_probe', 'PlaybackProbe.cpp',
  link_with: playback_lib, dependencies: playback_deps, install: false)
test('scripted-playback-ownership', playback_probe, timeout: 30)

# Worker-only clock fitting/resampling plus the existing APM ABI. No device opens.
alignment_lib = static_library('maestro_alignment', ['ClockAlignment.cpp', 'DuplexProcessor.cpp'],
  dependencies: audio_processing_dep, cpp_args: common_cxxflags, link_with: echo_lib, install: false)
alignment_probe = executable('maestro_alignment_probe', 'AlignmentProbe.cpp',
  link_with: [alignment_lib, echo_lib], install: false)
test('clock-aligned-duplex', alignment_probe, timeout: 30)

# Same actual APM/clock/resampler path; generated input and scripted sink receipts.
drain_probe = executable('maestro_drain_probe', 'DrainProbe.cpp',
  link_with: [alignment_lib, echo_lib], install: false)
test('nonterminal-duplex-drain', drain_probe, timeout: 30)

# Composed input/DSP/sink lifecycle. Permission and application leases stay above it.
session_lib = static_library('maestro_capture_session', 'CaptureSession.cpp',
  link_with: [capture_lib, alignment_lib, echo_lib], install: false)
session_probe = executable('maestro_session_probe', 'SessionProbe.cpp',
  link_with: [session_lib, capture_lib, alignment_lib, echo_lib], install: false)
test('scripted-capture-session', session_probe, timeout: 30)

# Characterization only: intentionally zero-extended finite input is NOT the
# selected graceful-shutdown path. Do not count these outcomes as quality passes.
termination_probe = executable('maestro_termination_probe', 'TerminationProbe.cpp',
  link_with: echo_lib, install: false)


# Reference-without-acoustic-echo and causal history controls. The observations
# remain separate from the unchanged acceptance matrices and never certify audio.
history_probe = executable('maestro_history_probe', 'HistoryProbe.cpp',
  link_with: [alignment_lib, echo_lib], install: false)
