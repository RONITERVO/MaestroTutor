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


## Real-provider rendered book proof (2026-10-06)

The book provider runner passes in both access modes on the final sources. It
uses the ordinary book chat controls and browser provider path against the real
Unity room, with isolated test credential setup. It checks earlier context,
verified delegation, native creation/editing, visible status/result, continued
chat access, workshop Undo/Redo and persisted task reload without replay.

Initial BYOK run `b6bc556f5b1e4b2bb0814288048c02cb` created the correct object and
completed the task, but failed reply equality because the Chrome DevTools response
reader misdecoded non-ASCII SSE text. The app's reply was correct. Observation now
uses a Fetch clone's UTF-8 text, preserving the original response. Run
`31e7c09347f847ecb4779bcfbf4bb7bd` then failed before a provider request: tsx's
function-name helper was unavailable inside the serialized browser init script.
The observer now uses an explicit isolated script. Neither failed run is a pass.

Managed attempt `cb2d96b6ef5a40629bb75c02763cdc34` failed at the first browser
request because staging correctly rejected the random local origin. Billing
reconciled zero usage/charges and zero reservations. The runner now serves its
isolated fixture from the existing trusted `http://localhost` origin; the backend
returns the ordinary CORS headers without a policy change or security bypass.

Before the result-reply fix, managed run `e16565c562bb4c3da6fff19205189c35` passed all ten book gates, with
14 real provider responses, matching task/native observations and clean client /
Editor exits. Whole-journey accounting settled **201 credits / USD 0.192848**,
with 14 matched usage/charge entries and zero reservations. This window includes
the initial context chat and both delegated tasks.

BYOK run `7f87a5467b744b2db1fbd3016b5df136` remains failed and exposed an app
issue: the native repaint completed, but the final provider response announced
another future handoff and included an agent tool fence. The existing visible-text
parser hid the fence, so the inaccurate future promise would have reached chat.
No native action was repeated.

The shared browser/headless reply stage now asks for a report of the already
recorded result. It retains the exact original request as quoted context instead
of issuing it again as the current instruction. Native receipts, scene, history,
language, attachments and task-limit/uncertainty information remain available.
A tool fence is rejected before publication, even if malformed. One bounded
correction may regenerate only the reply text; both calls count usage. Repeated
tool proposals fail visibly with recorded actions retained. Transport errors and
cancellation do not trigger this correction, and native planning/dispatch cannot
run from this path. Focused regressions force these conditions. The book gate now
compares the normal parsed visible text and separately refuses a final tool fence.


| Final access mode, including reply fix | Native run | Real provider responses |
| --- | --- | --- |
| BYOK | `05f53e53a7734385b9dfbf8a167f0c75` | 12 |
| Managed staging | `aedda976a985447096ac91e9f975b1db` | 14 |

Both pass all ten book gates and exit the client and Editor cleanly on identical
source fingerprints. Screenshots show working/completed tasks and the reloaded
result; original network output, task journals and native observations agree.
Final replies report the actual creation/repaint and contain no new tool fence.
These real runs need no reply correction; deterministic regressions force it.
Managed accounting for the complete final journey settled **207 credits /
USD 0.201414**, with 14 matching usage/charge entries and zero reservations.
BYOK records actual provider usage and payer identity without claiming invoice
verification. The earlier managed 201-credit pass remains historical evidence.

Validation: **2,472 app tests in 272 files**, app/probe TypeScript, full app lint,
production build, prompt ownership and core boundaries pass. No snapshots changed.
The test credential/observation bindings are absent from the production bundle.
No native C# changes are part of this increment; the mirror is hash-checked.
This proves the rendered book's basic conversational tasks. Broader artifacts,
page/bookmark and media flows, physical input, Android texture, Quest rendering,
sign-in/attestation and the other outstanding release gates still need their
separate acceptance evidence.

The original offline-provider book regression also passes on these sources: run
`9a03bda67fa6484589e0a069a1da12fa`, client and Editor exit 0. It keeps
concurrent human edits, stale-plan refusal, manual authoring and persisted reload
covered independently of the real-provider happy path. It does not count as a
real-provider run.

## Real-provider native physics proof (2026-10-06)

`PhysicsLaunch` now passes in both modes against actual Unity physics, through
the original chat, verifier and room-agent path. Explicit fixture setup positions
the contextual half-size ParityBall, configures its spherical body and creates a
fixed wall beside the synthetic Editor floor. These setup edits are not agent
actions or a real room scan. Three conversational tasks start physics, inspect
and perform one aimed throw, and pause physics.

| Final access mode | Native run | Real provider responses | Peak height |
| --- | --- | --- | --- |
| BYOK | `e0f6abbdfe3b42e7a13a4bb1158a354d` | 31 | 0.891657 m |
| Managed staging | `bd270b26839a43a28db8ba655875c81e` | 29 | 0.891037 m |

