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
