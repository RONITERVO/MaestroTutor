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

At that checkpoint the generated manual form still exposed resource
IDs/revisions; the named-resource increment below addresses that usability work.
No new APK, signing, installation, upload or deployment occurred. Device acceptance remains on hold
for the owner's cooling/charge readiness and a fresh health check. The outstanding
novice lesson, audio/camera, performance, world streaming and release gates remain
open; desktop success is not a full Quest v1 release claim.


## Named saved-resource choices (2026-10-08)

The optional book editor now shares native `x-choices` metadata for visual
layers, environment collision profiles, appearances and sound sources. It reads
existing paged facts, selects exact references without executing, and retains
explicit resource choices as literal program inputs even when they match the
current binding. Duplicate names disclose distinct IDs; unnamed sounds remain
selectable. Refresh discards the displayed list, not the user's chosen exact
revision; new selection is explicit. Other draft fields and object guards are
preserved. Appearance guard loading covers Maestro, the book and creations.
Sound sources remain shared by ID and capture their revision when playback starts.


Named-resource verification passes **942 EditMode and 828 PlayMode cases**,
with three expected optional private-file skips, and **1,731 shared room,
headless, Quest and prompt tests in 170 files**. Focused selector regressions
were rerun after the final unnamed-resource and explicit-selection refinements.
Build/type checks, lint, catalog provenance, core boundaries and bundled-asset
integrity pass. Native run `7d666ea1c408433a9dae7fe4bdf68703` passes **606
observations**. Final original-book run `da0a822769aa43528f2172ac8837ff43`
selects all four libraries by name, receives completed native assignment receipts
and reloads without replay or browser errors. The visible named sound selector
and layer selector screenshots were inspected. An earlier complete book run
`279e83a7a4f640b8a1f19c0f34226db4` also passed; the final run improves the
sound screenshot's scroll position. The initial full PlayMode attempt was stopped
by an eight-minute focused-test supervisor timeout while still progressing;
the complete rerun used the appropriate longer window and passed.

These new journeys use scripted provider responses. The earlier real managed
and BYOK WorldPresentation runs remain separate evidence; this UI/catalog
increment does not claim a new live-provider or physical-headset pass. No new
APK, release signing, upload or deployment occurred. Device work remains on
hold pending the owner's cooldown/charge readiness and a fresh health check.


## Temporary layer presentation (2026-10-08)

The native `visibility.layer.present` action and `visibility.presentation` fact
share the generated catalog with book forms, programs and the delegated agent.
Named lookup choices select a stable layer ID before reading current guards.
Switching that selection away and back cannot revive an earlier reviewed state.
Ordinary changes to the requested fade retain the current guards.

The renderer smoothly multiplies saved alpha using a transient viewer preference.
The action receipt confirms admission; nested progress reports actual completion.
No blend frame writes a document or replaces leased material identities. Saved
layer membership stays live. Authored definition edits reset only that layer;
tracking/focus loss, view disablement and temporary/workspace boundaries clear
transient preferences. The movement tray's Stop / MR resets them explicitly.
Book/tool Recall preserves viewing preferences and movement opt-ins. Collision,
sound, object placement and autonomous action ownership remain independent.

Fresh real-provider journeys pass all eight action turns through streamed tutor,
verifier, original-context handoff, native execution, final chat and usage checks:

| Access | Run | Confirmed native operations |
| --- | --- | --- |
| Managed | `226b39ac50de432a9921bde2ec933fff` | 37 |
| BYOK | `2191c5ea08434456b183c57d9868674d` | 33 |

Both used Gemini 3.8 Flash. Ordinary learner requests fade Maestro over one second
to half of the saved visibility, then restore his exact saved appearance. Native
facts confirm effective alpha 0.25 during the faded state and 0.5 after restoration,
with unchanged saved definitions. Final removal of the assignment restores normal
visibility. All eight task journals completed and their final replies reached the
original chat. Managed billing reconciles **1,178 credits / USD 1.145306**, with
70 usage and charge entries and zero reserved credits. This excludes introductory
chat and earlier failed attempts. Both runs use fresh synthetic text without old
private chat, images, speech or room frames.

The first managed attempt `d3fe050a2d474dfc9225c0845ac6d09f` correctly executed
the native fade and restoration but hit the probe's old ten-minute supervisor on
the seventh turn before its final reply and accounting check settled. Native ready
was 11:47:48.243 UTC; the task stopped at 11:57:48.571 UTC. The expanded eight-turn
scenario now has a bounded thirteen-minute window within the native fifteen-minute
probe lifetime. Failures retain explicit deadline timing. App leases, provider
request limits and planning budgets are unchanged; the failed run remains failed.

Shared verification passes **1,734 room/headless/Quest/prompt tests in 170 files**,
plus build, lint, probe types, catalog provenance, boundaries and asset integrity.
The native catalog exports 114 actions, 130 facts and 458 discovered runtime inputs;
**947 EditMode tests** and six focused final-source layer runtime tests pass.
The final full PlayMode suite passes **835 cases**, with only the three documented
optional external-asset skips. Scripted native run
`0c92674367ef4f8b8afe509a56c47d84` passes **612 observations**. Scripted original-book
run `f7a3e11a1ea849a99e9d551bf74a0ec4` chooses the layer by name, loads exact guards,
executes a fade, receives its native receipt and reloads without replay. Its form
screenshot was inspected. An earlier native probe compared membership before
assignment with membership afterward; its baseline now captures assignment before
the fade, keeping the saved-state equality check strict.

No new APK, signing, installation, upload or deployment occurred. Physical Quest
acceptance remains on hold for the owner's cooldown/charge readiness and a fresh
health check. Desktop interpolation and provider success do not establish visual
comfort, real depth or sustained headset performance. Imported surroundings,
passthrough windows, image-backed texture authoring, camera/audio completion,
weather/light, water-aware traversal, streamed regions and the remaining novice
and Store release gates remain open.


## Saved region lighting (2026-10-08)

Saved ambient and directional sun settings now use `world.lighting` and
`world.lighting.set` across generated book controls, programs and agent tasks.
Room v27, paired snapshot v26 and archive v25 preserve the settings. Exact
revisions, save-before-publication, Undo/Redo and temporary Keep/Discard use the
existing room authority. Lighting leaves object ownership, movement, animation,
collision profiles, backdrop/depth preferences and sound independent.

Fresh English-native/Spanish-learning conversations pass both action turns:
create dim blue ambient lighting with a warm sun, then restore the original
illustration while retaining the preset. Both used Gemini 3.8 Flash and completed
tutor, verifier, original-context handoff, native receipts and final chat.

| Access | Run | Confirmed native operations |
| --- | --- | --- |
| Managed | `9cbb44c4a14346d98d2a6607dfbf2e90` | 6 |
| BYOK | `04a1581bbb354dad8192055dfcdf63b3` | 7 |

Native readback confirms both requests, exact preset retention and unchanged
objects, collision policy and viewer state. Duplicate handoff checks produce no
extra dispatch or usage. Managed billing reconciles **202 credits / USD 0.194999**
across 14 usage and charge entries, with zero reservations before and after both
turns. Costs exclude introductory chat and the earlier failed attempt. BYOK usage
is attributed to the configured key owner. No old private chat or media was reused.

