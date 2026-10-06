# Quest agent release coverage

Release requirement confirmed by the owner on 2026-10-06: extend the existing
headless/provider checks to the new agentic Unity paths. Old first-lesson coverage,
direct planner calls and scripted book responses alone cannot satisfy this gate.

## Required evidence

Keep four distinct kinds of evidence: deterministic contracts, actual Unity
runtime integration, real-provider journeys, and rendered book/headset acceptance.
A provider fixture is not provider evidence, Editor physics is not room-scan
alignment, and command acceptance is not completion of an animation or program.
Both managed and BYOK must pass the provider scenarios using the shared client.

| Scenario | Required observations |
| --- | --- |
| Text handoff and result | Real tutor → suggestion verifier → task; exact request/history/language/attachments; task status and result in the same chat; actual requested native state |
| Live and observer, audio and visual | Exact successfully sent audio/frames; turn identity; handoff verification; result delivery without stale speech; missing/interrupted media starts no actions |
| Capability discovery and object creation/editing | Search/inspect/check and native receipts; requested geometry/material/position; subsequent human edit, Undo/Redo and agent readback agree |
| Composite creations and reusable programs | Editable recipe/program source; branches, conditions, timers/events, state, parallel actions and modules; save/start/finish are distinguished |
| Animation and avatar work | Exact compatible asset/clip IDs, joint tracks, gestures, follow/look/walk; completion and blocked paths; no silent replacement of saved motion choices |
| Physics, contacts and room observations | Gravity/throws/collisions, scanned geometry prerequisites, permission/cancel paths; real alignment and comfort remain headset checks |
| Steering and ownership | Follow-up/revise/continue/stop; exact parent task; concurrent user grip/program ownership; no bypass of a conflict |
| Failure and recovery | Provider timeout/refusal/malformed plan; lost receipt; missing object; source/account/session change; crash/restart; duplicate request; no replay of uncertain effects |
| Accounting and access | Stage usage/model evidence, managed charges and zero stranded reservations; BYOK payer; original account entitlement and release boundaries |
| Familiar book | Chat history/pages/bookmark, artifacts/media, keyboard/microphone, foreground/background, status flag/animations; actual rendered app and Quest acceptance |

Every newly added capability needs semantic native tests and an appropriate
scenario in this matrix. Do not invent a separate headless implementation of an
action. Human controls, programs and the agent must call the same native handlers.
Use the existing shared catalog, command parser, task lifecycle and chat formatter.
The legacy web first-lesson and broader provider gates remain mandatory as well.

## Implemented in this increment

- Headless connected-room session, original text/Live input capture, verifier
  gating, shared provider/task execution, durable claims, status/result projection,
  Stop/inspection during JSON-RPC work and restart-safe display recovery.
- The optional real-provider native probe now follows the conversational path,
  accounts for all task stages and checks duplicate prevention.
- Deterministic tests exercise managed/BYOK composition, original history/files,
  synthetic/invalid handoff rejection, account/source/session/conversation loss,
  Stop, exact sent Live media and interrupted journal recovery.

## Current proof and remaining work

On 2026-10-06, the real ContextCreateEdit scenario passed against Unity in both
access modes:

| Mode | Local evidence run | Native semantic result |
| --- | --- | --- |
| Managed staging | 6985c4e0466c4bc9b8c881cabc8e9fd4 | Create, edit same ID, preserve other objects, Undo/Redo, read-only agent readback |
| Dedicated BYOK key | 8d333de629214866a57ae8093f84be4b | Same required semantic checks passed |

A context-only conversation defines a small blue ParityBall without creating
anything. A later request passes through the real tutor, verifier, planner,
native execution and final chat reply. The test validates the actual object's
name, shape, scale and colour. A follow-up paints it red while preserving identity,
position, scale and unrelated objects. Ordinary native Undo/Redo restore exact
states. A final agent request reads the current red ball without changing it.
Retrying the original claimed handoff performs no extra provider work or native edit.

The three managed agent turns reconcile separately: creation 50 credits/USD
0.047166 (five usage/charge rows), edit 95/USD 0.090625 (seven rows), and readback
43/USD 0.042005 (four rows). Each finishes with zero reservations. Those windows
exclude the earlier context-setting chat and its verifier. BYOK records actual
Gemini usage and attributes payment to the API-key owner; it makes no claim about
that owner's provider invoice. Both modes used gemini-3.8-flash. Private journals
and provider diagnostics remain in ignored local evidence.

