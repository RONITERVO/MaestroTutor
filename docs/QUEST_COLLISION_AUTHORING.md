# Editable compound collision shapes

`collisionShapes.v1` adds an independent, version-1 collision recipe to created
objects. Recipe geometry, drawings and imported GLB/VRM objects share it. The
built-in book and Maestro retain their existing interaction setup.

## Shared authoring

Discover `object.collision.edit` in the catalog. Read `object.collision` for the
current revision, shape count and reserved piece cost, then read each
`object.collision.shape` at that revision/index before replacing existing shapes.
The action replaces the complete collision recipe and chooses automatic mode.
An empty `shapes` list clears it. Generated book fields and program calls use the
same schema, handler, exact-revision check and receipt path as the agent.

A recipe contains `version: 1` and up to 16 uniquely named shapes. Each shape has
an `id`, `shape`, `position`, `rotation` quaternion and `size`. Positions and
sizes are local metres in the whole object's root coordinates. Box and sphere
need no extra fields; sphere dimensions must match. Cylinder and ring use the
local Y axis and 8–24 `segments`. A ring's `innerRadius` is normalized against
its outer radius of 0.5. Its narrowest local wall must be at least 0.005 metres.
A 0.0000001-metre calculation tolerance keeps the minimum consistent across
web doubles and Unity floats. Positions stay within 2 metres; dimensions are 0.005–2 metres; combined extent
stays within 3 metres. Whole-object scaling applies to these dimensions.

Each ring sector becomes one convex prism; other shapes cost one piece. Admission
reserves at most 64 pieces per object and 512 across the room, including custom
recipes currently bypassed by a box/sphere override. An ordinary created object
reserves one piece. The built-ins are outside this custom-geometry budget.
These limits bound growth; they are not a Quest frame-time guarantee.

## Physical and save behaviour

All shapes attach to the object's existing single Rigidbody and grab interactable.
A cylinder floor plus a ring wall produces a cup with an open interior for solid
objects. Mass and fixed/solid/bouncy settings remain separate. Existing explicit
box/sphere choices bypass the recipe without deleting it; automatic restores it.
An edit does not start physics, change the material, or configure liquid capacity.

Shapes rotate/move/scale with the whole object. They do **not** follow animated
visual parts, infer hollowness from imported meshes, or resize automatically when
visual recipes change. Physical hinges, articulated members and fluid transfers
are separate components. Create geometry then configure collision as two edits;
atomic default-template creation is later work.

Held/owned targets and active animation-authoring sessions refuse edits. Accepted
changes share save-before-apply, temporary-room behaviour, Undo/Redo, copying and
receipts. Stale revisions and failed saves leave the prior shapes active. Replaced
geometry releases its native meshes. Unknown saved collision versions remain
invalid and preserved for recovery; they are never silently treated as absent.
Current room storage protects the whole unreadable room. Per-object quarantine
for future component versions is still release-hardening work.

## Acceptance

Native fixtures and tests cover the shared bounded contract, room admission,
save/load, default built-in absence, future-version rejection, shape reads,
copy isolation, Undo/Redo, mesh disposal, grabbing and failed edits. PlayMode
also drops a ball into the cup, pushes it against the wall, and throws the
compound cup onto a synthetic floor. The full Editor room journey uses the
shared client to edit/read/undo the same component. Chrome authoring replays
captured native states and matches its generated call to the native receipt.

Those checks are desktop evidence. Current Quest 3 input, compound-physics
performance and scanned-room alignment still require device acceptance.