Both runs pass twelve semantic gates and all ten pipeline gates for each of the
three tasks, with clean client and Editor exits and matching source fingerprints.
Native live-transform samples show gravity, ballistic rise/fall, approach to the
requested destination, floor support, wall contact without penetration, and
settling. The final sphere centre is 0.0325 m above the floor, matching its radius.
Identity/settings and unrelated objects are preserved. Reusing the exact launch
receipt does not throw again; the provider's subsequent pause disables simulation
and leaves the settled body still. This does not test braking a moving body.

Managed task windows reconcile separately: start **113 credits / USD 0.109800**
(7 usage/charge rows), launch **130 / USD 0.126740** (8 rows), and pause
**121 / USD 0.116879** (7 rows). All end with zero reserved credits. These windows
exclude the earlier context chat and object-creation task. Response counts in the
table include that earlier work. BYOK records real provider usage and its payer,
without claiming independent invoice verification.

Two preceding failures remain recorded. Run
`f263071ed98643f38f0a2e395375fdc9` started gravity, then emitted incorrect nested
coordinates which native validation refused, followed by malformed planner JSON.
No throw occurred. Open-ended catalog arguments now use JSON text only in the
provider response schema; the shared decoder restores typed objects before the
unchanged validation, durable intent and native dispatch. Only inspection/check/
execution argument payloads use this encoding. Programs, catalog definitions,
native commands and human editing retain their existing formats. Malformed JSON
may use the existing bounded pre-dispatch correction; oversized or non-object
payloads fail, and no uncertain native action is replayed.

Run `a719511a8a3d496a9a44d46a19689535` then performed the correct physical throw
but failed settling when the rolling ball left the finite synthetic floor. The
fixture now authors a fixed wall and asserts contact with it. App physics and
collision tolerances were not changed to hide that failure.

Validation: **2,479 app tests in 274 files**, application/probe TypeScript, full
app lint, production build, prompt ownership and core boundaries pass. The first
broad local test discovery also found an ignored diagnostic harness without its
private Express dependency; its failure is retained. The passing rerun excludes
only `.quest-evidence`, with committed test discovery unchanged. No snapshots or
C# sources were changed. Each native probe verifies the Editor mirror's sources.
Only trailing blank-line cleanup and import ordering followed these provider runs.

This proves one sphere/trajectory against a synthetic floor and fixed wall. Real
room scan permissions/alignment, controller throwing/catching, arbitrary imported
colliders, contact-driven user programs, headset rendering and Quest performance
remain separate gates. Earlier scenario passes are historical where shared
planner sources have since changed; the selected release still requires its
final paired provider and physical acceptance evidence.

### Follow-up on the argument-encoding sources

The rendered-book regression passes again on both access paths after the shared
planner change: BYOK `eeabb4227e3b4da9beb1b1e00510c919` and managed
`9674aaff8ff24c9b9d291ab2e08293d0`. Each passes all ten gates, makes 14 real
provider calls and exits both client and Editor cleanly. Reloaded screenshots
show the recorded result in the original chat. Managed whole-journey billing
reconciles **195 credits / USD 0.189442**, with 14 usage/charge rows and zero
reservations. This remains desktop browser/Unity evidence, not Android sign-in
or headset acceptance.

The construction-module regression also passes on both paths: BYOK
`7a209693f04e4b95ab61f6d08829b336` and managed
`59f8056b497c4ad09f4daed2138ffb30`, each with 19 real provider responses,
all fifteen semantic gates, both ten-gate save/start journeys and clean exits.
Exact module discovery/pinning, editable source, two independently triggered
parallel branches, native geometry/hinges, atomic Undo/Redo and no effects after
completion remain verified. Managed save/start billing reconciles respectively
**158 / USD 0.153682** (7 rows) and **76 / USD 0.073026** (5 rows), each with
zero reservations; earlier context/creation is outside those two windows.

Default Vitest discovery now inherits the existing Vite configuration and normal
Vitest exclusions, adding only the ignored `.quest-evidence` directory. Running
the ordinary test command passes the same **2,479 cases in 274 files**, without
a special local exclusion flag. No committed tests or snapshots were removed.
The separate configuration passes TypeScript checking. An attempted project-build
check encountered the existing node-config theme include mismatch; its generated
files were removed and that original config restored. No application fix or pass
is claimed for that separate build mode.

## Real-provider task steering proof (2026-10-06)

`TaskSteering` passes against actual Unity in both access modes. A real contextual
creation task is followed by a paint/inspect/resize request. Immediately after the
native paint acknowledgement, the probe invokes the public `room.stop` control
and makes manual-handler edits to colour and placement. It returns the unchanged
paint receipt to the runner. The stopped task retains that confirmed operation,
never performs the unfinished resize, and projects its stopped status into the
original chat state without generating a result reply.

| Access mode | Native run | Real provider responses |
| --- | --- | --- |
| BYOK | `90427aa74ab74e18ba17ab4328863828` | 21 |
| Managed staging | `9aa542b2b5794467953b50c91b78d458` | 21 |

