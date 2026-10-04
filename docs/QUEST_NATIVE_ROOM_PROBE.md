# Full-app native room probe

Development integration boundary, 2026-10-02. The book and local headless adapter
now use the same RoomAgentClient in core-sdk. The platform module retains browser
registration only. The adapter exchanges the production version-2 requests and
native observations with a real MaestroRoom running in a dedicated batch Editor.
There is no replacement scene simulation or mock action executor in this journey.

## Run and evidence

`unity/Tools/Verify-Quest.ps1` now runs the full-room journey after EditMode and
PlayMode tests. Both development and release package helpers use that verification.
A standalone rerun after verification is available with:

```powershell
./unity/Tools/Run-QuestRoomProbe.ps1 -Editor '<Unity.exe>' -BuildMirror '<owned mirror>'
```

The wrapper checks mirror ownership, matching native source files and absence of
another Editor using that mirror. It starts one hidden Editor and uses a fresh
`.quest-evidence/native-room/<id>` directory. The entire workspace and global
room preferences use that directory. Existing user rooms are not read or changed.
The scene starts empty and composes the actual MaestroRoom, including the bundled
avatar, native catalog, workspace services and action executor.

The file protocol is Editor-only, explicitly started, session-bound and bounded.
It exposes exchange and stop operations through the production client rather than
accepting scripts or arbitrary paths. No socket/server is opened, and the Editor
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

Passing `-Prompt` and optionally `-Profile` uses createHeadlessClient and the
original core runRoomActionTask against that same native lease. It adds no Gemini
SDK, account, subscription or billing implementation in Unity. This opt-in route
can use the existing configured headless profile/environment and may incur normal
provider/managed usage. Do not put secrets into the prompt or command line.

This increment did not run a provider prompt or read provider credentials. A future
provider journey must inspect its usage, actual receipts and final world state;
a returned task result alone does not prove the requested semantics. The optional
route starts with empty chat history and English native-language context. It does
not test chat handoff, Live audio/video, account UI or subscription enforcement.

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
its waiting fact. A separate shared launch sends the free ball into the socket.
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
