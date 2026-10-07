# Full-app native room probe

Development integration boundary, 2026-10-02. The book and local headless adapter
now use the same RoomAgentClient in core-sdk. The platform module retains browser
registration only. The adapter exchanges the production version-2 requests and
native observations with a real MaestroRoom running in a dedicated batch Editor.
There is no replacement scene simulation or mock action executor in this journey.

## Run and evidence

`unity/Tools/Verify-Quest.ps1` runs the full-room headless and live-book journeys
after EditMode and PlayMode tests. Both development and release package helpers use that verification.
A standalone rerun after verification is available with:

```powershell
./unity/Tools/Run-QuestRoomProbe.ps1 -Editor '<Unity.exe>' -BuildMirror '<owned mirror>'
```

The wrapper first runs the same strict driver type-check as CI, then checks mirror
ownership, matching native source files and absence of another Editor using that mirror. It starts one hidden Editor and uses a fresh
`.quest-evidence/native-room/<id>` directory. The entire workspace and global
room preferences use that directory. Existing user rooms are not read or changed.
The scene starts empty and composes the actual MaestroRoom, including the bundled
avatar, native catalog, workspace services and action executor.

The file protocol is Editor-only, explicitly started, session-bound and bounded.
It exposes exchange and stop operations through the production client rather than
accepting scripts or arbitrary paths. The file adapter opens no socket/server, and the Editor
adapter and its storage override are excluded from the Android player. Atomic
replacement tolerates transient Windows file contention. Repeated state files do
not refresh the client's native-observation freshness. Client IDs prevent a
pre-handshake observation from being accepted by a new connection.

The default journey does not contact an AI provider. It discovers object.create,
creates a real ball through the shared execution/receipt path, checks the returned
object ID, paints it, verifies Undo restores its colour, verifies another Undo
removes it, and reads native resource diagnostics. It then creates a lathe cup,
inspects its exact native recipe, edits its outline/subdivisions, reads paged profile
facts, and verifies Undo of both editing and creation. It retains initial state,
observations and outcomes in journey.json. verified.json requires both child
processes and the native terminal receipt to report success. An invalid observation
is retained as rejected-state.json before cancellation, so a later native update
cannot overwrite the diagnostic. The client reports the transport failure instead
of hiding it behind request cancellation. Failure stops only
the wrapper's owned Editor; it does not query or modify a headset.

The first full-app run revealed two production contract defects:

- A real complete room advertised 66 features, exceeding the client's obsolete
  64-feature limit. The shared inventory ceiling is now 256, retaining identifier,
  uniqueness, whole-message and other validation bounds.
- JsonUtility expanded an absent rules.memory into a default object without a
  session identity. The serializer now preserves null. A genuinely populated
  memory view still requires its valid scope; the web validator was not weakened.

The captured initial full-room observation is a web regression fixture. Unit
transport fixtures test framing, acknowledgements, stale files and identity
rejection; they are explicitly simulated transport tests, not native evidence.
The actual native journey is required by local build verification. Web CI runs
the shared-client/fixture tests but does not run Unity.

## Optional original-provider route

Passing `-Prompt` now runs the original conversational path: a real context-setting
chat turn, the requested tutor turn, the suggestion verifier, durable agent task,
native operations and final tutor reply in chat. It uses an isolated headless
profile; the old `-Profile` argument no longer selects existing personal history
for provider proofs. Unity contains no provider client or billing implementation.
The existing managed/BYOK environment selects the original application transport.
This opt-in route incurs normal provider/managed usage; keep secrets out of the
prompt and command line.

The context-setting turn defines “my test object” as a small blue ball named
ParityBall and requests no action yet. A useful prompt is “Please create my test
object now. Use the definition I gave in the previous message.” The real response
stream is observed without replacing its content. `provider-responses.json` is
private diagnostic evidence for these synthetic requests. `agent-journal.json`
retains the original inputs and actual native receipts on success; failure writes
`agent-failure.json`. `journey.room` requires provider usage, final chat delivery
and reconciled billing, and the probe checks that starting the same claimed task
again causes neither additional usage nor a new native revision. Inspect the
resulting scene for the requested name, shape and colour as well.

The planner's provider schema represents open-ended catalog argument objects as
JSON text. The shared decoder restores their typed nested objects/arrays before
the original command validation, durable task intent and native dispatch. Only
catalog inspection arguments, catalog check call arguments and execution-start
call arguments use this provider encoding. Catalog definitions, programs, stored
journals, native wire messages and human editors keep their ordinary object
format. Malformed inner JSON may use the existing bounded pre-dispatch correction;
oversized or non-object payloads fail, and no failed dispatch is retried by decoding.