Verification passes **956 EditMode tests**, **1,736 shared tests in 170 files**,
app/probe TypeScript, lint, production build, catalog provenance, boundaries and
included-asset integrity. The full PlayMode suite passes **842 cases**, with the
three documented optional external-asset skips. After the final built-in-control
lighting exemption, all **eight focused authoring/rendering tests** pass on the
final native source. These include actual book-page and palm Recall materials in
zero light, world-space sun direction, alpha/cutout preservation, failed-save
isolation, continued animation and temporary editing. The broad suite preceded
that final control exemption; it was not reported as a new full-suite run afterward.
The catalog contains 115 actions, 131 facts, 90 current-input mappings and 462
recursively discovered runtime source inputs.

Scripted native run `c3dc952185dd4d0883e1e0d50dc0939f` passes **627 observations**,
including lighting readback, Undo/Redo and restoration. Original-book run
`dd8f1f600eaf4f20b9af44b76f4ded7c` loads current lighting into the generated form,
executes it natively and reloads without replay. Its form screenshot was inspected.
The initial native attempt `4af8f9c7dc1c4c689c5fcc845a4d4fdf` failed an exact
JavaScript-double versus Unity-float comparison; the probe now uses a numeric
1e-6 tolerance, leaving production behavior unchanged.

The first managed attempt `20939a1aeaff420196541d4873f1cc04` completed its native
lighting action but failed the unrelated-object comparison against the startup
handshake. Every recorded agent operation retained the same Book position. The
probe now reads a fresh native baseline immediately before the request, after
startup/context, and saves both snapshots. The object-equality assertion remains
strict; the failed attempt remains failed and retained.

No new APK, signing, installation, upload or deployment occurred. Physical
lighting and performance acceptance remain on hold for the owner's cooldown/charge
readiness and a fresh health check. This implementation supplies one active
region's ambient/directional illumination; shadows, local lights, time/weather,
streamed-region projection and the remaining release gates are still open.


## Authored world time and daily lighting (2026-10-08)

The shared region clock supplies saved day/second, running intent, rate and an
optional daily lighting cycle through `world.time.configure`, `world.time.seek`,
`world.time` and `world.illumination`. Settings edits preserve the current position;
explicit seeking uses a separate action. Two to eight ordered frames interpolate
ambient/sun energy and authored angles continuously across midnight. The same
sample drives rendering and agent/program readback. Physics and animation speed
remain independent. Room v28, paired snapshot v27 and archive v26 retain the state.

Fresh English-native/Spanish-learning conversations pass three requests: create
a dim blue midnight / warm noon cycle lasting 24 real minutes, start it, then
pause at midnight. Both access routes complete the normal tutor, verifier,
original-context handoff, native receipts, final chat and provider-usage checks.

| Access | Run | Confirmed native operations |
| --- | --- | --- |
| Managed | `75136da3d9234a2db9e9dcfc9bc1ec4b` | 12 |
| BYOK | `5d7f3b40744a4a489d954344a7750afd` | 11 |

Native readback confirms active advancement with stable edit guards, exact frame
retention, paused midnight and unchanged objects, view and collision policy.
Duplicate handoff checks dispatch and charge nothing extra. Managed accounting
reconciles **478 credits / USD 0.462308** across 24 usage and 24 charge entries,
with zero reservations before/after every turn. This excludes introductory chat.
BYOK usage belongs to the configured test-key owner. These are fresh synthetic
conversations; no previously captured private media was reused.

Verification passes **969 EditMode tests** and **1,579 shared tests in 157 files**
(room, headless, Quest and prompt directories). After the final search/colour
validation changes, **147 focused shared tests** and **13 focused native
PlayMode authoring/rendering tests** pass. The full PlayMode suite passes **848
cases**, with three known optional external-asset skips; that broad run preceded
the small final search/colour validation fixes. App/probe types, lint, production
build, catalog provenance, core boundaries and included-asset integrity pass.
The catalog exports 117 actions, 133 facts and 92 current-input mappings, with
465 discovered runtime source inputs.

Native run `de6c607a65764d5cafb619e7e6aab7df` passes **648 observations** including
clock settings, seek Undo/Redo, advancement, pause, readback and restoration.
Original-book run `9211e37294974a1c94814a46a55288fa` sets the requested day/second
without letting current-value loading overwrite the destination, adds two daily
frames through visible controls, saves them, then disables the cycle while
retaining both frames. Repeated actions require distinct native invocation IDs.
Its seek and cycle forms were visually inspected; existing original-chat,
resource-selector, construction, fade and reload checks also pass.

The first book attempt `6c939c1100e1476bb9386ff36809d992` exposed exact-name search
results buried behind description matches. Native discovery now ranks exact
names/IDs first, then name/ID matches before description-only results, with stable
ID ordering within ties and unchanged bounded pages. Book and agent share this
search implementation. Regression checks ensure no matches disappear or repeat.
The failed run remains failed. Saved colour validation now rejects a trailing
newline that the old regular-expression end anchor could admit; static settings
and daily frames both have regression coverage. An initial test compilation error
used a nonexistent snapshot helper and was corrected to the existing archive
fingerprint API before any passing result was recorded.

Clock advancement stops under focus/runtime/write holds, skips resumed/stalled
frames and performs no offline catch-up. Runtime progress creates no Undo entries
or edit-guard churn; explicit changes do. Existing save paths checkpoint time and
preserve save-failure and temporary Keep/Discard semantics. Tests cover archive
fingerprints, unsupported files, midnight rendered pixels and allocation-free
cached lighting sampling. They do not establish zero allocation for the entire
frame or physical Quest timing/appearance.

No new APK, signing, installation, upload or deployment occurred. Headset work
remains on the outstanding cooling/charging readiness hold. Weather, local lights,
shadows, water-aware traversal and medium interactions, streamed regions and the
remaining novice/device/Store release gates remain accepted unfinished work.


## Shared weather and rain collection (2026-10-08)

The native region weather definition is shared by the generated book form,
program actions and agent requests. Weather transitions sample the authored
clock; rain intake uses active real seconds. Current/target weather, upstream
cover and measured rain collection have typed facts. A saved rain collection
episode can resume a user-authored event program. Cloud attenuation uses the
same light sample as world.illumination; fog preserves actual book/control
materials. Bounded local rain geometry is presentation only.

Geometry checks use the receiver's effective real-room collision profile.
Tests include a scanned roof covering one participant while a virtual-only
participant remains exposed, plus actual virtual cover, unavailable scan,
query saturation and a start point inside a solid. Liquid tests check measured
opening-area intake, save-before-publication, one reversible episode,
incompatible contents, pause and rollback after failed saving. No unscanned
physical collider is inferred from the depth image.

The new WorldWeather provider scenario uses fresh English-native/Spanish-target
chat to request a gently rainy/cloudy/foggy afternoon, then to clear it while
retaining objects, clock, view and collision policy. Its managed and BYOK
results must be collected separately; merely adding the scenario is not a pass.

Current results are recorded in .quest-evidence/spatial-state/weather-working.json.
One initial focused test expected a textured control to be pure white; the
correct acceptance compares its unfogged baseline against its fogged pixels.
The failed attempt remains failed. No runtime behavior was changed for that
assertion repair.

Physical headset rendering, performance/thermal behavior, weather sound, wind
forces, wetting, ground pools and water-medium interactions remain unverified or
unimplemented release work. Cloud cover currently attenuates accepted lighting;
there is no claim of volumetric cloud geometry. No new APK, signing, deployment,
installation or upload is part of this increment.


