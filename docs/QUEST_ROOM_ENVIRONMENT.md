# Shared room setup

`room.environment.set` and `room.environment` share the physical Load room, Scan
room and Show room service. `roomEnvironment.v1` is advertised when that service
is attached. The book generates controls from the catalog, and Maestro discovers
the same capability through its existing connection. No scene geometry is sent in
these observations. No new model, account or provider path is introduced.

Read the fact for an exact `stateId`. Operations are:

- `load`: request room-access permission and load an existing saved Meta scene.
  Missing scene data does not automatically open the room-setup screen.
- `scan`: explicitly request Meta room setup, then load its successful result.
- `show` / `hide`: change the visible scan surfaces. Virtual view keeps them
  visible; return to MR before hiding them.
- `cancel`: also pass the exact current `requestId`. Abandon the request's
  continuation; an OS screen still has to be closed in that screen. This cannot
  undo a room scan already saved by Meta.

A completed durable action receipt means the request was accepted, not that the
room has been scanned or is aligned. Read `phase`, `busy` and `requestId` for
progress. `availability` contains supported/active/virtualView/canLoad/canScan/
canCancel; `surfaces` contains showing/visible/ready/physicsRunning. Visible
describes requested or virtual-view-forced visibility, not proof that meshes are
loaded or rendered. Status/reason are bounded display text. The two grouped records
keep the normal program-value limits. A fact read and readiness check do not start work or update identities.

Load/Scan pause physics and invalidate the current room surfaces before requesting
permission. Native collider/tracking checks determine readiness after loading.
Loaded data, ready colliders and a clear navigation path are different things.
The user must check that visible floor/wall geometry aligns with the actual room.
Nothing starts physics or restarts movement automatically. Use the separate
`physics.simulation.set` action only on explicit intent. Unrelated actors are not
reserved by the setup action; normal app focus/lifecycle rules still apply when a
system screen takes focus.

The physical tray uses the same request service. Its Scan room button becomes
Cancel setup while a request can be cancelled, then Wait for system while a
cancelled worker drains. Load remains unavailable until the real operation ends.
No Maestro room document, Undo entry or imported object changes are made by setup.

## Request lifetime

Permission and Meta setup screens intentionally take focus. Their explicit
requests may return, but saved geometry is loaded only after returning to the
active MR app. Focus loss during geometry loading, disabled components, virtual
view and workspace holds cancel the continuation. Late results do not mark the
room ready or start another phase. Component destruction detaches scene callbacks
and discards continuation. Workspace changes retain the persistent scan service,
so an old platform operation cannot gain admission through replacement content.

A timeout reports failure but retains ownership of the pending platform task until
it actually drains. Cancellation also retains that ownership. New requests cannot
overlap the retired worker. Duplicate durable receipts never reopen permission,
scan or loading. Stop on a completed receipt does not cancel the service; request
cancel explicitly. There is no automatic retry after denial, cancellation, timeout
or lifecycle interruption.

## Evidence and remaining acceptance

Native tests substitute only the OS/SDK boundary and use the real scan coordinator,
physics world, catalog and durable execution path. They cover request acceptance,
permission/scan focus handoffs, no implicit scan on Load, exact cancellation,
stale visibility, virtual-view constraints, workspace/lifecycle interruption,
denial, scan cancellation, timeout and drain. The generated native observations
are validated in the web contract and replayed through the book's typed controls.
Browser replay cannot prove Meta OS permission UI, actual scene capture, tracking
alignment or device latency. Those remain physical Quest acceptance work; device
installation/testing remains on hold.

## Shared scanned layout inspection (2026-10-04)

`roomScanLayout.v1` adds three read-only facts to the same catalog used by the
book's inspector, agent and programs:

- `room.scan`: availability, current snapshot `stateId`, stable Meta `roomId`,
  supported surface count, omitted mesh-only/non-bounded anchor count and reason.
- `room.scan.surfaces {stateId, offset}`: four exact anchor IDs, semantic labels
  and plane/volume-presence flags, ordered by ID. The exact end is an empty list;
  an offset past it is unavailable.
- `room.scan.surface {stateId, id}`: room-local position/quaternion and anchor-local
  plane rectangle and volume-box center/size, each with a `present` flag.

Read status before details. State IDs are ephemeral and change when observed
geometry, room identity, setup or the coordinate frame changes. Meta UUIDs are
stable identifiers for the loaded scan, not a promise they survive a new scan.
Missing surfaces never match by label, proximity or array index. Snapshots are
captured on demand and shared only within one Unity frame. Changes detected on
subsequent reads invalidate old page/detail requests. Background room observations
do not contain these details, and reading does not request permissions, load a
scan, start physics, move objects, save or create an Undo entry.

The service requires an active tracked mixed-reality room with accepted loaded
geometry, ready floor/wall colliders and no setup or workspace hold. Virtual view,
tracking/lifecycle loss and malformed or oversized scan data make it unavailable.
Unavailable zero counts and empty IDs are placeholders, not an empty real room.
At most 128 bounded surfaces from 256 SDK anchors are accepted; exceeding either
limit refuses the whole layout rather than returning a seemingly complete subset.
The scanned-ink extension also admits at most 256 outline points per surface
and 4,096 total for local fit checks; they are never returned by these facts.
Each returned program value stays within the existing 1,024-character cost limit.
Non-unit coordinate frames or anchor scale are unsupported.

Coordinates matter: Meta planes lie in local XY at z=0 and face local **+Z**;
Maestro drawing patches face **-Z**. Bounds remain in the anchor's local metres;
apply its returned pose to use them in room coordinates. Rectangles and boxes do
not describe polygon boundaries, holes, cutouts, raw global meshes, moving objects,
free space or verified alignment. These facts do not implement persistent drawing
attachments or certify a safe placement. The separate [scanned ink layer](QUEST_SURFACE_DRAWING.md#saved-scanned-ink-layers-2026-10-05) capability supplies
explicit attachment, missing-anchor and lifecycle rules.

When a task explicitly queries layout, the existing Maestro AI connection can
receive it, with the same task-history/backup retention. It can reveal room size
and arrangement even without raw meshes or camera frames. Prepared privacy
copy now describes this; it is not yet deployed. Native local reads alone do not
send information to a provider. No real scans or provider calls are used in the
offline verification.

Verification: five native PlayMode cases cover detached snapshots, sorted/empty
pages, bounds, transformed coordinate frames, stale identities, lifecycle/runtime
holds, malformed/oversized scans and no setup/physics/save side effects. The real
native result fixture is also validated by three shared-client/program tests. The
full native-app desktop journey reads these catalog definitions and verifies that
no physical layout is invented on desktop. The SDK source is controlled in tests;
real Meta room data, headset alignment and performance remain unverified for this
increment. Layout inspection and saved ink are now included in the audited
development APK recorded in [the delivery record](QUEST_V1_PLAN.md), not installed.
