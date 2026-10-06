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

This document is an acceptance matrix, not a claim that it is all green. The
passes below extend the provider suite; broader composite/animation/recovery cases,
rendered book coverage and physical Quest acceptance still need their own evidence.
The local BYOK run uses the owner's dedicated test key without recording its value.
The CI secret cannot be read back to a local Unity process. No release gate is waived.

## Event-driven program provider/native proof (2026-10-06)

`EventProgram` uses the original chat, verifier and agent with a fresh full Unity
room. Both real Gemini access modes passed:

| Access | Local evidence run | Result |
| --- | --- | --- |
| BYOK | af1df718528644bc984a018b2d14342f | All 11 program semantics and clean client/Editor exits |
| Managed staging | d59e88ac77e54dad98ba5dfbba401aa5 | Same semantics; each agent turn reconciled with zero reserved credits |

The agent first creates the contextual blue ParityBall at half standard diameter.
It then saves an editable ParitySignal program without executing it or creating
buttons/bindings. The source declares a text event, a separate colour function,
a conditional branch, a state counter, two event waits and one-second delays.
A later conversational request starts it. Two native user signals exercise the
red and fallback-blue branches, increment the counter and complete the exact run.
A manual native restart gets a new run identity and fresh state. A conversational
Stop cancels that second run; a later signal causes no effects. Source and object
identity/size/position remain unchanged. The program runs in Unity without model
polling. Native calls exercise the same handlers as controls, not physical clicks.

The save/start/stop managed windows respectively cost 106 credits/USD 0.100957
(seven usage/charge rows), 107/USD 0.104404 (five), and 121/USD 0.116902 (seven).
Each has zero remaining reservations. These windows exclude setup chat and initial
ball creation. BYOK records real model usage and the API-key owner as payer, not
an independently checked provider invoice. All three conversational tasks in both
modes passed ten handoff/result/usage gates in addition to native semantic checks.

An earlier BYOK attempt, `97b42a5ff0b640c0895836dd5cc61995`, exposed a missing
fresh-sequence reference in the flat optional-field response schema. Validation
refused it before a program was saved or started. The provider projection now
separates operations and fresh/existing saves, requiring the appropriate fields.
The shared native validator and wire contract remain unchanged. Regression tests
cover the captured invalid response and both valid save forms.

Both passes include that schema fix. BYOK generated interpreter Sleep; managed
generated the native time.wait capability. The observation gate recognizes both
scheduler representations, with focused regressions. The BYOK run passed its
stricter earlier Sleep-only gate. A later evidence-only change retains creation
accounting in the top-level result and labels the stop phase; neither changes
provider requests or native effects. Private original journals remain available.

Validation for this increment: 2,441 app tests, 100 Functions unit tests, strict
application/probe TypeScript, lint, production build, prompt ownership and core
boundaries pass. Local discovery excludes only ignored diagnostic copies under
.quest-evidence; no committed tests or snapshots were excluded/updated. Native C#
is unchanged, and each probe verified the reused Editor mirror against source.

These runs establish events, branches, functions, state, timing and lifecycle;
they do not cover parallel/modules, every trigger, restart persistence, controller
button mounting, animations, physics or rendered block editing. Those remain
separate rows in the acceptance matrix.

## Existing web provider gates (2026-10-06)

