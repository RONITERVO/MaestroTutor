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