Verified results: **973 EditMode cases**, **857 full PlayMode cases** with the
three known optional external-asset skips, and **28 final focused native cases**
pass. The full PlayMode run preceded only a bounded rain ring-cursor cleanup;
the focused run covers that final source. The shared room/headless/Quest/prompt
suite passes **1,582 cases in 157 files**. After the book screenshot exposed
binary32 transport noise, **70 focused shared cases in three files** pass for
the final display-only formatting. Double quantities, integer guards and
unchanged submitted values stay exact. Final app/probe types, build, lint,
catalog provenance, core boundaries and included assets pass. The exported
catalog has 118 actions, 136 facts, 18 events, 93 current-input mappings and
471 source inputs.

| Access | Run | Confirmed native operations |
| --- | --- | --- |
| Managed | 01f814b3b4d84e89ab9491d711faad30 | 6 |
| BYOK | 91e4af0cbbf64f359727b006d803ab31 | 7 |

Both fresh two-turn Gemini 3.8 Flash journeys complete the original tutor,
verifier, exact-context handoff, native receipts and final chat. Requested rain,
cloud cover and fog are accepted, then cleared; objects, clock, presentation and
collision policy are unchanged. Managed accounting reconciles **217 credits /
USD 0.210487**, with 14 usage and 14 charge entries and zero reservations before
and after each turn. Introductory chat is outside those totals. BYOK costs belong
to the configured test-key owner. No captured private media was reused.

Native journey f3d16b24857a419ba9a316ea28266ac8 passes **670 observations**.
Original-book journey f2a951a5916549f39676d7ce4452613e passed before numeric
display polish; final journey f475e612967f4ec192fbcf60d1933a6a also passes.
Its inspected weather form shows saved rain/wind/cloud/fog/seed and current-value
reload through actual generated controls. Existing original-chat, create/edit/
Undo, resource-selection and reload checks also pass. Neither these forms nor
provider acknowledgements establish visible weather on a physical Quest.


## Shared finite liquid media (2026-10-08)

Fluid properties are shared container data, edited through existing native
receipts and the generated original-book form. The new LiquidMedium provider
scenario asks fresh English-native/Spanish-target Maestro chat to fill a named
pool with one litre of light oil at density 850 kg/m3 and increased drag, then
transfer exactly 200 ml to a named empty cup. Native assertions verify quantities,
property inheritance, medium readback, unchanged vessel poses and unchanged
paused simulation state. The harness creates the two empty vessels explicitly;
this scenario does not establish spontaneous asset creation or actual floating
on a physical headset. Real managed and BYOK results are recorded separately.

Native tests cover fluid compatibility and independent copying, strict current
wire validation, saved/snapshot properties, future-version protection, typed
record bounds, live rain-driven depth, sustained floating, dense-prop sinking,
drag, unchanged liquid inventory, paused/animated/unavailable physics, Undo/Redo,
workspace holds and below-scan queries using both participants' collision
profiles. Native and book journeys separately exercise shared authoring,
conserved transfer and current-value reload.

Failed attempts remain evidence: one missing test namespace prevented initial
compilation; the next export exposed the old eight-field typed-record ceiling.
After the explicit 16-field budget change, old nine-field-invalid and container
shape fixtures needed updating. A first floating assertion sampled after the
prop had risen out of the liquid. The subsequent sustained check revealed that
the fixture's vessel was itself falling; making that test vessel fixed allowed
a meaningful floating test. A later editor export exited after Unity's 300-second
pending-operation timeout; the editor was confirmed stopped before a successful
retry. No passing native result is inferred from a stale XML file.

The focused corrected run passes 31 cases. Full verification of the final physics implementation passes
976 EditMode and 863 PlayMode cases, with zero failed or inconclusive cases. Three
known optional private-model/motion tests are ignored. Unity exits with code 0;
the local wrapper initially rejected the aggregate Skipped:Ignored status and
the individual ignored reasons were checked before continuing. All six final
medium PlayMode cases pass, including per-entity ground and immediate hold gates.
Scoped shared tests pass 1,587 cases in 158 files. Production build, lint, probe
types, catalog provenance, core boundaries and included-asset integrity pass.
The exported catalog has 118 actions, 138 facts, 18 events and 476 source inputs.
Native transport, original-book and fresh managed/BYOK results follow below.

Headset work remains on the existing owner cooling/charging readiness hold.
No physical Quest medium behavior or performance acceptance is claimed. Terrain
reservoirs, displacement level changes, carried-fluid mass, water-aware actors,
ripples, water life and regional simulation remain unfinished release work.


The full native transport journey **46492e8d4b664d5895960dd50199dba7** passes
**689 observations**, including medium depth, conserved transfer, inherited
properties and Undo/Redo. Original-book journey
**a6fb0c4404484b31b909c3a012fb392b** passes visible fluid editing, native receipts
and current-value reload; its screenshot was inspected. The shared form's labels
still expose field paths, which remains a release usability-polish item. A final
metadata-only change supplies the documented water defaults when enabling the
optional fluid record; final focused and book verification follows that change.
The full physics run and provider results below precede only that default-example
metadata, not a physics or ownership implementation change.

| Access | Run | Confirmed native operations |
| --- | --- | --- |
| Managed | aed6ac01010648229876f47281b45a7f | 10 |
| BYOK | 1986fe8986f144e59303979e455a92bd | 10 |

Both fresh two-turn **Gemini 3.8 Flash** LiquidMedium journeys pass tutor/verifier,
original-context handoff, native receipts, final chat, usage and duplicate-handoff
checks. Native readback confirms one litre of oil at density 850 kg/m3 and increased
drag, followed by 800 ml in the pool and 200 ml in the cup with the same fluid
properties. Both vessel poses and the exact paused simulation state are retained.
Managed accounting reconciles **340 credits / USD 0.331799** across 18 usage and
18 charge entries, with zero reservations before/after both action turns. These
figures exclude introductory chat and the earlier failed harness attempt; BYOK
costs belong to the test-key owner. No captured private media was reused.

Managed attempt **68957d2789364976ae3b89271d695102** failed during harness baseline
setup before the first liquid request: it incorrectly supplied arguments:{} to
the parameterless physics.simulation fact and then dereferenced the unavailable
value. The corrected harness omits arguments. That attempt remains failed;
no native implementation or provider behavior was changed to make the retry pass.


Final optional-record metadata verification passes **976 EditMode**, **33 focused
PlayMode** and **70 shared tests in four files**. Final original-book journey
**a95198610a64496fab8fce5025b44108** passes: enabling fluid properties starts at
1000 kg/m3 with linear/angular drag 2/1; reloading restores the saved 850/3/1 values.
The final screenshot was inspected. Probe type checking and catalog provenance
also pass after that metadata change. No additional physics code changed after
the full 863-case PlayMode run or the successful provider journeys.


## Environment-query batching (2026-10-08)

This native-only optimization reuses accepted virtual ground across one
synchronous medium-force pass. It retains live pause/scan/participant policy
checks and fresh geometry on the next pass or observation. The same geometric
support predicate is used by direct and batched queries. No public action/fact
schema, saved format, book form or provider prompt changes.

The baseline measurement passes on the preceding implementation. The final
focused run passes **976 EditMode** and **46 PlayMode** cases, including all six
new query cases and existing per-entity, buoyancy, rain and liquid-transfer checks.
Shared contracts pass **1,444 tests in 135 files**; probe type checking, catalog
source provenance and core boundaries also pass. A fresh full native regression
and transport journey follow separately.

