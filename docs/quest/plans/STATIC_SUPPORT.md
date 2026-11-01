# Planned static support from authored collision

Status: **planned and unimplemented**, reviewed 2026-11-01. This bounded next slice
adds explicit static support to ordinary creations; the ground-cache optimization
alone does not provide it. Use [current status](../../QUEST_STATUS.md) for delivery
and holds, and [accepted scope](../../QUEST_V1_SCOPE.md) for the product direction.
This page is an implementation contract, not another release checklist.

## Shared intent and compatibility

Add a versioned, default-off support component through the shared capability
catalog, generated controls and normal prepared edit/creation transaction. User,
agent and programs read the same revision, settings, readiness and bounded reason.
Preserve save completion, Undo/Redo, temporary work, copies, prototypes, blueprints
and archives; inspection must not activate geometry or perform an action.

A platform/stair template or agent fulfilling an explicit construction request
should author compatible collision and support together. Learners should not need
to configure every block manually. Retain this accepted intent in editable data;
do not infer support from an object's name or appearance. Default-off preserves old
ordinary props. Existing height-field and imported mesh defaults/APIs stay intact
through adapters to one effective support descriptor, without competing flags.

## Exact sources and static eligibility

The descriptor owns object identity, provider kind, stable source/shape identity,
accepted revision/generation and exact collider handles. Publish or retire only
that provider's registrations. `RoomItem.SolidColliders` validates registration but
cannot discover primary ownership: [SetCollisionShape](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Creation/CreatedRoomObject.cs)
appends height-field collision to the selected primary set. Terrain/imported-rigid
providers keep separate ownership even when sharing publication code.

Reuse actual BoxCollider or compatible mesh geometry; add no duplicate collider,
Rigidbody or invisible floor. Select the whole primary source; refuse unsupported
sphere/capsule or mixed sets rather than accepting a partial floor. Pointers,
triggers, selection volumes, barriers and render-only geometry remain ineligible.
Imported proxies require an explicit compatible collision recipe; exact imported
mesh support retains its current provider and restrictions.

[RecipeVisual](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Creation/RecipeVisual.cs)
removes part colliders, while a default assembly has one enclosing box. Decorative
stairs therefore need explicit compound tread boxes, not that enclosing box.
Visual part animation does not move root-local collision. Preserve stable authored
shape identities; generated pieces are private subhandles, not saved list indices.

Require authored `Fixed` physics, matching runtime policy, compatible geometry,
no incompatible saved root motion and current ownership/restoration eligibility.
[RigidRoomItem.Refresh](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Interaction/RigidRoomItem.cs)
also makes unavailable dynamic and animation-owned bodies kinematic; transient
`isKinematic` alone must never admit support. Enforce this across every writer:

- Reject incompatible physics, collision or root-motion edits while support is
  enabled, or accept one explicit transaction disabling support with that edit.
- Prepare compatible geometry/scale/pose replacements and atomically swap handles.
- Suspend support during grip, manipulation or transient animation/carried ownership,
  retaining saved intent. Revalidate policy, pose and source before republishing.
- Never restart stopped actions on revalidation or silently change legacy terrain/
  imported operational semantics through a broader shared predicate.

## Bounds, readiness and publication

[Authored bounds](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Creation/RoomAuthoredBounds.cs)
currently describe visuals; [ground readiness bounds](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Creation/RoomGroundBounds.cs)
skip collision extents for authored-only metadata. Offset platforms/treads require
a conservative support envelope derived from accepted saved collision sources,
properly transformed and unioned, or unknown bounds. Metadata cannot grant readiness.

Drive classification, ground/movement lookup, dependency acquisition/retention,
foot queries, navigation and physics columns from the same descriptor. Validate
all selected native handles and source revisions. Empty collision can be query-
complete without being ground; missing/disabled/pending upper support cannot borrow
a lower resident floor. Actual rays preserve holes and interiors, not AABB columns.

Prepare detached, reserve before acceptance, then publish source/handles/readiness
atomically. Exact edits and live ownership changes invalidate affected consumers.
Failed projection preserves snapshots/cursors; stale completion cannot publish.
Retirement releases owned registrations, never another provider's geometry.

## Work and memory admission

Keep existing object, shape, terrain/import and native limits, including four
height fields and four imported room objects. No cap increase belongs to this slice.
[Navigation builds](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Interaction/RoomNavigationBuild.cs)
copy source meshes before asynchronous baking; two concurrent builds is not a
memory budget. Before settings/data/copies or support publication, reserve checked
expanded-source, unique-mesh, triangle and copy-byte costs for accepted geometry.
Deduplicate mesh copies per build while charging each transformed source instance;
unknown/unreadable/overflowing cost refuses visibly. Include concurrent builds,
installed/retained maps and cancelled copies awaiting deferred destruction, with
owned transfer and exactly-once release. Failure preserves accepted work.

Input-copy bytes do not measure all native build/output memory. Choose conservative
map charges and explicit limits using focused evidence. Keep traversal/cast bounds;
no whole-world synchronization, frame-rate or large-world acceptance follows.

## Focused acceptance and boundaries

Test exact physical collider identity across contacts/rays/feet/navigation/columns;
fixed blocks and explicit compound stairs; mixed provider ownership and stale handles;
all writer transitions; cold offset/unknown/empty/missing upper sources; cancellation,
dormancy and failed replacement; shared user/agent guards and save/copy/Undo recovery;
pre-copy refusal, retained/concurrent builds, transfer and deferred release. Exercise
real 32/33 overlay compaction once ordinary sources make it reachable within caps.

Keep existing 10 cm step, 25 degree slope, body/footprint clearance, water and real/
virtual collision policies. A valid platform need not yield a route: report narrow,
steep, obstructed or pending navigation honestly. Moving platforms, carry velocity,
automatic arbitrary stairs, per-face roles, crowd routing and streaming are outside
this increment; they remain separate design and acceptance work.