For the repeatable semantic scenario, use
`-ProviderScenario ContextCreateEdit` instead of `-Prompt`. Set
MAESTRO_HEADLESS_ACCESS_MODE to managed or byok; BYOK reads only the explicitly
configured MAESTRO_GEMINI_API_KEY. The wrapper starts a fresh isolated room/profile
and the same real-provider conversation in either mode. The scenario checks the
native ball's name, shape, small scale and blue colour, then asks for a red edit
of that exact object without changing other objects. It exercises the ordinary
native Undo/Redo handlers, compares actual saved object properties, and asks the
agent to inspect the current room without editing it. This is native handler
parity, not an assertion that a physical controller was pressed.

provider-scenarios.json preserves each journey's accounting/coverage and the
before/edit/Undo/Redo/readback states. It only says passed after all semantic
checks succeed. An ad hoc Prompt run has no such semantic certification.
Provider exceptions retain billing settlement evidence; failed zero-cost calls
are valid billing evidence but never a successful agent journey.

### Event-driven program scenario

Use `-ProviderScenario EventProgram` with the same managed/BYOK configuration.
It needs no speech fixture. The real tutor/verifier/agent creates the contextual
half-size ParityBall, saves ParitySignal without starting it, and later starts
and stops the program through ordinary conversational requests. Native user-event
handlers supply two signals; the driver checks actual colours, private state,
timer waits, completion, a fresh-state restart and no effects after Stop.

`provider-program.json` records the exact editable source, each provider journey,
accounting and native observations. It says passed only after all 11 program
semantics succeed; the outer `verified.json` also requires duplicate prevention
and clean process exits. Save, task completion and program completion are distinct.
The generator may use interpreter Sleep or the native time.wait capability;
both expose their actual scheduler state. The top-level journey retains initial
creation evidence separately from save/start/stop billing windows.

This proves the common native handlers, not controller input or rendered block
editing. Parallel/module execution, animation, persistence and physical Quest
acceptance require their own scenarios. Keep provider transcripts and credentials
in private ignored evidence, never in committed test fixtures.

### Adaptive first-time learner session

`-ProviderScenario LearnerConversation` starts a fresh English-native / Spanish-
target headless profile connected to the actual Unity room. It waits for explicit
`learner-request.json` messages in its fresh evidence directory. Requests have a
consecutive `sequence`, a `type` (`text`, `live`, or `finish`) and, for turns, the
learner's natural `text`. Live also names a local `speechFixture` whose transcript
must exactly match that text. The shared chat, verifier, media tools, room task
host and Live path perform the work; there are no synthetic tool decisions.

A developer reads `learner-response.json` and adapts the next ordinary beginner
message to the reply. Do not feed capability IDs, schemas, hidden native facts or
technical solutions back as the learner. Record clarification and failure instead
of forcing the model to produce a prescribed tool call. Stop during a turn is
available through `learner-control.json` with `stopSequence` matching that turn.
`finish` closes the owned session. The collector keeps actual messages, exported
artifacts, native samples, task journals, usage and per-turn managed settlement.
It also retains up to 50 recent provider response-text entries per turn (32,000
characters per entry), without request/media payloads or credentials, so a
rejected planner proposal can be distinguished from a native action failure.

Optional `-SyntheticRoomScan` installs an explicit Editor-only platform fixture
with a known FLOOR plane, initially unloaded. A real `room.environment.set` load
must run before scan facts become available. Native setup, placement, persistence
and receipts are unchanged. `ready.json` records `syntheticScan`, and
`synthetic-scan.json` records load/scan counts. This option is restricted to this
learner scenario; it cannot verify an OS permission dialog or physical alignment.

This mode has a one-hour Editor-only lifetime; other probes retain fifteen minutes.
It samples the shared client's passive snapshot during pending commands, without
trying to acquire an execution lease. Its floor is explicitly synthetic. A normal
collector exit means evidence was collected, **not** that the learner's requests
succeeded. Review semantics, generated artifact interaction, beginner usability,
voice understanding and the later real-headset session separately. Existing payer
credentials do not certify first-time account registration or Quest sign-in.

Proactive helpful artifacts and multiple complementary outputs are intended
product behavior. Evaluate their usefulness, accuracy and interaction together;
do not classify an unrequested music/image/artifact/tool as a failure by itself.

To continue after an external limit, use `-ResumeLearnerRun <closed-run-id>`.
Only a cleanly finished owned learner run with the same access mode is accepted.
Its saved room and profile are copied into the new isolated run; the source stays
unchanged, native session identity changes, and previous commands are not replayed.
`resumedFrom` records this boundary. This is restored-state evidence, not proof of
an uninterrupted Live transition. New requests start at sequence 1. The wrapper's
`outcome` is `collected-requires-semantic-review`, including when no errors occur.