For eight ground tiles and 1,000 decorative children, four trials of 4,096 paired
point admissions took 104.40–105.45 ms before the change. The post-change direct
path takes 103.24–104.57 ms versus 1.74–1.77 ms using one batch. Both warmed loops
allocate zero managed bytes. These desktop Unity Editor measurements isolate
admission queries; they do not measure the complete buoyancy pass, whole-app
allocation, a Quest frame, or a city-scale world. The benchmark has no
machine-dependent timing assertion; correctness and warmed allocation are
asserted. Run EnvironmentQueryTests with MAESTRO_ENVIRONMENT_QUERY_EVIDENCE set
to an output path to retain its direct/batched timing JSON.

Existing real managed/BYOK liquid results above remain evidence for the shared
contract. They predate this internal optimization and are not represented as new
provider runs. The unchanged book UI likewise uses its preceding visible journey.
Headset work remains on the owner's cooling/charging hold. No device, cloud,
release-signing or distribution operation is part of this increment.


The broader PlayMode run exposed a timing assumption in an existing visual-layer
test: a 0.1-second fade was required to remain active after a yielded frame.
A sufficiently slow Editor frame can correctly finish it first. The test now
asserts transition start immediately after synchronous shared dispatch, then
verifies completion through normal updates. Existing deterministic
EditMode tests already verify interpolation and large-tick completion. This
failure is retained as evidence; it is not treated as a passing full run.


The completed broad run has **868 passes**, the single timing-test failure above,
and the three established optional private-asset ignores. Production source did
not change afterward. The repaired test and its entire avatar/view family,
plus environment and liquid controls, pass **132 focused PlayMode cases** with
zero failures. This records the failed first run and successful repair separately;
it does not relabel the failed aggregate report. A second measurement inside the
broad run shows 101.52–102.36 ms direct versus 1.80–1.81 ms batched, again with
zero warmed allocations. Catalog contracts remain 118 actions, 138 facts and
18 events, now with 477 native source inputs.


Final native transport journey **1a98962f538e444d8ab6da50ba11c066** passes
**692 observations** with both client and Editor exiting 0. Shared native
receipts and readback cover the complete existing journey, including finite
medium properties, conserved transfer and Undo/Redo. No provider was used in
this run. The generated catalog differs from the preceding commit only in
source provenance; its public schemas and descriptions are unchanged. All ten protected unrelated working files remain unchanged.


## Actor water traversal (2026-10-08)

The shared waterTraversal.v1 capability persists an independent actor policy:
Maestro defaults to avoidance; authored creations opt in to avoidance or bounded
wading. Follow, controller walking, authored travel, recorded root motion and
physical animation preview use continuous finite-liquid sweeps. Live fill levels
and both participants' environment policies apply while rigid forces are paused.
This does not change explicit placement, grips, buoyancy, collision profiles or
visibility. The read-only path fact proves water admission only, not a connected
route. Detours, escape/swimming, terrain reservoirs and headset acceptance remain
open.

Current native verification passes **982 EditMode**, **107 focused PlayMode** and
**878 full PlayMode** cases. The full run has zero failures and three established
optional private-asset ignores. Shared room/book/headless tests pass **1,432 in
134 files**, with **22 probe-contract tests** separately passing. Types, targeted
lint, catalog provenance and core boundaries pass. The catalog contains
119 actions, 140 facts, 18 events and 480 native source inputs.

Failure evidence remains explicit. The initial saved-room assertion relied on a
different snapshot's object order; it now finds the stable object ID. A later
regression exposed stale Collider.bounds after an immediate transform move;
water-motion reads now synchronize transforms first. Dedicated tests cover both
fresh clearance and subsequent water collision, plus an opted-in creation's
recorded playback. All nine dedicated cases pass. Headless run
320c204e07284ab08dbcd4b5b01f2c8f failed because the harness used creation-only
object.placement for Maestro. The corrected harness uses existing
object.definition and object.position v2; no runtime contract was loosened.
The completed native/book/provider results follow separately below.


The corrected deterministic native journey **29955143e8b8462ea4d6c85479041cd3**
passes **702 observations** with client and Editor both exiting 0. It checks water
configuration, exact revision readback, unchanged Maestro pose, Undo/Redo and
restoration alongside the complete existing world journey. No provider was used;
this does not replace movement tests or physical headset acceptance.


Original-book journey **75b1f9ffed404fcf888196e4e9f9c0ef** passes with client and
Editor both exiting 0. The generated form selects Cooperative ball by name,
loads its current revision, saves avoidance while retaining the 0.25 m preference,
and reloads the accepted values. The captured two-page form was inspected: target,
mode, depth, completion status and current-value controls are visible and readable.
This is the desktop original-book/native bridge, not Android texture or physical
controller acceptance.


Fresh real-provider WaterTraversal journeys also pass:

| Access mode | Run | Confirmed result |
| --- | --- | --- |
| Managed | d16ffa9c1faf4290829a2f7fa0a4bf72 | Two ordinary requests, three completed native water-policy edits, 40 observations |
| BYOK | 8d9593d9f8fe4e15990f6b19bead2a84 | Same two-request semantics, three completed edits, 40 observations |

Both use Gemini 3.8 Flash through the original tutor/verifier/delegated-agent
path. Maestro first wades to 0.15 m while WaterRobot avoids water; the later
request makes Maestro avoid water while preserving that depth preference and
the robot's unchanged policy. Native saved/live poses, collision environments
and simulation state match the pre-request baseline. Each action turn confirms
original context, verified handoff, native receipts, completion and reply in chat.
The named robot is a harness-created box stand-in; no autonomous swimming,
walking, camera input or actual scanned room is claimed by these provider runs.

Managed accounting reconciles **329 credits / USD 0.321044** across the two
action turns (176 + 153 credits), with no mismatches or outstanding reservations.
The introductory chat is outside those totals. BYOK provider usage is retained;
managed billing is inapplicable because the API-key owner pays the provider.
All ten protected unrelated working files remain unchanged. Headset, signing,
upload, deployment and Store acceptance were not exercised by this increment.


## Water-aware following (2026-10-09)

The existing native follow capability now detours around finite liquid cavities.
There is no new agent-specific action or saved format. Users, programs and agents
retain the same water policy, follow invocation, cancellation and live status.
Candidates use the same expanded geometry as actual traversal, with accepted
ground and per-participant environment admission. Search work is cooperative and
bounded; actual steps always recheck current water and body clearance.

Eight native route scenarios cover following around a pool, personal distance
with a book near the viewer, water moved into an accepted path, policy changes
during planning, newly permitted or emptied water, multiple pools, a submerged
destination/Stop, and virtual ground below the scanned floor. Geometry tests
cover rotated/scaled/tilted shapes and actual wet foot height on a sloping edge.
A deterministic search test requires bounded work per tick and rejects a route
whose final validation fails. Final full-regression results follow below.

The preceding managed/BYOK water-policy runs remain evidence for the unchanged
shared authoring and handoff contract. They did not walk around water and are not
relabelled as provider or headset acceptance for this routing change. Swimming,
escape, generic NPC movement and complete multi-level navigation remain open.


Final native regression passes **988 EditMode** and **886 PlayMode** cases, with
zero failures and three established optional private-asset ignores. This includes
the complete existing avatar, ownership, terrain, audio, physics, storage and
world-motion suites. Shared room/book contracts pass **1,414 tests in 133 files**,
with **22 probe-contract tests** separately passing. Probe types, native catalog
provenance, core boundaries, included assets and unique Unity metadata pass.
The generated catalog retains 119 actions, 140 facts and 18 events, now backed
by 483 native source inputs. This increment changes no provider orchestration,
UI layout or storage format; prior provider/book results retain their original
scope. The native transport journey is recorded separately below.