Earlier real attempts exposed two bugs that deterministic transport tests did
not catch: Gemini rejected nested array-cardinality constraints, and the flat
optional-field schema allowed creations without a name/reference and with an
unrelated empty recipe. Bounded read-only provider probes isolated the schema
rejection. The provider projection now describes array bounds and discriminates
each action, requiring its own fields and forbidding unrelated ones. The original
native validators still enforce all bounds before execution; no invalid attempt
dispatched a room edit. Primitive creation cannot receive a recipe field.

Validation: all 2,437 app tests, 100 Functions unit tests and 31 Live gateway tests
pass, along with
application/probe TypeScript, full app lint, production build, core boundaries
and prompt ownership. Local app discovery excludes only ignored .quest-evidence
diagnostic copies; committed test discovery is unchanged. No snapshots were updated.
Native C# is unchanged and the reused Editor mirror verifies its source hashes.
The managed scenario preceded a pure import narrowing in the shared command-field
table; the BYOK scenario includes it. Final release still requires paired proofs
from the selected release commit.

## Live and observer provider/native proof (2026-10-06)

The final paired runs below use the same application and driver sources, the
ordinary Live/observer journey, the original suggestion verifier/task provider,
and a real Unity MaestroRoom. The reference speech is synthetic offline English;
the JPEG is a synthetic red reference. These are real provider calls with synthetic
inputs, not mock-provider or physical-headset evidence.

| Access | Scenario | Local evidence run | Whole Live + agent charge |
| --- | --- | --- | --- |
| managed | LiveVisual | 8416436ff09a492bb9e93140d4a73f26 | 67 / USD 0.064136; 0 reserved |
| byok | LiveVisual | 51f748e13cad4766a4fe679a84d46e9f | API-key owner; real usage recorded |
| managed | ObserverVisual | 590b5c78f10e4bf3adaab950b4daadfa | 70 / USD 0.068275; 0 reserved |
| byok | ObserverVisual | 1d7275e6dd874bb0b334839a56b954fd | API-key owner; real usage recorded |

All four runs pass 14 pipeline checks, exact client-send/journal/planner media
hashes and native semantics: ParityBall, ball, scale 0.5, red colour, no unrelated
object changes. The original spoken request carries no name, size or colour;
name/size come from prior chat and the colour agrees with the supplied image.
Every run returns the completed task to that chat and rejects duplicate execution.
The managed totals each contain five reconciled usage/charge rows. Correlated
Live charges are 9 credits/USD 0.008160 for conversation and 10/USD 0.009770 for
observer, included in the totals above. Context-setting chat precedes those windows.
BYOK proves provider usage and payer identity, not an independent provider invoice.

Earlier attempts are retained:

- `487671789ba54d038b36df4d43ce0cbd` hit a 29-second gateway cold start before the
  former 20-second client connection limit. A subsequent ledger read confirmed
  its complete 156-credit release, no usage row and zero reservations. No blind
  paid retry preceded settlement. The shared client now permits 60 seconds,
  retains server ticket/session limits, and cleans listeners after failed connects.
  Tests also preserve queued final provider/billing messages before a ready close.
- `c30ed8f7805744e2bd4da9640e11d5de` and
  `3a536529eb9246bdb0b628a0ee858ff3` completed room work but exposed spoken tool
  JSON. Task-catalog instructions now distinguish speech from text in both web
  and headless hosts. The new speech-format gate rejects those historical outputs.
- `4b4e3c52f77944ae83a129636c9487ba` created the correct named red ball at default
  scale, but failed an ambiguous “small” expectation. Standard primitives are
  already 13 cm. The final scenario explicitly asks for half the standard diameter
  and checks scale 0.5; it does not relax the assertion or silently call the old
  run passed.

Live completion also refuses changed conversation/history/access/native scope
before persistence or task dispatch. It does not claim to abort ongoing provider
consumption immediately. Source changes, both host prompt modes, sent-media
preservation, speech syntax and gateway timeout/close races have deterministic tests.
The offline fixture generator reproduced the exact 181,440 input samples and
PCM SHA-256 used by these runs. Private journals remain ignored local evidence.

These passes cover delegated work after a Live turn. Direct native Live function
calls, actual microphone/camera/Whisper capture, final task speech, rendered book
and broad program/animation/recovery scenarios remain separate release gates.

This document is an acceptance matrix, not a claim that it is all green. The full
managed/BYOK real-provider scenario suite, broader Live/observer cases, complex program
semantics, rendered book coverage and remaining Quest release acceptance still
need their own recorded passes. The matching local BYOK run uses the owner's dedicated test key without recording
its value. The CI secret cannot be read back to a local Unity process. No release
gate is waived.
