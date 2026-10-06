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

Validation: all 2,419 app tests and 100 Functions unit tests pass, along with
application/probe TypeScript, full app lint, production build, core boundaries
and prompt ownership. Local app discovery excludes only ignored .quest-evidence
diagnostic copies; committed test discovery is unchanged. No snapshots were updated.
Native C# is unchanged and the reused Editor mirror verifies its source hashes.
The managed scenario preceded a pure import narrowing in the shared command-field
table; the BYOK scenario includes it. Final release still requires paired proofs
from the selected release commit.

This document is an acceptance matrix, not a claim that it is all green. The full
managed/BYOK real-provider scenario suite, Live/observer delegation, complex program
semantics, rendered book coverage and remaining Quest release acceptance still
need their own recorded passes. The matching local BYOK run uses the owner's dedicated test key without recording
its value. The CI secret cannot be read back to a local Unity process. No release
gate is waived.