Deterministic native journey **0dcaaab3664e4c58bd11ea8c3f88bcc6** passes with
**700 observations**, client exit 0 and Editor exit 0. It rechecks the complete
existing shared room command/receipt/save workflow, including water settings;
the separate native movement tests establish the new routing behavior. No real
provider, physical headset, new package, release signing, upload or deployment
was used for this routing increment. All ten protected unrelated working files
remain unchanged. Physical movement quality, thermal behavior and planning cost
remain pending the existing headset cooling/charge hold.


### Shared liquid contacts and bounded ripples (2026-10-09)

Water contact now has one native sampling path used by presentation, shared facts
and event programs. `object.medium.contact` names the changed vessel and reports
participant, kind, entered/exited/crossed phase, sampled speed, depth and an
explicit authored-world surface point. `object.medium.contactState` and
`input.medium.contactState` distinguish unavailable sampling from a dry result.
All three advertise `liquidContacts.v1` through the generated catalog. Users and
the in-app agent can build an editable listener using the existing program
system; no separate agent-only contact endpoint or new saved-water format is used.

Created rigid props reuse the existing 4x4x4 collider-occupancy samples. Maestro
uses six visible mapped joints; tracked hands use the actual index fingertip,
and controllers use the same aim-pose origin as physical pushers. Distant ray
hits do not touch water. Sampling runs at up to 20 Hz, selects the smallest
containing cavity, and respects both participant and vessel environment policy.
An unavailable inner vessel cannot borrow an outer pool's readiness. Props
allowed outside the scanned room can contact virtual water below its floor;
physical input retains the global environment policy.

Initial placement, geometry/ownership changes, reload, tracking reacquisition,
pauses and workspace holds establish fresh baselines. They do not manufacture
entry/exit events. A strict finite-surface crossing can report a fast sample
that enters and leaves the cavity between ticks. This is bounded sampled contact,
not exhaustive continuous fluid or skin collision. Contacts alter neither saved
liquid quantity nor velocity; free-body buoyancy/drag remains in its existing
physics path. The new event can drive authored sounds or behaviors, but this
increment does not add automatic splash audio, haptics, fluid displacement,
terrain reservoirs, swimming or aquatic NPC simulation.

Each vessel renders at most eight transient one-second expanding waves on its
existing clipped free-surface mesh. No per-wave object, collider or extra liquid
store is created. The surface follows gravity; material/opacity, authored-world
lighting and depth occlusion still use the shared presentation pipeline. The
bounded effect is an initial readable contact cue, not a final art treatment.

Final native regression passes **992 EditMode** and **895 PlayMode** cases, with
zero failures and the three established optional private-import skips. Nine new
contact cases cover actual Input System controller events, Maestro joints, typed
native event delivery, below-scan policies, unavailable nested geometry, focus/
tracking/workspace reset, finite crossings, bounded rendering and expiration.
The rendered ripple was inspected on the measured finite surface. Hand-tip
injection remains an adapter-boundary check, not physical hand acceptance.

Shared contracts initially pass 1,366 tests in 123 files. After the real-provider
diagnostic fixes, the shared/core room and probe-contract subset passes 1,349
in 122 files; the focused three diagnostic/contact tests and probe types pass
again after the final valid-program fast path. Quest UI/probe contracts pass
274 tests in 29 files. These are overlapping suites, not additive totals. Billing
and room-journey checks pass 35 tests in three files after the baseline repair.
Catalog provenance, core boundaries, included assets and unique Unity metadata
checks pass. The generated catalog now contains 119 actions, 142 facts, 19 events
and 486 native source inputs.

Deterministic native journey **b4dc89d57c6145fab38303d8976b4ab9** passes **709
observations**, including unknown contact readback while tracking/physics are
unavailable; client and Editor exit 0. Original-book journey
**9d6c11860a6745a8bce1cb768edb9beb** passes the complete existing form/chat/native
workflow and the new contact fact inspection. Its screenshot was inspected:
users can select a tracked side and read unknown versus dry through the existing
catalog form. The initial book test used the wrong search label after changing
category; correcting it to `Search facts` fixed the test without product changes.
An initial duplicate event/fact catalog identity was also corrected before the
passing native regression, using explicit `contactState` fact IDs.

New `LiquidContacts` real-provider acceptance asks ordinary English/Spanish chat
to save, arm and stop an editable water-contact listener, preserving its finite
vessel, objects, quantities and paused physics. It checks the authored event,
source filter, participant field, state assignment and twenty-second delay.
No tracked contact is fabricated; physical event delivery is separate PlayMode
evidence. Fresh BYOK **80a69f05f194440cb7e05bb489fe9946** passes all three requests,
using the configured `gemini-3.5-flash-lite` fallback during provider high demand.

Initial managed **b2b3cf16df224b3c81a1f3fd94a97aa5** failed while the fallback
model repeatedly submitted malformed programs. Shared source validation now
identifies missing/unknown fields and malformed expression/assignment shapes,
without accepting, normalizing or silently repairing invalid programs. Managed
retry **60f8d2aa5a554184a65e494ba34d94dd** successfully saved the native program
and passed handoff/usage coverage, but strict billing failed: 248 credits were
already reserved at its baseline, and earlier settlement contaminated its
measured deltas. Failed evidence is retained. Chat and Live room acceptance now
wait for a settled billing baseline, with a bounded preflight that fails before
provider calls if earlier reservations remain. Ledger/charge checks are unchanged.
Fresh managed **391710e70e9e475580fd8ac384febe2e** passes all three requests,
including native start/wait/stop and preserved source/world state. Gemini 3.8 Flash
and the configured 3.5 Flash Lite fallback both appear in these successful
managed/BYOK journeys. Managed accounting reconciles **282 credits / USD 0.273610**
across the three measured requests, excluding the introductory chat, with zero
reservations before and after each. No tracked contact was synthesized. Final
lint, probe types and whitespace checks pass. All ten protected unrelated files
retain their original hashes. PR comments were reread with no new findings since
the previous review. No new headset, APK, signing, upload, deployment or
Store-readiness claim is made; the existing device cooling/charge hold remains.


Release gate **37861313979** passed app/shared tests and lint, then exposed the
production TypeScript target's older `Error` constructor signature in the new
billing preflight. The helper now preserves its cause with the project's existing
`Object.assign` pattern. The full local production build and all 35 affected
billing/room-journey tests pass after that compatibility repair. The successful
native/provider runtime behavior is unchanged; the exact follow-up commit still
requires its own release gate.


### Imported spatial audio acceptance — 2026-10-09

The shared source/emitter system now accepts bounded private WAV assets through
`audio.import` (select, inspect, accept, cancel and refresh), with exact content
identities, off-thread validation/decoding and silent acceptance. The same source
variant is available to the agent, generated book forms and construction resources.
Portable workspaces include the original bytes and report missing sound references.
Saved playback handles and live connections are never restored. Current boundaries
and unfinished audio adapters are documented in `unity/AUDIO.md`.

Regression evidence currently passes **2,732 shared/web tests in 299 files**,
**1,012 native EditMode**, **899 native PlayMode**, and the subsequent **16-case
native PlayMode** boundary/lifecycle subset. Native full-suite skips are the three
established optional private-asset cases. Android passes **97 tests**, with two
optional private-archive skips, plus release AAR assembly and lint. The built AAR
contains the `AudioPicker` keep rule required by JNI reflection. Thirteen portable
workspace browser fixtures were regenerated from actual native receipts.
Production TypeScript/build, lint, probe types, catalog provenance, included assets
and architecture boundaries pass. All ten protected unrelated files are unchanged.

