#!/system/bin/sh
# Diagnostic package only; no persistent device properties.
export LD_PRELOAD="${0%/*}/libmaestro_storage_fault.so"
exec "$@"