Both runs pass fifteen semantic gates and exit client and Editor cleanly. Real
conversational Revise and Continue requests resolve to their exact parent tasks,
retain the original request/receipt chain, and stay in the same room, owner and
conversation. Revision paints the same object blue while preserving manual
placement and the original size. The continuation inspects without editing. A
fresh host/store restores the records and chat statuses; durable claims and the
old handoffs cannot replay work or consume more provider usage.

The positive journey checker intentionally rejects the stopped task's completion,
reply and reply-usage gates. Its other seven gates pass; planning usage is present
and result-reply usage absent. Revised and continued tasks pass all ten gates.
Managed billing reconciles separately for stop **32 credits / USD 0.031611**
(3 usage/charge rows), revise **143 / USD 0.140431** (7 rows), and continue
**66 / USD 0.062922** (4 rows), all with zero reserved credits. These billing
windows exclude the earlier context/creation work included in the response counts.
BYOK confirms recorded provider usage and payer, not an independent invoice.

Validation: **2,481 app tests in 275 files**, including 74 focused task/provider/
projection tests, and probe TypeScript pass. The two new evidence-gate tests reject
extra effects, uncertain receipts and incorrect task ancestry. No app runtime,
C# sources or snapshots changed in this increment. Provider runs used matching
source fingerprints; afterward only the semantic flag `visibleStoppedStatus`
was renamed to `stoppedStatusInChatState` and its wording clarified, with no
assertion change. The original run evidence retains its original flag name.

This is a deterministic acknowledgement-boundary Stop and ordinary native edit
handlers. It does not establish physical grip arbitration, spoken Stop latency,
rendered Stop controls/status, process-crash recovery, lost acknowledgements,
Android reload or headset acceptance. Those remain separate release gates.


## Adaptive novice lesson findings (2026-10-06)

The requested English-native / Spanish-target novice dialogue ran through the
shared headless chat, verifier, tools, room agent and actual Unity. User messages
contained ordinary learner requests, not catalog IDs, coordinates, schemas or
native solutions. The developer adapted follow-ups to the actual replies.

Baseline managed run `3b23832e3b01431fa237fd4778cc0882` and BYOK run
`8454b9153dfa47b1a6d9feb18d08276c` collected the full text sequence: greeting,
flashcards, cafe image, tree/table/apple, current weather, walking, wagging dog,
robot, Newton/catch and guidance to enter Live. Proactive music and weather
artifacts are intended product behavior, including complementary artifact/tool
outputs; their mere presence is not a failure. Exported flashcards and the
weather notebook were exercised in isolated desktop Chrome, and PNGs inspected.
This does not prove interaction with those artifacts on the book texture.

The baselines are **not semantic passes**. The initial real-table placement did
not create the requested objects; a natural follow-up requesting a virtual table
created them. Weather responses initially assumed an unsupplied location. After
the persona supplied Helsinki, the reported temperature matched an independent
FMI check. Walking and catch never ran: physics was paused and no real-room
alignment was established. Dog recipe tracks and robot animation were saved and
marked playing, but their appearance/motion was not observed on a headset. Live
navigation advice named generic controls rather than establishing the real UI.

BYOK Live understood all 34 requested words and removed the exact tree while
retaining the same apple and named dog/robot/table. A strict all-object comparison
then found a second batch deleting three unrelated starter primitives. Managed
Live, resumed from the untouched saved room as
`292a987d4a6245fdbe2923cdc8cf46ae`, reproduced these extra deletions. Both are
failures despite successful native receipts and fluent final replies. The first
managed Live attempt was blocked by the staging daily allowance; the owner-
authorized increase is recorded in LIVE_TURN_SPENDING_RELEASE.md. No previous
native command was replayed during restore.

The shared planner now receives its exact dispatched commands, paired by index
with the native receipts already supplied. Previously it received only resulting
room snapshots, which omitted deleted targets. The same pairing reaches the
result narrator. Rejected receipts remain failures; runtime start receipts still
do not prove completed motion. Generic assembly guidance distinguishes one
recipe's internal parts from independent top-level objects. This changes neither
proactive tutor outputs nor the native command/permission contract.

Managed regression `782a61fe9fee4d9882c0d102582e70ba` restores the original room
and repeats the identical spoken request with this fix. One acknowledged batch
deletes only the tree and places the same apple at y=0.04 m (its radius is 0.039 m).
All other object identities, placements, colours and physics settings remain
unchanged. The five usage/charge rows reconcile to **86 credits / USD 0.083598**,
with zero reservations. The failed managed baseline Live cost **121 credits /
USD 0.118723**; the earlier text lesson cost **649 / USD 0.631325**. These are
separate windows, not a total of every development attempt. BYOK records provider
usage and payer, not an independently reconciled invoice.

