# Native nonterminal audio completion

This candidate is outside Unity Assets and is not selected by the app. The
contract preserves the original `PcmCapture.flush()` distinction: flush leaves
the microphone owner alive; close cancels it. No device or provider is opened by
the probe.

## What completes

1. On the serialized capture worker, hand off any accepted short input via
   `CaptureInput.FlushPending`, then admit that fragment to `DuplexProcessor`.
2. `BeginFlush` snapshots the number of samples admitted to that worker. Another
   call while pending returns the same ticket and original deadline. Subsequent
   real microphone input does not move that snapshot.
3. Continue pumping the **same** input/output owners. The alignment resampler
   waits for real lookahead. Its source cursor must pass the snapshot by a full
   32-sample input kernel. The worker then processes at least the backend's
   reported fixed delay, rounded up to complete 160-sample output blocks.
4. After the original sink admits each block, acknowledge its `DuplexFrame`.
   `Ready` requires acknowledgments through the required block, in order, from
   the same owner and epoch. Producing PCM alone is insufficient. This receipt
   does not mean a provider has received or interpreted a network packet.

This conservative frame rounding can deliver newer real audio with the prefix.
It does not drop, replay, reset or pad the continuing stream. The empty prefix is
immediately ready. A new request after completion supersedes the old ticket;
resolve the old caller before beginning a new request. Tickets/receipts are
trusted internal values, not an authentication boundary.

All methods, sink admission and destruction must be serialized. The eventual
app lease bridge must recheck cancellation before a scheduled sink call; closing
this worker cannot erase a PCM copy already handed to another component.

## Fixed processing delay versus a filter tail

`maestro_echo_processing_delay` returns a conservative **160 output sample**
(10 ms at 16 kHz) fixed delay bound for this pinned APM configuration. It does
not measure device latency and does not describe infinite recursive-filter decay.
The derivation is tied to the hash-pinned WebRTC APM 2.1 source:

- `audio_processing_impl.cc::InitializeLocked` selects the processing rate from
  the minimum capture input/output rate. With supported input rates at least
  16 kHz and output at 16 kHz, processing is 16 kHz.
- `audio_buffer.cc` therefore has no split-band or output-rate conversion in the
  capture path. Its possible input conversion uses `PushSincResampler`.
- `aec3/block_framer.cc` starts with one 64-sample block of delay;
  `aec3/suppression_filter.cc` stores the second 64-sample FFT half for overlap
  with the next block. These account for 128 output samples.
- `push_sinc_resampler.cc` primes the input converter for half its 32-tap kernel.
  The bound reserves an entire kernel in **output** samples, conservatively
  covering input conversion at 32/48 kHz as well as the no-conversion 16 kHz path.
- The duplex worker separately passes a full alignment kernel in capture-source
  coordinates before applying that backend bound. This covers its centered sinc
  contribution without converting a long-running source cursor to one double.

Static assertions pin both kernel/block sizes. The actual-backend probe checks
impulse peaks at three capture rates and seven block phases. These probes detect
important regressions; they are not proof of a maximum IIR tail or arbitrary
future backend behavior. A backend/configuration change requires revisiting this
bound and these source assumptions. `capture_first` still identifies the DSP's
input source position; it is not a latency-corrected output acoustic timestamp.

High-pass and adaptive filter history remains alive across flush. Graceful
**finite EOS**, fractional final output, tail cutoff and exact effective output
sample identity need a separate policy for truly finite input. The composed live
capture owner below instead finishes using actual microphone context. Injecting zeroes with invented hardware timestamps while a microphone
is merely stalled is not that policy. Cancellation continues to discard work.

## Failure and resource bounds

A pending fence expires at one second on the next worker pump, poll, repeated
begin or valid acknowledgment. Deadline expiry faults the owner; a late receipt
cannot revive it. The event loop must keep polling; this class owns no timer or
background thread. Clock rollback, device-clock/PCM faults and queue overflow
also prevent success. Close is terminal and takes precedence over old tickets.

At most 50 processed frames (500 ms) can remain unacknowledged. Reaching that
limit faults before producing another block. Input and reference queues retain
their existing independent half-second limits. A stalled sink cannot cause an
unbounded receipt queue. Owner IDs never reuse a value within the process, even
if memory addresses or caller epochs are reused.

