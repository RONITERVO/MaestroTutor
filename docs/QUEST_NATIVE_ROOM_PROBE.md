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