### Stop and task steering provider scenario

`-ProviderScenario TaskSteering` asks the real conversational agent to paint the
contextual ParityBall red, inspect it, then resize in a separate edit. The fixture
observes the first actual native paint acknowledgement and invokes the public
`room.stop` control before returning that unchanged receipt. It then uses ordinary
native edit handlers to paint green and change placement. Provider output and
native acknowledgements are never substituted.

The original task must be stopped, with the confirmed paint retained, no resize,
no result narration after Stop, and a stopped status projected into its source
chat state. Rendering that status is a separate browser/headset acceptance check.
The normal positive journey intentionally refuses that stopped outcome; the probe
checks the exact expected coverage differences and still requires real tutor,
verifier, planner usage and complete managed settlement. Reusing the same stopped
handoff cannot run or charge again.

Later conversational requests must resolve Revise to that exact stopped task and
Continue to the resulting revised task. Their recorded parent request/receipt
chains must match, the cancelled resize must stay cancelled, and painting blue
must preserve the manually changed position and original size. Continue is read
only. A fresh host/store restores those statuses; original claims and handoffs
cannot replay any actions or consume provider usage.

`provider-steering.json` retains the original paint, stop response, manual edit,
all task records, final results and billing, including failed runs. This tests
shared handlers and a deterministic acknowledgement boundary. It does not prove
physical grip arbitration, spoken Stop latency, process-crash recovery, missing
acknowledgements or headset UI acceptance.

### Physical launch provider scenario

`-ProviderScenario PhysicsLaunch` uses the original managed/BYOK chat,
verifier and task path for simulation start, one aimed launch and a later pause.
It starts the Editor adapter's explicitly synthetic floor and creates a fixed
wall using ordinary native object creation. These are labelled test geometry,
not an invented real scan or proof of room alignment.

The earlier contextual chat creates the half-size ParityBall. Fixture setup moves
it away from the viewer, sets solid spherical physics and adds the fixed wall
through ordinary user edit handlers. Those setup edits are not credited to the
agent. The provider then discovers current readiness, starts physics and lets gravity act; a second
request discovers trajectory readiness and launches toward the specified point.

The driver samples the existing native observation channel, whose positions are
actual live transforms. It does not animate, move or apply physics during flight.
It requires observed rise/fall, approach to the requested destination, floor
non-penetration, wall contact without penetration and settling. The native launch
receipt alone cannot pass those checks. Exact target/settings, unrelated objects and one launch are verified.
Reusing the original run receipt must return the same outcome without another
throw. A third conversational request pauses physics; the ball must stay still.

`provider-physics.json` preserves samples, native receipts, all three provider
journeys and their billing. Failed runs retain their error and original evidence.
The outer verified result also requires clean client/Editor exits and duplicate
handoff prevention. This is one sphere/trajectory against a flat synthetic floor
and fixed wall, not real controllers, scan permissions/alignment, imported colliders,
catching, general contact-driven gameplay or Quest performance proof.

### Library animation provider scenario

`-ProviderScenario AvatarAnimation` uses real chat/verification/agent requests to
save ParityMotion for the compatible downloaded Agree_Gesture clip, then start
that exact program. A contextual ParityBall remains in the room as an unrelated
object. The probe reads the saved source and ordinary native motion search,
compares exact shipped model/motion/rig identities, and requires one completed
run with unchanged source, objects, placement, activity and walking preferences.

For this opt-in scenario, the Editor-only adapter records actual displayed skin
bone transforms and the active library motion/run IDs in `avatar-playback.json`.
It never samples a clip, starts animation or replaces a provider response. Idle
identity changes and approximately 10 Hz active observations are retained up to
512 frames and 512 distinct skin bones; truncation fails the proof. Assertions
require at least 15 displayed bones, five changing joint rotations, full-duration
contiguous playback and a subsequent stop. They reject wrong IDs/rigs, frozen
skins, invalid joints and native playback errors. This checks the actual skin,
not just an accepted animation command. It is not a frame-rate or headset verdict.

`provider-animation.json` retains save/start journeys, accounting, exact source,
native final state and the observation summary. The outer `verified.json` also
requires duplicate prevention and clean client/Editor exits. This shipped-clip
scenario does not certify every imported rig, all 178 motions, gestures, follow,
authored room travel or physical joint posing; those need their own evidence.

