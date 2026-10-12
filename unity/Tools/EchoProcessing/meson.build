# App-owned evaluation only; appended to a verified private upstream copy.
echo_lib = shared_library('maestro_echo', 'EchoProcessor.cpp',
  dependencies: audio_processing_dep,
  cpp_args: common_cxxflags + ['-DMAESTRO_ECHO_BUILD'],
  gnu_symbol_visibility: 'hidden', install: false)
echo_probe = executable('maestro_echo_probe', 'Probe.cpp',
  link_with: echo_lib, install: false)
test('generated-audio-echo', echo_probe, timeout: 90)
