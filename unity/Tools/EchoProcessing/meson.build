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
capture_lib = static_library('maestro_capture', ['CaptureInput.cpp', 'AndroidCaptureDevice.cpp'],
  dependencies: capture_deps, install: false)
capture_probe = executable('maestro_capture_probe', 'CaptureProbe.cpp',
  link_with: capture_lib, dependencies: capture_deps, install: false)
test('scripted-capture-ownership', capture_probe, timeout: 30)