Original-book **ec002e5e5bda4f7dbb68440ecacbba97** passes the existing workflow plus
synthetic WAV discovery, imported-source variant, named sound selection, spatial
attachment and native one-second PCM completion. Its screenshot was inspected.
This run uses the original rendered book and real Unity handlers, with offline
scripted provider responses and a muted renderer; it does not exercise Android
selection or establish headset audibility.

Managed **837a00345652449ab07f556846424bd6** passes three ordinary English/Spanish
chat requests: attach the imported bell silently, play once, then detach while
retaining the source. Actual native consumption is one second. Tutor, verifier,
handoff, exact request/history, receipts, final chat and usage checks all pass.
The three measured turns reconcile **816 credits / USD 0.797831**, excluding the
introductory chat, with zero reserved credits before and after each turn.

Failed real-provider runs remain evidence, not passes: BYOK
**58f06ba1f7ba46fa8044d6c55d0c693c** used an old unavailable library result after
refresh. Refresh now returns one verified entry and continuation immediately,
while library pages hold two entries within the shared value budget. BYOK
**dc46971a662b49bbbd7f145e7fd9f819** attached correctly but the driver incorrectly
passed empty arguments to a no-argument fact; the driver now omits them and checks
availability before reading values. BYOK **0957ad8f6002488ca27c2e89186a8b3f** sent
an execution start without its call. Shared bounded pre-dispatch feedback now
allows correction of that missing call, without guessing fields or replaying prior
effects. Unknown capabilities, bad arguments and lost native receipts remain hard
failures. Three additional shared regression cases verify those boundaries.

Other initial failures were retained and corrected: the native equality test lacked
its manifest environment; workspace fixtures lacked sound counters; worst-case
quoted metadata exceeded the shared value budget; the new result array lacked its
explicit minimum size; and two existing full-suite tests needed a fresh synthetic
tracking sample and an evidence output directory. Contracts were not relaxed.
The provider and UI checks use only an explicitly synthesized bell saved through
the real private library; no captured private media is sent to the provider.

Fresh BYOK **41dddf896c26432b99aa4e18a6e02c91** also passes all three requests,
including one second of native consumption and exact source retention. Client and
Editor exit 0 in both provider runs. Successful managed and BYOK responses use
Gemini 3.8 Flash. Final catalog/EditMode again passes 1,012 cases. Deterministic
native journey **f1430828558d4268b5c102dc26a19df3** passes **703 observations**, with
client and Editor exit 0. The catalog contains 120 actions, 144 facts, 19 events
and 490 native source inputs. Exact-head CI results are tracked on PR #248. No physical Quest acceptance, APK signing/upload, cloud deployment or
Store-readiness claim is made. The existing device cooling/charge hold remains.


### Image-backed shared appearances — desktop/provider verified, 2026-10-09

The `image.import` adapter selects and inspects a bounded static PNG/JPEG, saves
exact private bytes and creates one unbound reusable appearance. Users, programs
and the agent assign that appearance through the existing shared binding. Root,
recipe-part and imported-material addresses retain their precedence; book pages
and drawing overlays keep their display materials. Texture, surface opacity,
collision participation and sound remain independent. Original-chat generated
images must later enter the same asset pipeline, with no Unity provider duplicate.

The full shared/web suite passes 2,736 tests in 299 files and the production build
passes. Full native regression passes 1,031 EditMode and 906 PlayMode tests, with
three established optional private-model skips. The focused native set also passes
57 EditMode and nine PlayMode tests. Android
passes 104 tests with two optional private-archive skips, release AAR and lint.
GPU readback verifies all eight EXIF orientations and alpha; texture leases,
missing-file repair, preservation waits, cancellation and archive identity are
covered. The catalog contains 121 actions, 147 facts, 19 events and 495 source
inputs. The 1,536 archive entry cap fits all bounded libraries and metadata; its
512 MiB total remains enforced.

Original-book **8e08939f954745e2ba08032999c4e5f5** passes the existing workflow
plus image discovery, appearance creation, opacity and book binding. Its control
screenshot was inspected. Provider responses are scripted for that run; it does
not establish Android picker behavior or physical Quest visual acceptance.

Failures are retained as evidence. Initial native tests found obsolete appearance
fact versions and error-length expectations; both were corrected and rerun. Fresh
native recovery evidence exposed Unity expanding absent construction records into
empty objects, which the web bridge correctly rejected. Serialization now preserves
those nulls. Book run **33b031a87ef94b599f2e5ad062762136** found image actions
missing from advertised runtime features; advertisement and a regression assertion
now cover that path. The first texture test also caught readiness ahead of material
refresh; object binding status now stays loading until its renderer catches up.

Managed **09b257d17e2448c2a2a7edd47e794b88** failed semantic acceptance: it found
the exact image but stopped before creating or binding its appearance, asking for
permission again. Shared discovery now allows 32 planning calls / 24 read-only
batches while retaining three action batches. Completion guidance explicitly covers
necessary reversible setup for an already requested effect; native confirmations,
missing authority and uncertain earlier effects remain protected. The 87 affected
shared planner, prompt, bridge and preview tests pass. This increases the maximum
read cost/time, not mutation allowance. Fresh managed **c98a48c730b349d9a83a70f805d642d6** and BYOK
**bcdb3707868e416395dd89abb50176dd** subsequently pass all three ordinary-language
requests using Gemini 3.8 Flash. Client and Unity exit 0 in both runs. The original
private image bytes remain with the same SHA-256 after removal, and other surface
preferences remain unchanged. Managed usage reconciles **1,119 credits / USD
1.097833** for the three measured turns, excluding introduction, with zero reserved
credits before/after each. The first successful managed turn peaked at **85,929
prompt tokens**; bounded working-context compaction and discovery latency/cost
remain release work. Full durable traces, exact definitions/identities, current
guards and uncertain-effect evidence must survive that optimization.

Final deterministic native journey **1d92eb93d07c4006b1dc489dec2838be** passes **706
observations**, with client and Editor exit 0. This exercises the shared native
transport and authored systems without provider calls or headset input.

Physical Quest texture decoding, picker usability, sustained memory/performance
and visual comfort remain unverified. The device cooling/charge readiness hold
continues. Generated-image attachment, other/larger formats and professional colour
management remain unfinished. No APK, signing, upload or deployment is implied.


### Lossless room receipt working context — 2026-10-09

Planning and final narration now share a provider-only view that stores large,
byte-identical receipt fields once. A separate `receiptReuse` table maps each
original receipt index and field to its exact shared value. Scalars, guards,
failures and every distinct observation remain available; the current scene is
still complete. Authored values that resemble references are ordinary data and
cannot create links. No model-written summary, truncation or semantic merging is
used. The original request, history/media, command-to-receipt links, prior-task
uncertainty, accepted starts and full durable journal remain unchanged.

Offline measurement against the preceding managed/BYOK image journals reduced
receipt JSON by 43–63% across their six tasks, with exact reconstruction of every
field. This measures receipt characters only, not total tokens or provider cost.
The static planner instructions still exceed 72,000 characters; unique discoveries,
conversation and related-task context also remain. Broader discovery latency and
context scaling are therefore still release work.