The full [managed/BYOK provider workflow](https://github.com/RONITERVO/MaestroTutor/actions/runs/37438807399)
and [release CI](https://github.com/RONITERVO/MaestroTutor/actions/runs/37438731895)
passed on `800ddca8cadb42f5538af597b4577b287361925a`. That commit predates the
EventProgram increment above; these are not claims about a later release commit.
The provider workflow includes accounting safety, both access modes and access
parity against staging, with no deployment. Both first-lesson artifacts pass all
18 coverage flags, including ten chat turns, search, attachments, streaming, STT,
Live/observer audio and vision, suggestion aftersteps, tools/uploads, translation,
TTS, capture, re-engagement and accounting. Connected Live, long observer and
long conversation jobs also passed their assertions. Managed no-output and
60-second handoff checks passed, including release/settlement checks.

The managed first lesson reconciled 468 credits/USD 0.450024 across 49 usage and
charge rows, with zero reservations. Its no-output test released all 156 reserved
credits with no usage/charge rows. The handoff test forwarded 60 of the offered
65 seconds and settled 2 credits/USD 0.001405 with zero reservations. These are
existing web/provider proofs; they do not substitute for native or headset proof.

## Library animation provider/native proof (2026-10-06)

`AvatarAnimation` extends the same conversational path to a shipped library clip.
The agent must discover the compatible downloaded Agree_Gesture motion before
saving its exact ID in an editable ParityMotion program. Saving starts no playback
and creates no buttons or automatic bindings. A later chat request starts that
saved source; the native run must complete exactly once without rewriting it.

The Editor-only probe observes actual displayed skin transforms while the provider
is busy. It does not call clip.SampleAnimation, choose a motion or drive the avatar.
The gate matches exact model, rig, motion and active run identities, checks full
13-second playback and subsequent stop, and rejects frozen joints or truncated
observations. Saved avatar placement, actual world position, walking preferences,
tutor-state choices and unrelated objects must remain unchanged. This diagnostic
is not included in the player and does not introduce a second animation system.

Both final runs use the same runtime and probe sources, including the accepted-start
fix below. Each save/start turn passes all ten conversational handoff, receipt,
chat-result and provider-usage gates, and both client and Editor exit cleanly.

| Access | Local evidence run | Observed skin playback | Save/start billing |
| --- | --- | --- | --- |
| Managed staging | dd4804e242f74b59a847ea66be4f6b16 | 120 active samples; 68 bones; 51 changing; 13.088 seconds | 137/74 credits; USD 0.134045/0.070662; 0 reserved |
| BYOK | e30b004ce83a4af1b9fda7ed9b58e171 | 121 active samples; 68 bones; 51 changing; 13.062 seconds | Real provider usage; API-key owner pays |

The managed save/start windows contain eight/five matched usage and charge rows.
Context chat and the earlier sentinel-object creation precede those windows.
BYOK payer identity is recorded; its external invoice is not independently verified.
Both providers proposed just one start in these final runs. Deterministic regression
tests separately force the duplicate proposal and prove native inspection replaces
replay. Earlier BYOK run `921a59f6a5a04ab19487e321deff9b41` passed before the shared
start fix; it is retained separately, not used as final paired-source evidence.

Earlier run `91c7c549cdfc4af4afe84a2480eb460c` remains failed. Its animation and
program completed, but the harness requested creation-only object.placement for
Maestro. The corrected harness reads supported object.definition and object.position
facts, validates availability before the next provider request, and compares saved
rotation/scale/position plus actual world position after playback. A regression
rejects that unavailable fact and changed positions/rotations. A separate post-hoc
skin check of the failed run is diagnostic only; it does not turn the run into a pass.

Managed run `1b29d02b3e8d4c51a72c825ac1f94c32` correctly remains failed: the
provider started the same saved program twice after an accepted Loading receipt.
Both native runs completed, so command acceptance alone would have missed it.
Its save/start billing windows settled 142/146 credits (USD 0.137388/0.142460),
with zero reservations before the corrected run began.

The shared task runner now remembers accepted program target/revision pairs for
that task. A duplicate play proposal becomes a normal native rules.inspect call,
recorded truthfully in the journal and charged to the query allowance. Loading,
preparing, queued, completed or cancelled starts cannot be silently reissued by
that task. Refused starts remain eligible for correction; another target/revision
or a later explicit user task can start normally. Requested repetition belongs in
the editable program. This guard does not fabricate completion or retry uncertain
effects, and does not suppress unrelated repeated creation commands. Focused tests
cover native receipts, cancellation/queueing, query exhaustion and task boundaries.
The final paired runs above include this shared fix.

Validation on this increment: 2,451 app tests in 268 files; 837 Unity EditMode and
647 PlayMode passes, with only the three documented optional private-file skips.
Full native-room journey `718b54b0748a45279e1a4c2c3b5babee` and original-book journey
`c19b054182c64c24bcda8b27d349799e` also exit cleanly. The book journey uses scripted
provider responses and is not real-provider or headset evidence. The only C#
changes here are Editor observation code; runtime animation remains unchanged.
Application/probe TypeScript, production build, full app lint, prompt ownership and
core boundaries also pass. No snapshots were updated. A test-only mock argument
type was corrected during the managed run; runtime/probe source hashes stayed fixed.

This covers one exact shipped full-body library motion. It does not certify all
178 motions, arbitrary imported rigs, live body/cloth physics, gestures, follow,
obstacle-aware travel, physical joint posing, Android WebView or Quest rendering.
Those broader acceptance requirements remain open; no release gate is waived.

## Composite module provider/native proof (2026-10-06)

CompositeModule adds a real-agent-authored program with a pinned included Passive
spinner definition, parallel branches, independently signalled construction and
native Undo/Redo. Both final access-mode runs pass. The probe checks actual geometry,
collision sources, physics settings, member identities and internal hinge frames;
it does not infer correctness from a successful play acknowledgement.

Initial BYOK run `cd8af4e105e54de69138f0400b504f7d` remains failed before composite
creation: the real planner chose RGB (0,1,1), cyan, for the preceding blue sentinel
request. The shared colour guide now distinguishes ordinary primary/secondary
names while preserving explicit RGB and qualified shades. The blue gate remains
unchanged. Independently, prior native evidence showed Unity serializes root
recipe parents as empty strings; the geometry check normalizes null/empty roots
without accepting changed nonempty parents. Focused regressions cover exact pins,
parallel child ownership, self-trigger refusal, hierarchy, finite geometry and links.

Second BYOK run `154693a3e5454d3fb5bcf137ef8b23e1` also remains failed. The blue
sentinel passed, and the agent searched/inspected the correct included module,
but its escaped sequence.program contained malformed JSON. No program save was
dispatched. The shared task runner now offers bounded local correction for JSON
syntax or rejected behaviour source, returning the validator diagnostic and prior
proposal as data in the next planning call. It uses the existing nine-call budget,
counts provider usage, and spends no action/query batch for rejected local drafts.
Unsupported actions and oversized command envelopes remain hard failures. Native
transport/receipt errors are outside this catch and never trigger replay. An empty
response after an unresolved rejection cannot masquerade as completion. Tests
exercise corrected dispatch, retained receipts, hard bounds and lost acknowledgements.

Run `fa17cb7a995047acbc90a159aa136882` reproduced the same embedded-source bracket
error during bounded corrections and reached the task stop deadline without a
program dispatch. Repeatedly regenerating immutable library code is not a reliable
module-import interface. Explicit agent drafts may now use module:null with an
exact hash inspected successfully in the same task. Shared authoring copies the
verified immutable definition before full validation, durable intent and dispatch.
The visual module editor uses the same verified-copy function. Native wire and
saved source still contain complete definitions; pins are never fetched remotely,
upgraded, or granted extra resources/signals. Stale catalog data carried by an
unrelated acknowledgement is not proof of inspection. Missing/changed pins and
combined-size/duplicate-key violations fail before effects.

Run `629b0ba444d745ef85bc1a87d4af8742` stopped on a Gemini SDK incomplete JSON stream
segment before any save proposal. It is transport-failure evidence, not a pass or
proof against the reference mechanism.

| Final access mode | Native run | Result |
| --- | --- | --- |
| Managed staging | `d614feae861546d0a706d6eaf4b3eac8` | Passed, client and Editor exit 0 |
| BYOK | `af0d435034924b65b619c9acb8867bbe` | Passed, client and Editor exit 0 |

Both use identical runtime/probe source fingerprints. Each save and start journey
passes all ten chat/verifier/handoff/context/native/result/usage gates. All 15
composite semantics pass: exact inspected pin, save without execution, editable
source, parallel native children owned by one unfinished parent, independent user
events, completed parent, exact editable geometry and placement, native collision
and physics settings, internal hinge endpoints/frames, one Undo per construction,
Redo preserving identities/connections, preserved unrelated objects and no effects
after completion. Each run records 80 native samples for both two-piece
constructions; the source and journal retain complete module definitions.

Managed save/start billing windows settled **139/78 credits** (USD
**0.134878/0.075327**), with matched usage/charge rows and zero reservations. These
windows exclude preceding context chat and sentinel-object creation. BYOK records
real usage and API-key payer identity, not an independent provider-invoice audit.

Validation: **2,466 app tests in 270 files**, app/probe TypeScript, full app lint,
production build, prompt ownership and core boundaries pass. No snapshots changed.
There are no C# changes in this increment; each probe verifies the native mirror.
Earlier 837 EditMode / 647 PlayMode results remain historical native evidence.
This covers one included construction module, not arbitrary generated geometry,
all modules, physical fidget feel, headset rendering or scanned-room acceptance.
Those broader release gates remain open.