The shared task runner reconciles repeated play proposals for an already accepted
program target/revision through native rules.inspect. An accepted Loading/Queued
receipt already started work. The journal records that real inspection and its
receipt; it cannot restart a completed or cancelled run to satisfy an agent retry.
The probe requires exactly one completed run, not merely one accepted play reply.
A later user task can start the saved program again.

## Composite module provider journey

`-ProviderScenario CompositeModule` uses the same managed/BYOK configuration and
ordinary chat/verifier/task pipeline. A real agent discovers the included Passive
spinner module and saves its exact immutable definition/hash in editable
ParitySpinners source. Saving must create no objects, buttons, bindings or runs.

After a later chat request starts it, two native branches must wait independently
under one unfinished parent. The harness sends user.spawnLeft, observes only the
left construction while the right branch still waits, then sends user.spawnRight
and requires parent completion. These are ordinary user-event handlers, not mock
program execution. Each construction has its requested position, editable recipe
parts, authored collision shapes, physics settings and an internal passive hinge
connecting only its own new members. One Undo removes each complete construction;
Redo restores exact identities, geometry and connections. Later signals cannot
restart a finished program. Existing room objects and avatar preferences stay intact.

`provider-composite.json` retains source, native facts, intermediate states and
real-provider usage for save/start turns. Source checks accept either reuse of one
pin or separate aliases of the identical pin. Recipe comparison tolerates native
float precision and JsonUtility's null/empty root-parent representation, but rejects
missing parts, changed hierarchy, different geometry or cross-construction links.
JSON syntax and invalid behaviour-source proposals can be corrected by the shared
planner within its existing planning limit. The invalid proposal never reaches
native dispatch or the native receipt journal. Unsupported actions, oversized
command envelopes and transport failures are not correction candidates. This is
shared browser/headless behavior, not a special provider-test retry.
Agent drafts can reference a successfully inspected module with module:null plus
its exact hash. The shared verified-copy authoring helper materializes the complete
pin before validating/journaling/sending the program. Source and native validators
still reject unresolved null imports. The visual editor uses the same pin copier;
references neither fetch assets nor grant resources/signals or change pinned code.
This proves authoring and native construction; it does not prove physical fidget
feel, scanned collisions, headset rendering or arbitrary generated recipe quality.

### Spoken and visual agent scenarios

`-ProviderScenario LiveVisual` and `-ProviderScenario ObserverVisual` run the
ordinary headless Live/observer turn and suggestion aftersteps against the same
native room. Both require `-SpeechFixture <absolute JSON path>`. The JSON contains
`sampleRate:16000`, mono PCM16LE `pcmBase64`, and a nonempty `expectedTranscript`.
An offline synthetic fixture can be prepared on Windows with an installed voice:

```powershell
powershell.exe -NoProfile -NonInteractive -File ./scripts/New-RoomAgentSpeechFixture.ps1 -OutputPath '<new fixture.json>'
./unity/Tools/Run-QuestRoomProbe.ps1 -Editor '<Unity.exe>' -BuildMirror '<owned mirror>' -ProviderScenario LiveVisual -SpeechFixture '<fixture.json>'
```

Choose `ObserverVisual` for the second route and repeat both with managed and
BYOK access. The fixture generator uses Windows PowerShell 5.1/System.Speech,
plays no sound, contacts no provider and refuses to overwrite an existing file.
Its default installed voice is Microsoft David Desktop; `-Voice` can select
another installed voice. Keep the generated audio and provider diagnostics local.
The 11.34-second reference fixture is synthetic, not a human microphone recording.

Earlier ordinary chat defines ParityBall and its diameter as exactly half the
standard ball, leaving the colour unspecified. The spoken request asks to create
that object using the colour of the camera reference. Only the synthetic JPEG
shows red; neither its label nor the speech nor the context names the colour.
The probe requires the real native name, shape, scale 0.5 and red colour, and
preservation of unrelated objects. A prior ambiguous “small” fixture could
legitimately select the standard 13 cm ball; that historical rejection remains
recorded rather than being reported as a product size failure.

The wrapper requires real-time input pacing, transcript recognition, model audio,
a natural spoken handoff without text-tool syntax, real model verification,
original context/media, completed native receipts, final chat projection and
usage/settlement. Instrumentation hashes actual successful Live client sends and
actual planner request media without replacing either. These must match the
journal's WAV/PCM and JPEG hashes. The scenario records its candidate evidence
before semantic checks and marks `phase:passed` only after they pass. The outer
`verified.json` also requires duplicate prevention and clean Editor termination.

Observer injection reproduces the local-speech-to-Live ownership transition; it
does not run the browser Whisper worker or certify physical microphone/camera,
headset rendering, tracking or comfort. The Live reply precedes the agent's final
chat result. This route proves delegated after-turn planning, not direct native
Live function calling or spoken playback of the final task result.