The shared/web suite passes 2,746 tests in 300 files. Fidelity cases cover changed
revisions, removals, null versus absence, ordered data, unknown fields, literal
reference-shaped content, no input mutation and exact historical command links.
A task-level regression confirms that only the provider view is compacted; the
returned result and durable receipt callbacks retain full native data. Initial
checks caught helper placement outside the prompt catalog and a test-only helper
outside the configured JavaScript target; both were corrected without loosening
the architecture or runtime checks. Fresh provider acceptance is recorded below.

The first managed pass **e51bc3c0b7d343b7aa73f155859ad57b** completed all three
image requests. Its first-task peak was 45,118 prompt tokens, but an actual
continuation exposed 96,002 tokens because earlier task receipts were still inline.
The three turns reconciled 998 credits / USD 0.979780, excluding introduction.
That run established semantic compatibility, not complete context scaling.

Prior-task planning and final task-control narration now use the same exact-value
projection, with references keyed by the original operation index. Missing receipts
stay absent and uncertainty stays true. Offline reconstruction of that continuation
preserved all 17 operations while reducing its prior-task JSON from 186,645 to
71,005 characters. The image provider scenario now explicitly requests continuation
and checks the exact prior task identity; it cannot pass that coverage as fresh work.

Final managed **db301d6dc49042d78fd52e45c3c30858** passes all three image requests,
including the explicit continuation of the exact prior task. First/follow-up/removal
peak inputs are 45,312 / 54,145 / 32,198 tokens. Usage reconciles **791 credits /
USD 0.769262**, excluding introduction, with zero reserved credits around every turn.
Compared with the earlier intermediate continuation's 96,002 tokens, its 54,145
peak is lower; these are individual variable model runs, not a controlled cost or
latency benchmark. The initial BYOK **4d55827fe2fd4cffb0e5aed1d39ab3bd** also
passed before the prior-task fix. Final BYOK **d1c6ceccb98c4562b517d3c698343572** also passes all three requests
and explicit continuation; its peak inputs are 44,942 / 53,587 / 32,011 tokens.
Client and Unity exit 0 for both final runs. Exact private image bytes and full
uncompressed native/prior-task journals were separately verified after completion.
Build, lint, probe typing and architecture checks pass. Native code and installed
device state are unchanged by this increment; physical acceptance remains on hold.


### Shared discoverable room guides — 2026-10-09

The original app selects shorter planner instructions when the native scene
advertises `catalogGuides.v1`. Ten versioned programming and motion references
use read-only catalog search/inspect with `category:"guides"`. The book's optional
Guides category exposes the same native definitions and prerequisite links.
Reading cannot author or execute an action. Reference text has one canonical
source in `shared/prompts`, a generated Unity resource, exact web validation,
detached native receipts and CI generation/provenance checks.

Core authority, original input, receipt, revision, placement and completion rules
remain always loaded. Older installed runtimes retain byte-identical complete
instructions. Guides grant no permission or feature availability. Program creation
must inspect the relevant grammar and prerequisites rather than guess syntax;
exact guide observations already in the task can be reused.

Fixed instructions decrease from 71,958 to 46,245 characters (35.7%). This is not
a token, cost or latency benchmark: complex work makes discovery calls and adds
needed references into context. Full journals retain every native observation.

Real composite testing also found two native pose issues. Object-list observations
now use the live authored-frame reader used by placement facts and saving, which
preserves exact direct-child values without replacing motion with saved positions.
Undo/Redo now restores poses only for objects changed by that history entry while
continuing the existing full saved-component reconciliation. An unrelated nested
Book keeps its live position and unchanged saved data through three Undo/Redo
cycles. Exact effect assertions remain strict. The harness records a fresh native
pre-request reading beside the startup snapshot.

**Verification:** all **2,750 shared/web tests in 301 files**, build, lint, probe
typing, prompt ownership, core boundaries and catalog provenance pass. Final native
verification passes **1,032 EditMode and 908 PlayMode tests**, with three optional
private-model skips. The catalog has 121 actions, 147 facts, 19 events and 497
native/resource provenance inputs.

Final native **749a689e28a840729151b3168a14ab8e** passes 702 observations.
Original-book **b85fb8e7e552453b94cae1773892f592** passes its forms/chat/native
journey and exact guide/prerequisite inspection with no authored scene change.
The screenshot was inspected: reference text wraps within the existing inspector.
Client and Editor exit 0. These use real Unity Editor; the book provider is
scripted, and neither proves Android or physical Quest acceptance.

Final BYOK composite **006bc3b132954525ace12aba6a86ea34** passes discovery of all
five required exact guides and the pinned included module, save-only behavior,
two independent native event waits, both constructions, geometry, placement,
physics/hinges, Undo/Redo identities and no post-completion replay. Save/start
measurements use 11/3 agent planning/reply calls, with peak agent inputs
41,930/28,739 tokens; introduction
and baseline creation are excluded from those measurements. Earlier BYOK image
**c82f2ac59a3a4492a78fe5b4aeef9ddb** passes all three image requests and exact prior-task
continuation (before the subsequent native history fix), with peaks
40,470/51,356/27,062 tokens. No managed billing applies to BYOK.

Final managed composite **fbaf2930aab742f7a2f99b205afae9cd** passes the same complete
semantics, including Undo/Redo. Save/start uses 10/3 agent calls with peak agent
inputs 41,844/28,708 tokens. These measured turns, including their tutor/verifier
calls, reconcile **300 credits / USD 0.291138** with no mismatches or outstanding
reservations. Introduction and baseline creation are excluded.

Final managed image **448591484e1042bdb8e016144d1c5a52** passes assignment,
half-transparency and removal, retaining the exact private image, readable pages
and unrelated objects. Explicit prior-task continuation is verified. Its 44 native
observations and client/Editor exits confirm the final native source. Initial,
continuation and removal use 22/8/6 agent calls with peak agent inputs
40,412/51,946/26,809 tokens. These three measured turns, including their
tutor/verifier calls, reconcile **860 credits / USD 0.837203**, excluding the
introduction, with no mismatches or outstanding reservations. This is successful
functional acceptance, not a latency or cost improvement claim.

**Retained failed attempts:**

- An early managed launch was refused by the mirror ownership guard while the
  bundled book verification still ran; no provider task started.
- Composite **388e476c4f0640e3a74a18240683abae** compared against startup before
  the Book frame settled. **54efcf8802654ccd867b231ad6531c19** then rejected a
  malformed targetless inspection in the new harness read; the harness now uses
  the existing `physics.environment` fact.
- Composites **bba02052a17243a9ab6dd0fb46fe3d89** and
  **c44990bda2e24d6b96c3ca8e61107721** completed discovery and both constructions
  but failed the exact unrelated-Book comparison at Undo. The first observation
  correction alone was insufficient for the real Book hierarchy; the history
  correction above resolved that path without relaxing assertions.
- Managed image **029da24dde1a48fd866016d818a271f4** completed its initial
  assignment, then failed a continuation reservation for insufficient test credits.
  It is not a full acceptance pass. Two guarded Stripe test-mode checkouts each
  produced one reconciled 1,000-credit webhook grant for the isolated staging
  wallet. The adapter now selects the observed Card control once, including its
  zero-layout-size variant, rather than toggling it while fields load. Existing
  HTTPS/host, test-session and test-card guards remain enforced; five focused
  safety/billing tests and both real test-mode grants pass. No production payment
  data, credit rules or deployed service configuration changed.