The focused shared tests pass **102 cases**, and the full app suite passes
**2,483 cases in 275 files**, plus application and integration-driver TypeScript.
Unit tests verify command/receipt pairing through deletion, rejected edits and
queries, retained immutable evidence and final-reply delivery; they do not assert
that a mocked model made a correct semantic decision. Original failed evidence
is retained. The real-headset lesson remains open. All Editor runs use a synthetic floor and synthetic paced speech: they do
not prove real scan alignment, floor contact, microphone capture, headset pixels,
first-time account registration or physical input. The whole lesson remains
incomplete until those outcomes and the observed novice friction are resolved.


Fresh focused BYOK regression `b20568a8b0774530a2d068d58853cfa1` creates its tree,
table, apple, dog and robot through two ordinary learner chat turns, then switches
to real Live. With command/receipt pairing, its single edit batch deletes only the
tree; every unrelated object and the same apple's properties are preserved.
However, the apple center was placed at y=0, intersecting the synthetic floor by
its 0.052 m radius. This remains a **partial result, not a full semantic pass**.
A subsequent shared API-contract clarification explains primitive center origins
and prefers native surface placement or inspected geometry/bounds over guessed
floor coordinates. It also makes explicit that completing a deletion stops further
deletion, not other unfinished requested actions. These two wording clarifications
postdate the paired provider runs; their provider/device acceptance remains open.
The Editor-only restore guard also retains rejection of conflicting files or
response directories. Original runs and source fingerprints remain private evidence.

## Novice Quest lesson: partial device evidence (2026-10-06)

The same English-native / Spanish-target persona has now begun a fresh lesson on
Quest 3 using the installed development APK (SHA-256 prefix 4997B47A4620).
BYOK was configured through the visible application UI. Managed access was not
offered by that headset build; production Quest bootstrap remains disabled.
Neither managed headset acceptance nor first-time account registration is claimed.

Ordinary composer text was entered through the WebView, then submitted through
simulated Quest controller input routed to the real book collider and page.
No tool schemas, object IDs, coordinates or native implementation hints were sent
as learner messages. Actual stereo headset captures, browser DOM/images, native
task receipts and room state were collected separately. This is device automation,
not physical-controller ergonomics or human microphone coverage.

The greeting returned bilingual help and a proactive notebook. A five-card game
responded to controller-driven flips and Next controls, and a cafe image was
generated and inspected in the WebView. Spanish-only game controls were awkward
for the beginner. Opening the game first did nothing while the session was
inactive, requiring the existing physical resume bell. Language confirmation
also reset the selected pair; the existing idle-confirm path eventually committed
English/Spanish. These two application defects now have focused regression tests:
Confirm commits the chosen pair, and deliberately opening an inactive game resumes
the session if foreground and media-shutdown gates permit it.

The first real-table request correctly created nothing without room access.
A natural help request opened Quest's spatial-data permission dialog, which the
user allowed. The task stopped on application interruption, and its native
permission deadline expired while waiting for that manual response. No command
was replayed automatically. Asking to carry on did not recover the intent; a
complete natural restatement was needed. The next request created an independent
tree, virtual table and apple in one acknowledged batch. No actual scanned-table
alignment, apple contact or corner placement was established. Weather used Finland
without a supplied city; actual local weather remains unverified.

A later actual stereo capture exposed **stale book pixels** after the permission
dialog even though the browser DOM had advanced. Native diagnostics reported
contentExists=false; DOM screenshots after that interruption are not proof of
the user-visible book. Android resume now schedules renderer recovery without
requiring a resize callback. Four fragment-lifecycle regression tests cover fresh
frames, cancelled recovery and duplicate/initial surface callbacks; two fail
against the old implementation and pass with the fix.

Current source validation is **2,488 web tests in 276 files**, application
TypeScript and focused lint, plus **86 Android tests passed / two optional
private-import tests skipped**, release AAR build and lint. The language tests
also fail against the original handler. Updated APK and actual book-texture
acceptance remain separate from these test results.

The lesson and app data were backed up before stopping at 22% battery for charging.
Walking, wagging dog, robot, catch and Live have not yet run in this headset lesson.
Real-room physics, rendered motion, actual microphone audio and preservation of
the same apple through Live remain open. Original failures and private room/chat
evidence are retained outside version control; the lesson is not a semantic pass.


### Wireless headset continuation and generated-image provenance (2026-10-07)

The owner supplied a wireless ADB endpoint, and the same Quest 3 was verified
before installation. Development APK `211B99FF` (source `dad2ba62`) preserves the
saved lesson. The headset remained connected to its charger at about 53%.
Actual stereo captures show new book content after opening and returning from
Android Settings, with the same app process and advancing frame-copy counters.
This verifies a real focus interruption; the exact spatial-permission dialog
resume still needs a separate rerun. A native controller tap on **Resume and play**
opened the weather activity without touching the audio bell.

A natural setup-help turn exposed an image-origin defect: the tutor treated its
AI-camera illustration as a real view, claimed a green alignment check and named
nonexistent pictured buttons. Its delegated task performed zero native actions.
A subsequent plain-language clarification actually loaded and displayed the
scanned room. The owner then confirmed that the surfaces lined up with the real
room. The next request started physics and hid the overlays. However, it used
an instant `move` for Maestro and narrated this as walking; walking is not a pass.