This does not replace the broader paired real-provider scenarios in
[QUEST_AGENT_RELEASE_COVERAGE.md](QUEST_AGENT_RELEASE_COVERAGE.md). A successful
basic creation run alone is not Live, program-authoring or headset release proof.

## Acceptance boundary

Verified locally: 1,942 web tests, lint and TypeScript checks; 653 native EditMode
and 425 PlayMode tests (three optional private-asset skips); real full-app
create/paint/Undo/readback and lathe editing through the shared client. One initial
client interruption was not reproduced in two standalone runs or a 16-cycle stress
journey (118 observations); this is not a claim that all device timing issues are
resolved. Set MAESTRO_ROOM_PROBE_REPEATS to 1–32 to repeat the geometry cycle (default 1).
The cycle now also discovers and edits compound collision, reads its counts and
wall shape, and undoes it before removing the visual object. collision-authoring.json
retains native catalog states and the exact execution for the Chrome generated-form
replay. That replay validates fields and call parity; it does not simulate PhysX.
The Chrome profile-editor replay uses captured native states and compares the
submitted call with the real native receipt. Its acknowledgement is simulated. APK and CI evidence for the
final commit are recorded separately in the delivery record and PR.

This is Editor integration, not proof of Android WebView texture/input, Quest
hands/controllers, passthrough/scan alignment, provider behaviour, headset frame
timing, device storage/power-loss behaviour, release signing or Store acceptance.
The same command semantics improve developer/user/agent parity, but cannot replace
rendered UI journeys and actual device interaction checks.

## Transient file locks

The desktop transport retries a locked observation read at most eight attempts
with bounded backoff. Byte limits, JSON validation, probe identity and shared
state validation still apply. This retry reads the same file; it does not create
or resend an action. A persistent lock cancels pending client work with an
explicit failure and never fabricates an acknowledgement. Deterministic tests
cover both a temporary Windows `EBUSY` read and an exhausted retry budget.

Storage writes are a separate boundary: a failed room save is still a failed
action. Native storage diagnostics record the failed stage and error code without
private file paths or document contents; the probe must not reinterpret that
failure as a transient transport read. Native file publication separately handles
bounded, known Windows replacement refusals before exposing a save result; see
[action recovery](QUEST_ACTION_RECOVERY.md). The transport still never resends a
mutation in response to a storage failure.

## Included structure watcher (2026-10-03)

The default journey now inspects the shipped **Structure state waits** module and
compares its exact definition with the shared `program-structure-watch.json`
fixture. It supplies the newly created structure's observed ID/revision, saves
without starting, explicitly starts, displaces a member, observes one transition,
resets and rearms, then stops and deletes the probe behaviour. Move/reset Undo
restores the earlier test's history. `program-structure-watch.json` in the evidence
directory retains module, source and every relevant native state. This journey
uses an explicit move; the separate PlayMode test covers an actual ball collision.
Neither route automatically resumes a cancelled run or calls an AI provider.


## Captured constructions (2026-10-03)

After the included lever journey, the client reads each live member's definition
revision, inspects and runs the shared capture action, waits for its receipt,
and reads the published module from the native library. It removes the original
members with Undo, rebuilds from the captured constructor, checks fresh identities
and an internal hinge, then undoes the complete copy and removes the probe's
library module. `construction-capture.json` retains the request, catalog definition,
receipt, module and before/after states. This checks the real transport/runtime;
PlayMode separately checks model-dependency failure/cancellation and stale capture.
It does not assert headset, provider or frame-time acceptance.


## Shared construction selection (2026-10-03)

The lever journey first selects its two native pieces through `room.selection.set`,
compares `room.selection` with the inline observation, and locates a member using
ordinary inspection. The same shared helper used by the book prepares the capture
arguments; independently queried object revisions must match. Removing the originals
must prune the selection and invalidate its old state ID. `construction-selection.json`
retains those real native states.

`probe-construction-selection-authoring.mjs` drives the optional book workshop in
Chrome against that recorded evidence: select named members, Locate,
review the seeded capture, load current guards, reject invalid input and explicitly
run. It compares both selection calls and capture with their exact native receipts.
This replay acknowledges recorded results; it is not another physics implementation
and does not establish physical controller, hand, reach or performance acceptance.

## Shared construction movement (2026-10-03)

Before capturing the selected lever, the journey shows its native construction
handle, compares `room.selection.manipulation` with the inline state and reads
both current placement facts. It calls `object.layout.transform` to move, turn and
resize the complete hinge assembly, verifies both results, restores both with one
Undo, then hides the handle. `construction-movement.json` retains these actual
observations and the exact action arguments. Physical grip ownership, two-hand
resizing and interruption are separately tested through Unity XR PlayMode.

