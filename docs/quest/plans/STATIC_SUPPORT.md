# Static support from authored collision

Status: implemented; desktop integration verified. See [current
status](../../QUEST_STATUS.md) for release gates and holds, and [accepted
scope](../../QUEST_V1_SCOPE.md) for the product direction. This contract does not
establish Quest performance or release readiness.

## Shared intent and compatibility

Ordinary creations save `staticSupport: {version: 1, enabled: false}` by default.
The shared `object.support` fact reports intent, object revision, live readiness
and reason. `object.support.set` uses that revision and normal prepared/persisted
completion. Generated controls, users, agents and programs share the
`staticSupport.v1` feature. Reading never activates geometry or starts physics.
Copies, Undo/Redo, prototypes, blueprints and temporary work retain the setting.

The bundled `platform` and four-tread `stairs` templates explicitly include
compatible collision and enabled support. An agent fulfilling a construction
request can author these together; learners need not configure every block.
Names and appearance never imply support. Terrain and imported rigid mesh
settings remain independent providers behind the shared ground queries.

Room storage writes format 47. Prototype format 11 carries nondefault support;
older minimal formats remain available otherwise. Archive format 34 accepts
bounded formats 32/33 with their original schema, sidecar and v46 room rules.
Original manifest and entry hashes are checked before loading. Older missing
support fields become disabled; malformed fields and unsupported versions refuse.

## Exact sources and static eligibility

The provider owns object identity, stable source/shape identity, accepted
revision/generation and exact primary collider handles. Primary ownership is
captured before independent terrain colliders are appended. Registered solids
validate ownership; they are not a discovery mechanism for primary geometry.
Only the provider's registrations retire when it changes.

Enabled ordinary support requires authored `Fixed` physics, no scanned anchoring,
no saved root-motion recording and matching live policy. The complete selected
primary source must be compatible: explicit Box, a supported default
Block/Drawing/Assembly box, or Automatic box/cylinder/ring recipe. Imported proxies
require Automatic plus explicit compatible collision shapes. Imported rigid mesh
support retains its own walkable setting and restrictions.

Actual colliders serve contacts, ground rays, feet, navigation and physics
columns. No duplicate collider, Rigidbody or invisible floor is added. Unsupported
sphere/capsule or mixed sources refuse as a whole. Pointers, triggers, selection
volumes, barriers and render-only geometry are ineligible. A decorative assembly
normally has one enclosing box; the stairs template instead has explicit tread
boxes matching its editable visual parts. Part animation does not animate those
root-local collision shapes. Generated pieces are private handles, not saved
array indices.

Incompatible physics, collision and root-motion edits refuse while support is
enabled. Disable support through an accepted edit before making that change.
Compatible replacements prepare detached geometry and swap exact handles.
Grip, manipulation, transient animation/carried ownership, restoration and live
policy mismatch suspend availability without deleting intent. Revalidation never
restarts a stopped action. Transient `isKinematic` alone cannot grant support.

## Bounds, readiness and actor policies

Accepted primary collision metadata bounds offset shapes, transformed through
current authored/retained placement and conservatively unioned with independent
terrain/import roles. Unknown bounds remain conservative. Metadata discovers
missing dependencies; actual native geometry grants support. Rays preserve holes
and interiors rather than treating the whole bounding box as a floor.

Ground/movement lookup, dependency acquisition/retention, foot queries, navigation
and physics columns use the same accepted identities. Complete source checks,
exact revisions/generations and publication-membership stamps reject partial or
stale captures. A missing upper support cannot silently borrow a lower floor.
Failed publication preserves accepted snapshots/cursors; stale completion cannot
publish a route.

An explicitly moving actor excludes only its own ordinary-only missing-support
obligation while motion suspends that support. Another accepted floor is still
required. Every other missing source and mixed terrain/import role remains
required, and explicit target residency is checked first. Actorless queries retain
all obligations. Batches check each participant even when their policies match.

Real-room collision participation remains independently selectable per entity.
Opting out of the scan allows valid virtual support below the real floor; it does
not invent terrain or authorize a route through other required virtual obstacles.
Visual opacity, occlusion and audio settings do not silently change collision.

## Work and memory admission

Existing authored object, shape, terrain/import and native limits remain unchanged.
Navigation reserves checked expanded-source, triangle, copied-mesh-byte and
voxel-work charges before native settings, data or copies are created.

| Navigation admission | One build | Process total |
| --- | ---: | ---: |
| Expanded source instances | 768 | 3,072 |
| Expanded mesh triangles | 262,144 | 1,048,576 |
| Owned copied mesh bytes | 16 MiB | 32 MiB |
| Voxel-cell work proxy | 32 Mi cells | 128 Mi cells |
| Map reservations | — | 8 |
| Running native operations | — | 2 |

Unique meshes are copied once per build; each transformed instance contributes
triangle work. Copies remain charged until physical destruction. Installed,
retained and retiring maps retain their charges. Original collision meshes remain
owned by collision resources. Provider admission does not promise that an
aggregate map fits. Operation-slot contention waits visibly; over-budget work or
held map capacity refuses explicitly. Cancellation cannot release native inputs
early or publish a late result.

These ceilings and desktop route/accounting measurements do not measure total
native memory, Recast peak allocation, Quest frame time or eight largest maps.
Source discovery and full-owner checks still incur bounded repeated traversal;
whole-world synchronization is unchanged.

## Acceptance boundaries

Verification covers exact sources and stale handles, cold offset/unknown/missing
support, ownership and writer transitions, overlay compaction, shared edits and
persistence, resource refusal, cancellation and deferred retirement. Native
regressions exercise explicit compound stairs and actor travel with real-room and
virtual-only policies. Integration journeys and current-candidate physical,
long-session, thermal and comfort acceptance remain separately recorded gates.

Existing 10 cm step, 25 degree slope, body/footprint clearance and water policies
remain authoritative. A valid support may still be too narrow, steep, obstructed
or pending to yield a route; scaling a template does not guarantee traversal.
Moving an object suspends support. Passenger-carrying moving platforms, automatic
arbitrary stair conversion, carry velocity, per-face roles, crowd routing and
streaming are not implemented by this increment.