The dog and robot were created. The dog has a running rotation track, but native
samples and recipe playback semantics show that its cylindrical tail rotates
about its length axis (unchanged center and longitudinal direction), not a visible
side-to-side wag. The first catch request performed no actions; its follow-up
inspected hold/launch/catch capabilities but still created no program or throw.
The same apple remained on the virtual table. Further catch and Live testing
continues; this device lesson remains a **semantic failure**, not release acceptance.

Generated user/assistant images now persist explicit provenance independently of
upload/compression variants, including failed uploads and subsequent reuploads.
The shared provider boundary labels the corresponding image parts for chat and
room-agent planning/results. Live and suggestion context retain the distinction.
Headless file inputs retain the same provenance. Images and proactive artifacts
remain enabled; filenames alone are not treated as evidence of origin. Old
unmarked user images remain of unknown origin.

Current validation: **2,494 web tests in 277 files**, application TypeScript,
focused lint, prompt ownership and runtime-boundary checks pass. Both managed and
BYOK transport regressions cover the tutor-to-agent handoff and preserve original
user words while keeping application metadata out of provider fileData fields.
One owner-approved **real BYOK Gemini 3.8 Flash** replay of the failed setup turn
with its current illustration labelled no longer asserted a visible green check
or named the imaginary controls; it proposed a room-agent handoff. This is one
captured-input replay, not a complete provider or headset acceptance run.
The provenance change is not yet installed on Quest.