## Physical connection probe

The full app configures a hinge through `object.connection.edit`, reads common
settings and hinge tuning separately, aligns it, and verifies live readback/Undo.
It then uses `attach` to join two pieces at their existing poses, reads their exact
IDs and break limits and verifies one Undo removes the join. `connection-authoring.json`
records those observations; `hinge-authoring.json` records the hinge path.

The Chrome connection fixtures replay these real states through generated controls,
requiring current revision guards, refusing negative break limits/self-links and
matching the native call and receipt. Native PlayMode tests separately exercise
actual PhysX breaks and delivery to the ordinary typed event scheduler. Neither
fixture claims headset or provider acceptance. Construction selection/capture now
runs before movement in the probe so each browser replay uses a contiguous series
of native action identities, without inventing intermediate receipts.

The slider journey creates the included spring-button blueprint, configures its spring target, reads saved tuning, explicitly aligns at a linear distance, verifies travel and Undo, then removes the construction with one Undo. `slider-authoring.json` feeds the generated Chrome form replay (`scripts/probe-slider-authoring.mjs`). Physical pressing and the shared program response are tested separately in Unity PlayMode; no headset/provider claim follows from these checks.

## Explicit snap-point journey (2026-10-03)

The app configures root-local points on two existing objects, reads their IDs and
current placements, and executes `object.layout.snap` with a quarter-turn and an
explicit fixed join. It checks aligned origins, exact connection identity/break
limits and complete-pose plus connection restoration with one Undo. Point edits
are then undone separately. `snap-authoring.json` feeds the production generated
book form replay in `scripts/probe-snap-authoring.mjs`. Both revisions are required;
invalid turns are refused and the accepted call and receipt match native evidence.
Physical proximity previews and headset acceptance remain separate future checks.

## Physical catching journey (2026-10-04)

The deterministic no-provider journey opts into a clearly labelled synthetic floor
through `MAESTRO_ROOM_PROBE_PHYSICS=1`. The Editor adapter creates it only in its
fresh isolated probe scene; `ready.json` records `syntheticPhysics`. Physics stays
paused until the ordinary shared action explicitly starts it. The optional real
provider route does not enable this fixture. This is not a captured room scan.

The journey creates a recipe socket and ball, discovers `object.physics.catch`,
checks its refusal with physics paused and readiness after starting, and observes
its waiting fact. Before starting the catch, the probe observes the ball settling
on the synthetic floor for longer than one periodic placement capture. This
prevents normal gravity and physics autosave from invalidating the fixture before
its one launch; stale-target guards remain unchanged. A separate shared launch
sends the free ball into the socket. Refused commands stop the journey without
retry, retaining `refused-command.json` and `journey-failure.json`.
The app must physically catch, hold and drop it and retain both named resources
in the completed receipt. Replaying that exact receipt cannot start another catch.
The journey pauses physics and removes its creations afterwards.

`physical-catching.json` is retained as the book fixture, and a web regression test
passes all ten native observations through `RoomAgentClient`. Run
`scripts/probe-physical-catching.mjs` against the local Vite fixture to check the
generated form, bounded inputs, availability and exact command/result identity.
The browser replays those observations; native PlayMode separately verifies the
shipped Meshy rig's actual flight interception, rotation-only reaching, human
priority, obstruction, timeout, cancellation and typed event delivery. No provider
or Quest acceptance is inferred from desktop success.


## Live book and original-chat journey (2026-10-04)

`Run-QuestRoomProbe.ps1 -Journey Book` starts a separate fresh native app and runs
`scripts/probe-native-book.ts`. The runner owns a headless Chrome context and a
loopback Vite server with an ephemeral port, isolated optimizer cache and no file
watcher. It requires installed Chrome and repository npm dependencies. It needs no
already-open browser tab or dev server. Vite and Chrome close before the owned
Editor shuts down; success still requires both processes and the terminal receipt.

The development-only `quest-native-book.html` fixture mounts the production
QuestBookSurface, ChatInterface and useTutorConversation. The same raw file channel
used by HeadlessRoomTransport carries its actual bridge snapshots, native states
and separate image payloads. It introduces no second RoomAgentClient, native scene,
action executor, simulated acknowledgement or replay fixture. Only the owned
fixture's top-level frame may call the runner's native binding. The fixture refuses
to start without that binding or outside development mode.

The real UI journey:

1. Open the workshop, create a multipart robot, change its part colour through the
   inspector, then Undo the edit and creation. Check the native recipe and object ID.