The physical cooling/charge hold remains. No APK, signing, upload or deployment
was performed. Unique context growth, serial discovery latency and the remaining
headset/release gates stay open.


### Bounded grouped catalog planning — 2026-10-09

The original app's planner can propose up to four independent catalog reads in
one response. It validates every command and advertised feature before the first
read, then dispatches each query separately through the existing native contract.
Each read has its own durable intent, actual receipt, operation index and query
budget charge. The 24-query/three-action ceilings are unchanged. Separate reads
can observe different moments; this is not an atomic world snapshot. Dependent
queries wait for their actual inputs, and groups cannot contain edits, execution,
captures, motion searches or rule controls.

Cancellation, a changed session, missing capabilities, failed persistence or a
lost acknowledgement stop further dispatch. A rejected native query ends its
group and the next planner receives the acknowledged count and undispatched
remainder. Grouped reads use fresh native state; mutations retain the original
planning-time guards. Module inspections still feed exact content-pinned imports.
The public/native parser continues to reject multi-query native requests.

All 2,764 shared/web tests in 302 files pass, including lifecycle, partial-journal,
query-budget and stale-mutation regressions; build, lint, probe typing, prompt
ownership, core boundaries and catalog provenance pass. No C# or bundled native
resource changed from `605c9554`; its 1,032 EditMode / 908 PlayMode passes remain
the native baseline, with three optional private-model skips.

BYOK image **06f3d6ec2bf644f792d562da56da3d9f** and managed image
**406e510b34b24e35a4c590b1e9d1ac73** both pass all three requests, exact prior-task
continuation, native appearance effects and unrelated-object/page preservation.
Their grouped proposals contain two to four queries; journals retain 27 and 22
separate acknowledged catalog operations respectively. Client and Editor exit 0.
BYOK uses 11/5/5 agent calls with peak agent inputs 44,603/56,460/27,204 tokens.
Managed uses 11/4/5, with peaks 43,665/52,790/27,368, and reconciles **574 credits /
USD 0.562490**, with no mismatches or outstanding reservations. These measurements
exclude introduction and include each measured turn's tutor/verifier billing.
The preceding managed image run used 860 credits / USD 0.837203. These are observed
runs, not a controlled performance benchmark: fewer calls did not reduce peak
context, and wider task latency/cost acceptance remains open.

BYOK composite **5d6c1c5c21cd43e9ad02d40863245ef4** passes exact discovery of the
five guides and included module, save-only behaviour, both independent events,
construction geometry/placement/physics/hinges, Undo/Redo identity and no replay.
Its save/start turns use 5/3 agent calls with peak agent inputs 42,192/28,933 tokens,
excluding introduction and baseline creation. Two groups (four and three reads)
produce seven individual acknowledged catalog operations.

Final managed composite **9bdca621e8fb4d7688adf32ae95dd94f** passes the same complete
native semantics and exact module/guide requirements. Save/start use 6/3 agent
calls, peaks 42,167/29,043 tokens, and reconcile **245 credits / USD 0.240125** with
no mismatches or outstanding reservations. The preceding managed composite used
300 credits / USD 0.291138. Introduction and baseline creation are excluded;
tutor/verifier calls within the measured turns are included. Its two grouped
proposals (four and two queries) plus one standalone read produce seven separate
acknowledged catalog operations. No native assertions or budgets were relaxed.

Retained failed managed attempt
**129e60b466ef4a73954470e02d6ac33b** inspected the exact module and all five guides
and saved the program, but its final narration reservation failed: the isolated
staging wallet was 13 credits short. Its eight acknowledged operations are retained;
the program was not started. This is not a full acceptance pass and was not a
planner deadline failure (215,432 ms against 600,000 ms). Replenishment uses the
existing guarded Stripe test-mode flow and retries use fresh isolated rooms.

The replenishment completed as one `cs_test_` checkout, one purchase ledger entry
and a 1,000-credit staging grant. No production payment data, manual credit write,
service configuration, device query, APK, signing, upload or deployment changed.


## Selected virtual-camera provenance — 2026-10-09

The existing book camera controls now route a native virtual-scene source through
chat and Live. Snapshots/history/attachment variants and delegated Live frames
retain `virtual-scene` provenance. Live uses a source caption in its video image
and preserves exactly those bytes for handoff; the managed gateway contract is
unchanged. Native origin never grants spatial knowledge or room-edit authority.

Camera-source unit tests cover explicit selection, unsupported/reserved source
IDs, cancellation, stale/malformed/late images, wrong session/request, supersession
and suspension. Native tests cover real rendering, a separate agent snapshot
cache, interrupted leases and room gates. Real Chrome covers the React preview,
canvas capture, snapshot and Live transport, stale-frame shutdown and zero OS
camera calls with a recorded native JPEG. Android tests/build/lint verify bounded
origin-checked top-document delivery; no new browser camera permission is granted.

The isolated `scripts/probe-quest-camera-provider.ts` runs real original-app text
and Live providers with that Chrome-produced image and locally synthesized novice
speech. Successful managed and BYOK runs recognized both red/rojo and the virtual
source, returned model audio, preserved frame bytes/origin and passed input/output
pacing. Managed text plus Live reconciled 18 credits/USD 0.016987 with no mismatch
or reservation. A preceding BYOK Live greeting-only response is retained as failed
semantic evidence, not removed by the later pass. The broader novice conversation,
aftersteps/artifact delivery, agent action chain and headset camera performance
are not covered by this narrow test.

The final parity audit also found and repaired two headless filters that accepted
only generated-image origin. Two additional managed/BYOK cases now exercise
JSON-RPC chat dispatch, saved variants, tutor, verifier, agent planning/results
and original handoff labels together. All 38 tests in the affected headless suites
pass, along with probe typing and lint.

See [camera contract and limits](QUEST_V1_PLAN.md#virtual-scene-camera-sharing--2026-10-09).


## Physical-camera transport parity — 2026-10-09

The selected headset-camera adapter joins the same preview/snapshot/chat/Live
path as the virtual camera. Shared origin validation now includes generated,
virtual-scene and physical headset-camera images. Deterministic managed/BYOK
cases cover JSON-RPC dispatch, tutor, verifier, agent planning, receipts, final
chat and persisted image origin together. Browser Live tests check both camera
captions, exact sent-frame provenance and discarded stale callbacks.

Native tests cover permission failure followed by a late grant (no restart), a
fresh selected lease, source changes, room gates and malformed source IDs.
Physical-camera frames carry their actual JPEG dimensions/hash without invented
virtual pose metadata. Real Chrome verifies a square synthetic fixture through
React, canvas/video, snapshots and Live, including permission re-selection. This
is transport evidence only: no physical sensor or paid provider was exercised by
that synthetic fixture. The shared provider paths retain their earlier real
managed/BYOK camera evidence, not a new physical-camera acceptance claim.

Actual Quest camera permission/return, frame orientation/color, source switching,
sharing latency and sustained performance remain open under the device hold.
The combined mixed-reality headset view is a separate unfinished source.

Validation for this increment: the full web suite passes 2,798 tests in 305 files;
three additional physical Live-provenance cases pass with all 34 tests in their
affected suites. Unity passes 1,032 EditMode and 918 PlayMode tests (three expected
optional private-model skips), plus ten focused camera tests on the final source.
Android discovers 107 tests: 105 pass and two optional private-import fixtures
skip; release AAR build and lint pass. Final production web build, lint, probe
typing, prompt ownership, core boundaries and catalog provenance checks pass.
The catalog definitions are unchanged; only native source provenance updates.