The preceding development APK completed **837 EditMode / 647 PlayMode tests**,
three optional private-file skips, **86 Android tests / two optional skips**, and
both deterministic native headless and rendered-book integration probes.
Release CI [37522598096](https://github.com/RONITERVO/MaestroTutor/actions/runs/37522598096)
passed on `dad2ba62`; these native results precede the web-only provenance change.
Private conversation/room pixels, original failures and the replay remain outside
version control.


### Microphone-only Live entry (2026-10-07)

The next catch request checked two trajectories, both blocked by the scanned
room or objects. No throw ran; the same apple remained on the table. The next
natural request to switch to Live incorrectly described a floating Live button.
Actual book inspection found that the Live button existed only in a working
camera preview. The public Live-start controller also required video, despite
the shared audio pipeline supporting microphone-only input. The current Quest
APK intentionally lacked physical-camera permission, making this an application
blocker rather than a Gemini transport failure.

The shared composer now offers Start/Stop/Retry Live when no camera preview is
shown. It reuses an enabled camera but does not implicitly request one; audio-only
status does not claim Maestro can see the learner. A deliberate start can resume
an idle foreground lesson while respecting native suspension/shutdown guards.
Draft text and the existing media-preview controls remain intact.

Validation: **2,500 web tests in 277 files**, application TypeScript and focused
lint pass. The focused 41 tests include microphone-only start/restart/stop,
retry/error visibility, duplicate-start suppression and suspended-session gating.
Release CI [37532033410](https://github.com/RONITERVO/MaestroTutor/actions/runs/37532033410)
passed the preceding image-provenance commit. A new development APK is being
verified; physical microphone and final Live object-preservation acceptance
remain open. The lesson and room have a fresh verified local backup.

The owner also clarified that v1 must support separately selectable physical,
mixed-reality and virtual-only views. The integration/acceptance requirements
are recorded in QUEST_V1_PLAN.md; this microphone fix does not implement them.

### Scanned-floor recovery and novice provider checks (2026-10-07)

The physical Live continuation removed only the requested tree and preserved the
apple and other objects, but its live surface ray missed. A subsequent request
inspected four facts without loading the unavailable scan or moving the apple.
Its reply described the earlier failed placement as a new attempt. Neither turn
passes the complete placement requirement. The prior microphone/depth commit
`9957953a` passed [release CI 37545778212](https://github.com/RONITERVO/MaestroTutor/actions/runs/37545778212).

The shared catalog now exposes `object.scan.place` for an exact inspected upward
scan plane. The real native handler fits collision bounds, saves through normal
object editing, and preserves Undo, temporary-room and ownership semantics. See
[surface placement](QUEST_SURFACE_PLACEMENT.md). Planner guidance distinguishes
the requested floor from a live ray's first hit, and permits loading an available
saved room once as a prerequisite. It requires a fresh setup observation rather
than treating an initial permission-stage acknowledgement as a still-open prompt.
Result narration must identify this task's actual commands; the initial Live
reply cannot claim results before the delegated task executes.

Per-task discovery limits are now 18 planning calls and 12 read batches; action
batches remain limited to three. The earlier six-read ceiling was exhausted by
setup, scan inspection and capability discovery. The higher ceiling costs more
time/tokens when used; it is bounded, not a success guarantee. Regression tests
retain rejection before dispatch/journaling and cancellation at budget boundaries.

Real Gemini 3.8 Flash checks used the ordinary English-to-Spanish tutor, verifier,
agent and actual Unity runtime, with fresh learning history. The learner asked
to put the existing ball on the floor and explain "ball" in Spanish; no native
IDs, schema names or coordinates were supplied in the learner request.

| Access | Evidence | Observed result |
| --- | --- | --- |
| BYOK | `94b5065ae5404f0d93073d27a74deaf3` | Initial request failed under the earlier 9/6/3 limits. A plain-language follow-up discovered exact scanned placement and moved the same ball onto the floor. The other four objects stayed unchanged. |
| Managed staging | `482d5939407a4677829fd430f6f3956e` | Fresh request under 18/12/3 limits loaded once, refreshed scan state, inspected the floor and placed the ball. One incorrect fact argument was rejected and corrected before placement. No follow-up or extra physics start was needed; other objects stayed unchanged. |

Both successful placements returned completed native receipts and the expected
collision support height. Managed accounting reconciled 17 usage/charge entries,
465 credits / USD 0.454757, with no remaining reservation. Earlier failed runs
are retained too: BYOK mistook a historical permission acknowledgement for a
current prompt; the first managed attempt ended before placement with a generic
task error whose rejected proposal was not retained. The learner driver now
records bounded provider response text for diagnosis, excluding request media
and credentials. No full-lesson acceptance is inferred from these focused runs.

`Run-QuestRoomProbe.ps1 -ProviderScenario LearnerConversation -SyntheticRoomScan`
explicitly replaces only the Editor platform scan/permission boundary with a
known floor, initially unloaded. The native setup state machine, catalog, planner,
placement, storage and receipts remain real. This fixture does not test an OS
permission dialog, real room alignment or physical input. The option is rejected
for other scenarios. Private conversations and diagnostics stay outside Git.

Validation: **2,503 web tests in 278 files**, **839 EditMode / 651 PlayMode tests**,
with three optional private-import skips. TypeScript and catalog provenance pass.
Both deterministic native integration journeys pass: headless
`3b715b2da041492d914a5284b805cccb` and rendered book
`d43ec35228f14136a3e60ebb2908053b`. The latter exhausts all 12 discovery reads,
still creates the object, rejects a stale agent paint after a human edit, shows
the native receipt history and reloads without replay. Its earlier fixed-count
test assertions were updated to the shared limit; provider replies remain
explicitly scripted for this book regression, separate from the paid checks above.
The new floor action and Live narration guidance still require completed headset
acceptance. The subsequent device attempt is recorded below.

## Interrupted planning stream on Quest — 2026-10-07

Development APK **D85F3CB7** (`f58dab48`) was installed over the existing lesson,
after verifying private/external backups, all 148 packaged web files, ARM64/v2
signature and 16 KiB alignment. Android verification passed 87 tests with two
optional private-import skips; the Unity counts above are unchanged. The saved
room was equal before/after reload. This is separate from the older Alpha build.

The BYOK learner's natural floor-placement follow-up produced a truthful pending
handoff, then two read-only room queries. The task failed before native placement.
The app's existing Traffic Log identified `Incomplete JSON segment at the end`.
The installed Google SDK throws this at SSE EOF with an unfinished frame; this
is transport framing, not invalid model-generated command JSON. Traffic Log
inspection was developer diagnosis through the existing UI, not user-flow proof.
Private bounded response/error evidence remains outside Git.

A second ordinary follow-up loaded the real scan and inspected its FLOOR anchor.
It was interrupted deliberately when the device reported 8% battery, discharging,
and 56°C. The app was stopped and the device put to sleep; owned ADB forwards
were removed. Saved room/receipt readback shows all nine objects unchanged,
the same apple still on the table, and no `object.scan.place` receipt. This is
neither a placement pass nor a diagnosis of what caused the heat. Sustained
performance/thermal acceptance remains open; device work waits for cooling and
normal charging above 40%. See [device QA](QUEST_DEVICE_QA.md).

The shared Gemini response path now permits **one** retry of that exact SDK
framing error, with a one-second cancellable delay and the same model/request.
The allowance is shared across search/model fallbacks and stays within the
existing total request-attempt ceiling. It applies only before any text or
thought callback has exposed output. A private incomplete plan is discarded;
partial visible output, ordinary JSON syntax errors and coded service errors
are not reclassified as this transport failure. It does not restart the room
task, resend a native command or replay an uncertain receipt. Progress reports
retrying, not high demand or a model switch. A failed attempt may still incur
ordinary provider charges; recovery does not imply that its usage was free.

Deterministic regressions run through both access adapters and through the real
installed SDK with a synthetic truncated SSE response (no network). Room-agent
tests preserve a prior completed action and receipt across a broken subsequent
planning stream, including a complete-looking command before the failed EOF.
Stop, retry exhaustion, visible text/thoughts and Search fallback are covered.
All **2,524 web tests in 280 files**, TypeScript, lint, runtime/prompt/catalog
boundaries and the production web build pass. The ten unrelated local edits
retain their original hashes. No native code changed or Unity test run was
claimed for this recovery. It is not yet packaged or physically verified; the
failed device attempt and interrupted follow-up remain in the acceptance record.


## Catalog discovery and world presentation — 2026-10-08

The WorldPresentation scenario starts a fresh English-native/Spanish-target
learner conversation, asks in ordinary language for a half-visible neutral
background with real occlusion disabled, then asks for the normal room view
again. No action IDs, state IDs, schemas or coordinates are supplied in the user
messages. The ordinary chat, suggestion verifier, delegated agent and existing
provider adapter run against desktop Unity. No earlier private history or media
is reused. Useful complementary artifacts remain allowed.

| Access | Run | Observed result |
| --- | --- | --- |
| Managed staging | 4f9c9bf25c7541ecae1cf367e2a409c9 | Both requests complete through native receipts and chat replies. Backdrop 0.5/depth false changes to 0/depth true. Both turns reconcile usage and billing. |
| BYOK | 735738fa7b3446cfbaeaaaab4461f2ac | Both requests complete through the same native path, with real provider usage and API-key-owner billing. |

Both runs preserve exact object records, saved scene revision and independent
real-room collision policy. Head tracking/focus are synthetic Editor state;
scan readiness is false. These results do not establish room alignment, available
physical depth, headset passthrough blending, stereo quality or comfort. The two
managed action turns reconcile 213 credits / USD 0.204802 across 15 usage/charge
entries, with zero outstanding reservations. That figure excludes context-only
chat and earlier attempts.

The first managed attempt (17f5aee9b82b4e659baf5f8252524dc5) returned an empty
plan without querying the available catalog. Its native-receipt gate failed;
accounting still reconciled 44 credits / USD 0.041092. The shared catalog guide
now explicitly explains that static command descriptions are not exhaustive.
For an unmatched request, the agent should search relevant terms, inspect exact
definitions/current facts and check availability before declaring it unsupported.
An empty narrow search permits a broader relevant term, not enumeration of the
whole catalog. This is general capability discovery, without a special prompt
branch for presentation or a separate provider implementation.

An earlier BYOK run (6599ddec12bf428da02f2720212f3746) correctly changed the
view but failed exact object comparison against the early startup snapshot.
The book was identical across all of its recorded agent operations. The runner
now queries native state immediately before the actual request and retains both
baselines. In the subsequent managed run, those two snapshots show the startup
book x changing from 1.4901161193847656e-08 to 2.9802322387695312e-08 before
the requested change, with scene revision unchanged. No tolerance was added to
object equality; both subsequent runs preserve the pre-request snapshot exactly.
The original-book UI journey independently verifies a view action does not edit
the saved scene and that reload does not repeat it.

Local verification: 927 EditMode and 801 unique PlayMode cases pass, with three
optional private-file skips. PlayMode uses the full run plus 87 focused reruns
after correcting the old tray-button count and an assertion that conflicted
with concurrent physics capture; runtime sources were unchanged between runs.
Shared room/book/headless tests pass 1,489 cases in 152 files, plus 55 prompt
checks, app/probe TypeScript, lint, catalog provenance and the production build.
The scripted full native journey passes 574 observations; the generated book
journey and inspected screenshot verify the same action and reload behavior.
No new APK or device acceptance is claimed. Cooling/charge readiness and the
remaining novice, Live, thermal, audio and release gates remain open.


## Mixed-reality movement parity — 2026-10-08

WorldPresentation now adds ordinary learner requests to enable thumbstick walking
while retaining MR, then disable it. No capability IDs, schema arguments or state
identities are supplied by the learner. The original chat/verifier/agent/provider
path discovers the same controller capability as the generated book controls.

Managed staging run `139e0f9a51374a8181997f90601bcd66` passes all four action turns:
backdrop 0.5/depth off, restore MR/depth on, user movement on in MR, then movement
off. Exact object records, saved scene revision, physical collision policy and
Maestro's independent opt-in stay unchanged. Tutor/verifier streaming, original
request/history handoff, native completion, chat replies, provider usage and billing
gates pass. The four action turns reconcile **437 credits / USD 0.420920** across
31 usage/charge entries, with no outstanding reservation. This excludes the
context-only introductory chat. An initial read guessed controller capability v1;
the agent recovered by searching the catalog and inspecting the actual v2 before
executing. No wrong-version mutation occurred.

BYOK run `3b22402a958c48a8802c1bdc76a7eca0` passes the same four ordinary-language
requests through the real Gemini 3.8 Flash adapter. Native state, exact object
preservation, chat replies and provider usage pass; costs belong to the API-key
owner, with no managed-credit assertion substituted for BYOK. Neither run reuses
earlier private learner history, uploaded images, camera frames or speech.

Matching-source native verification passes **935 EditMode and 812 PlayMode cases**,
with three expected optional private-file skips. Tests cover clear/blocked swept
translations and yaw arcs, fixed bindings, query saturation, missing alignment,
per-object real participation, MR controller opt-in, neutral gates, view changes,
concurrent actors and navigation invalidation/rebuild after movement settles. The
complete run passes after earlier synthetic fixture corrections and adding the
two-stable-frame navigation debounce; retained local failure reports are not
claimed as passes. Mixed-source following waits for a fresh path without losing
its owner, while direct steps still use current ground/body checks.

The complete native headless journey passes **586 observations** in
`ce50470ace1f45b281a4abbf2ed541b0`. The original-book run
`61054388242a44afb1c396ab27db06f0` uses generated action inputs, receives native MR
movement completion, preserves the saved scene and reloads without replay. Its
movement screenshot was inspected. These two journeys use scripted responses;
the real provider run is separate. Shared room/headless/Quest tests pass 1,489
cases in 152 files, with the refreshed native controller capture checked again.
Build, lint, probe types, catalog provenance and core boundaries pass.

All current results are desktop evidence. No new APK, headset installation,
physical movement/comfort/performance acceptance, release signing or upload is
claimed. Fixed physical tracking and synthetic collision tests do not establish
headset scan alignment or compositor pixels. Synchronous room-scale navigation
baking and conservative collider envelopes remain explicit performance/clearance
limits; streamed navigation and larger worlds remain accepted implementation work.


## Saved visual layers and shared planner correction — 2026-10-08

WorldPresentation now adds two ordinary learner requests after the existing four
view/movement turns: make only Maestro half transparent and visible behind real
things, save that choice, then remove that visual-layer assignment. The user
messages supply no schema fields or native IDs. Both providers use the original
chat, suggestion verifier, delegated agent and shared capability discovery.

| Access | Fresh native run | Result |
| --- | --- | --- |
| Managed staging | `7b150581bc484b1ea0d53e5a118a38cd` | All six action turns pass handoff, native receipts, completion, chat reply, real usage and managed accounting gates. |
| BYOK | `92b25cc1ef984658a4d3048b01784105` | The same six requests pass through real Gemini 3.8 Flash, with API-key-owner billing. |

Native object.visibility changes to a saved layer at opacity 0.5 and realDepth
false, then returns to empty binding, opacity 1 and realDepth true. The global
view and Maestro's environment collision policy remain unchanged. This verifies
saved native facts; it does not claim physical passthrough pixels or rendered
headset translucency. The layer definition remains available for reuse after its
assignment is removed. The six successful managed action turns reconcile
**772 credits / USD 0.744659**, with 49 usage/charge entries and zero outstanding
reservation. This excludes introductory chat and earlier failed attempts.
Neither run sends old private chat, uploaded images, camera frames or speech.

Earlier attempt `f32d40240f6e4c9091939dfa2a15c4dc` applied the requested view
change but the observer helper incorrectly supplied empty arguments to an
argument-free fact. The helper now omits that field and checks availability
before reading values. Attempt `c181e675c80d451e83bd166298ed5612` reached the
new layer request, where the real planner emitted two catalog queries together.
The strict standalone-command contract refused the proposal before dispatch,
but the task ended rather than returning local correction feedback. Its failed
layer turn still reconciled 76 credits / USD 0.073368; no layer mutation occurred.

The shared planner now identifies that batch-shape error only after every command
has passed validation. It feeds back the rejected proposal within the existing
18-call budget; it neither splits and executes a partial batch nor adds a retry
allowance. Existing successful command/receipt pairs stay in context. Unknown
actions, oversized input and lost native receipts remain hard failures. Regression
tests prove correction into individual reads, preservation of a prior successful
edit, no partial dispatch, bounded repeated rejection and unknown-action refusal.
The successful fresh provider runs are separate from that deterministic recovery
proof; they do not require that a model reproduce the same mistake.

Local verification passes **941 EditMode and 828 PlayMode cases**, with three
expected optional private-file skips, plus **1,722 shared room/headless/Quest and
prompt tests in 168 files**. Build, lint, probe types, catalog provenance, core
boundaries and asset integrity pass. The final archive regression caught an
omitted strict layer check and the repaired reader passes the full EditMode suite;
PlayMode used the same runtime except for that archive-reader repair. Native
headless run `f83ba1d0ea1c4294928d92fb8c7e014f` passes **603 observations**.
Book run `70ae6c7b65ae40409fa271575d4c3715` creates/assigns a layer with the
generated fields, captures the layered construction and reloads without replay.
Its form screenshot was inspected. These two journeys use scripted responses.

The generated manual form still exposes resource IDs/revisions. Shared named
resource selection remains pre-release usability work. No new APK, signing,
installation, upload or deployment occurred. Device acceptance remains on hold
for the owner's cooling/charge readiness and a fresh health check. The outstanding
novice lesson, audio/camera, performance, world streaming and release gates remain
open; desktop success is not a full Quest v1 release claim.