2. Type an ordinary chat request. Original tutor response, tool verification,
   delegated planning, task journal and result narration use the existing browser
   provider client and chat coordinator.
3. Discover the creation capability and create a ball through the native catalog.
   Hold the next planner response while the user changes that same ball in the
   book. Assert chat input remains available while the agent is working.
4. Deliver the now-stale paint plan. Require Unity's refusal, retain the user's
   colour, and inspect the recorded refusal in the original chat's task details.
   Verify the original request and native receipts reached the planner HTTP input.
5. Run `room.view.capture` through the generated book form. The displayed JPEG's
   SHA-256 must match the actual native receipt; preserve pixels separately.
6. Reload the page with the same disposable IndexedDB. The saved result must
   return without new room commands or another planner request.

Every provider response is explicitly scripted local SSE at the browser network
boundary. A deliberately invalid fixture token is used; no real credentials or
provider requests are needed. All other external requests are blocked. This tests
handoff and coediting mechanics, not model reasoning, account eligibility, managed
billing or provider access. Voice, camera and hardware hooks are inactive. The
snapshot is the virtual room at the Editor viewer pose, not passthrough or proof
that an agent understands the picture.

`book-journey.json` retains native observations, requests, task operations and
scripted-provider request bodies. Screenshots show the original book editors and
chat result; `book-native-view.jpg` preserves the actual image. `book-failure.json`
and a screenshot retain failed runs. The successful desktop run recorded 19 room
requests and 63 distinct native observations, with all seven provider responses
scripted. Its native runtime is the previously verified physical-catching runtime;
this verification increment does not require or claim a new installed APK.

The first self-contained server attempt timed out during a real browser click.
The fixture server was then isolated from development file watching and optimizer
cache sharing, and the full journey passed. This is a test-harness change, not a
claim that device input timing is resolved. Raw-channel regression tests also
cover old-document states, invalid/oversized requests and overlapping writers.

Web CI checks those transport contracts. Local Unity verification additionally
requires both real-app journeys; it still does not replace the remaining device,
real-provider, signing or Store acceptance gates.


The catalog-workflow increment extends the live-book task through six actual
native discovery/check queries, creation, stale paint refusal and a ninth final
planner call. Its assertions read remaining budgets from the real HTTP prompt.
The earlier 19-request run remains historical evidence; current run counts are
recorded with each checkpoint. The harness retains full browser exception stacks.
Playwright 1.62.1's injected service-worker blocker reads a getter that throws in
opaque sandbox frames, so this runner uses an equivalent registration blocker
that catches only that SecurityError. App sandbox permissions and the requirement
for no uncaught page errors are unchanged.


## Resting-ball launch regression (2026-10-04)

The full native catch probe now waits for a settled ball before its single throw,
retaining command/observation evidence on refusal instead of retrying effects.
This exposed a padded-sweep contact at distance zero on the supporting floor.
The native trajectory check permits only an initial separating contact with a
convex collider: the unpadded volume must already clear penetration checks and
its motion must move outward from the closest supporting plane. Every other hit,
later segment, endpoint and head-clearance check remains active. A real settled
PhysX ball now exercises the launch test; adjacent walls, penetration and downward
flight have explicit refusal coverage. Concave surfaces retain conservative checks.


## Carried-material transfer journey (2026-10-04)

The full native journey creates a released carrier with a measured material store,
discovers `object.material.transfer`, loads both current revisions, takes material
from a field, deposits part of it back and reads both actual balances after each
operation. Two separate Undo operations restore the take state and then the
original field/store state. `material-transfer-authoring.json` retains the exact
native calls, receipts and readbacks. The carrier and its configuration are then
undone so later journey steps start from their intended room.

`probe-material-transfer-authoring.mjs` replays those captured observations in the
ordinary catalog editor. It checks endpoint variants, both current-revision reads,
same-object and oversized-amount refusal, and the exact emitted call and native
result. It does not run a browser material simulation. This authoring check does
not claim physical shovel contact, carrying visuals, hand packing, provider
reasoning or headset acceptance; those are separate adapters and release gates.


## Paused-physics cleanup boundary (2026-10-04)

After the catch/drop cycle, the probe explicitly pauses physics and observes all
object revisions/poses for a full placement-capture interval before its single
cleanup Undo. Pause freezes bodies but intentionally does not save placements;
the room's periodic capture can still publish the last dropped pose. The
`paused` and `pausedSettling` observations preserve that boundary. A stale refusal
still fails the journey and is never retried. This closes a harness race exposed
by a correctly refused global Undo; native revision guards are unchanged.


### Material scoop authoring