## Evidence and limits

`maestro_drain_probe` runs 20 scenario groups against the actual source-built
backend. It covers empty/coalesced requests, real lookahead, delayed delivery,
foreign/duplicate/out-of-order receipts, close, faults, deadline entry points,
clock failures, altered/superseded tickets, bounded outstanding delivery and
repeated flush/resume with exact PCM parity across ring wraparound. Its final
impulse matrix covers 16 fragment boundaries and three clock-skew configurations
(48 combinations), requiring the accepted impulse peak to reach the sink before
completion. The separate backend delay matrix has 21 rate/phase combinations.

Windows CI runs the contracts in all three evaluation profiles. Android builds
cross-link the same worker, probe and DSP with the existing ARM64/API32/16 KiB
checks; cross-compilation does not execute them on Quest. Existing broadband
quality gates remain mandatory, and existing tonal failures remain release
blockers. No app, human-speech or physical-headset acceptance is claimed.

## Composed live capture ownership

`CaptureSession` now owns `CaptureInput` and `DuplexProcessor` on one serialized
worker. It admits processed PCM to one synchronous sink, then acknowledges that
same frame. Its `Flush` remains nonterminal. `Finish` requests graceful shutdown:
it hands off the existing short fragment, completes the required fence using
**real context from the same still-open microphone**, then closes input and DSP.
An older pending flush is completed first; if its prefix predates the shutdown
request, exactly one additional fence covers the newer input. A one-second total
deadline bounds both phases. There is no loop that continually moves the goal.

`Close` cancels immediately without reading context or draining. This is the path
for revoked access, focus/session invalidation or an immediate stop request.
Graceful finish can deliver some newer real microphone samples while preserving
the requested prefix; it must not be presented as an instantaneous privacy stop.
A missing reference, starved device, sink refusal/exception or timeout releases
the input and reports failure instead of pretending the final word was delivered.

The sink copies the 160 PCM samples before returning. It may cancel via `Close`;
other reentrant operations are rejected. The session rechecks cancellation before
acknowledgment or another sink call. After successful finish, no more microphone
reads or sink calls occur. Playback has its own owner and is not closed here.
The original app lease/permission bridge and asynchronous JS admission still
need integration; this worker does not acquire permissions or call a provider.

Twenty-seven composed-session scenarios cover the live ownership path, final
fragments, cancellation during sink admission, reentrancy, sink/device failures,
failed construction/release, deadlines and clock errors. Both near-only and
trained-echo shutdown paths must produce exactly the same PCM as an uninterrupted
control, with only the finishing owner closing its input. The latter preserves
actual acoustic echo after the shutdown request instead of replacing it with
zeroes. This is still scripted acquisition, not physical microphone acceptance.

## Why zero-extended finite input is not the shutdown path

`maestro_termination_probe` records 72 generated cases: three capture rates,
four cutoff phases, echo/near/double-talk, and a continued or truncated speaker
reference. It runs the actual APM for eleven seconds before truncating the
microphone input, then examines output after the fixed 10 ms delay plus
0/10/20/40/100 ms. Microphone zeroes here are deliberately **not** claimed to be
physical silence in the room. This experiment is retained as a diagnostic,
not a set of successful quality cases or a newly relaxed quality threshold.

In the local measured32 run, echo-only input followed by zeroes produced peaks
around -13.4 dBFS with the reference continuing, and -14.1 dBFS even when the
reference also stopped. The latter still had roughly -20.9 dBFS peak output
beyond an additional 100 ms. Near-only output decayed much sooner. Consequently,
a tail rule chosen only from near-only impulses could emit strong residual
echo as if it were captured speech. Nor does processing arbitrarily many zeroes
make this an acceptable finite-input speech path.

Fresh source builds retain the diagnostic rows and summary separately from
quality gates. A future finite-recording backend needs an explicit termination
contract and acceptance evidence; it must not silently reuse live capture's
clock metadata, invent a timestamp for padding, or turn this rejected experiment
into a selected microphone path. The current composed owner needs real live
context and fails gracefully when it is unavailable.