The native journey now creates the editable Material scoop template, reads its
saved tip, edits the measured dose through `object.sculptTip.edit`, checks that
configuration neither creates material nor starts contact, and undoes the edit.
`material-scoop-authoring.json` contains those actual native observations for the
Chrome form replay. Real held take/deposit, obstruction, manual takeover, stale
drafts, save failure, temporary discard and paired Undo use Unity PlayMode tests.
A replay is a form/contract check, not a browser physics simulation or headset test.


## Typed integration drivers and complete placement evidence (2026-10-04)

`npm run verify:quest-probes` type-checks both native journey drivers, their shared
assertions and the real book fixture against the production client and browser
contracts. It inherits the app's strict compiler settings; ES2022 library types
cover the Node runner. The release-gate workflow runs this check, and the native
wrapper refuses to start its Editor if it fails. The fixture's actual declarations
provide the browser evidence types; no second browser bridge interface is maintained.

The initial check found 118 room-driver and 31 book-driver diagnostics. Catalog
queries now retain their request-specific reply type. Before narrowing, the driver
checks the operation, category, identity/version and fact arguments, search page or
complete checked action. This follows the production client's wire validation and
cannot turn a missing/unavailable fact into a successful observation. Dynamic fact
helpers check the reply category; captured construction programs pass the shared
program parser before extracting their creation action.

Two old rotation assertions compared a nonexistent summary field on both sides,
so they could pass with two undefined values. Fixed joining and snap Undo now read
`object.placement` for each member and retain the actual before/after facts in
`connection-authoring.json` and `snap-authoring.json`. Comparisons require finite
position/scale and a normalized quaternion, reject missing/mismatched targets and
check all three pose components. Equivalent quaternion signs and small floating-
point round-off are accepted. Focused regressions include missing rotations and
rotation-only changes. These checks verify the native world; they do not add a
second simulation or replace physical Quest acceptance.


## Physical packing settings and gestures (2026-10-04)

The full native journey now discovers `material.pack.tool.set`, enables a bounded
amount/radius/mass, reads the actual settings and confirms no ball or gesture was
created merely by choosing them, then disables the tool. The exact native requests
and acknowledgements are in `physical-packing-authoring.json`.
`probe-physical-packing-authoring.mjs` replays those replies through the normal
book capability form, rejects an oversized dose and zero mass, and matches the
emitted call and receipt. This is not a browser physics simulation.

Eight Unity PlayMode scenarios cover actual finger/trigger capture, one measured
ball, accepted collision during preview, atomic save/Undo, replay, obstruction at
start/publication, rotated/scaled surfaces, capacity, human ownership, tracking/
pause recovery, moved-source refusal, real XRI grabbing and temporary discard.
Native preview/result and solid-tray renders are inspected separately. Full native
suites passed 805 EditMode and 620 PlayMode cases, with three optional private-file
skips. The shared-client room journey passed 462 observations. These checks do not
certify Quest tracking feel, scanned-room alignment or sustained performance.


## Real-provider book journey

`-Journey Book -ProviderScenario ContextCreateEdit` uses the real rendered
QuestBookSurface and ChatInterface/useTutorConversation with a real Unity room
and the selected managed or BYOK provider. The existing Book journey without
ProviderScenario remains deterministic and offline.

The runner enters a context-only message through the chat composer, then asks
for creation and a later colour edit. It verifies the exact task handoff, real
verifier decision, provider output/usage, visible working/result state, editable
chat during work, native object identity/geometry/colour and preserved unrelated
objects. Workshop Undo/Redo uses the ordinary controls. Reload must retain the
same saved task journal and result without any new provider request or room action.

Only test credential setup is supplied through an owned top-level fixture binding.
Managed calls still use the browser backend service and the real staging backend;
BYOK uses the browser Gemini client. Provider replies and native states are never
fabricated. A Fetch response clone records UTF-8 stream evidence without changing
the original Response. Network access is limited to the local fixture and the
selected generation endpoint; headers, keys and tokens are not stored in evidence.
The browser context is disposable. No credentials are added to compiled assets.
Managed staging uses its already trusted `http://localhost` origin (port 80);
the runner refuses a busy port and does not weaken CORS or browser security.

Managed billing covers the whole browser journey and settles on failure as well
as success. This does not test interactive sign-in, Quest attestation, physical
input, Android WebView texture or headset rendering. Keep these boundaries in the
release evidence and retain failed attempts separately.

The result-reply gate also rejects another tool handoff after native completion.
The shared reply provider distinguishes the original action from the current
request to report its result, with at most one reply-only correction. This never
replays native work. The probe verifies the final visible text against the original
provider stream using the normal tutor parser, and checks tool fences separately.
