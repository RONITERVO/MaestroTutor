# Conversation and a shared visual workspace

Accepted direction, 2026-09-26. This is an implementation record and release plan,
not a claim that the complete agent or visual workspace has shipped.

## Product contract

Spoken or typed requests are the default way to use and create in the room.
The agent should complete simple and multi-step actions and report actual results.
Users can optionally open an editable view of the same underlying objects,
parts, animations and behaviours. Manual controls remain available for authors.
The agent uses Maestro's existing managed account/access and BYOK route; BYOK
retains its existing precedence. There is no separate room-agent subscription.

The user's Scratch-like reference is a useful **behaviour view**, not the format
for every kind of data. Provide connected object/part hierarchy, behaviour blocks,
and animation timeline views. Selecting a node highlights its object or joint;
selecting a scene object finds its saved definition. Let users ask to explain,
modify or show a selected block. Show the active event/action and readable failures.
Imported GLB/VRM meshes and animation payloads remain referenced assets; blocks
expose their supported controls, not fabricated editable source geometry.

The workspace is optional book-page content, opened explicitly, with a return to
chat. It must fill those pages and preserve the familiar conversation when closed.
It must not add permanent toolbars over chat or floating flat controls outside the
book. Any additional external interaction stays a physical 3D object.

## Durable representation

Store versioned, typed domain data. Use stable IDs for objects, parts, sequences,
steps, animation channels and assets. Native Unity validates and evaluates the data.
A recipe is editable source; meshes and playback graphs are derived resources.
No generated OpenSCAD, JavaScript, C#, URLs, expression evaluation or dynamic
compilation is required to create a native object. Optional CAD import/export can
be a separate future adapter.

The visual editor and natural-language agent must use the same operations,
validation, revisions and Undo journal. Do not translate back and forth between
independent generated source code and blocks. Layout, zoom, collapsed blocks and
comments are view metadata; they cannot change runtime semantics. Preserve unknown
future fields/nodes read-only until supported, with explicit schema migrations and
recovery copies. Domain-schema versions must not depend on a visual-library version.

Use bounded events/conditions/actions, explicit waits and scheduler cancellation.
A Scratch-like repeat-until must yield, have budgets, and stop on cancellation;
never run an unbounded loop on Unity's frame thread. Add editable parameters,
reusable subactions and imported animation references incrementally.

Agent transactions should show what changed, allow one Undo for a logical edit,
and respect user locks/pinned parts and concurrent editing. Require the revision
actually seen by the planner, not the newer revision at submission. A model answer
is not an execution receipt. Display partial completion honestly when a later
batch, network request or session fails. Do not blindly replay a timed-out action.

Blockly is a candidate for the on-book behaviour UI; it supports custom blocks
and JSON workspace serialization. Its workspace JSON should remain UI metadata
around Maestro's domain data, not become the native engine's canonical program.
No Blockly dependency has been added yet. References:
https://docs.blockly.com/guides/create-custom-blocks/overview/
https://docs.blockly.com/guides/configure/serialization/

## Implemented foundation in this change

- `RoomRecipe` v1 contains at most 32 ordered parts with named parent joints,
  primitive geometry, per-part colours, local transforms and up to 17 rotation
  tracks. Each track has 2–16 keys, within a 0.1–30 second duration. Size, aggregate
  reach, finite values, unique IDs and acyclic ordering are validated. There is a
  256-part scene budget. No source code or arbitrary assets are executable.
- `RecipeObject` builds native grabbable geometry and evaluates quaternion tracks.
  The included `boxRobot` expands into editable data containing all 17 semantic
  humanoid joints plus eyes and a looping wave. It is a simple room character,
  not yet a replacement for the tutor's imported humanoid/pose pipeline.
- Assemblies persist in the existing room journal/storage with paint, size,
  movement, deletion, Undo and Redo. There is one approximate rest-bounds collider
  per assembly; animated limbs do not independently collide. Per-joint posing of
  arbitrary recipes, physics settings via agent and rigid articulated mechanisms
  are still pending. Pausing/focus loss stops recipe playback.
- A bounded native executor accepts at most eight actions as one atomic room edit.
  It validates the whole candidate before editing. Built-ins cannot be deleted;
  held objects and stale plans are rejected. IDs are generated by native code.
- A separate 32 KiB room bridge uses native session IDs, sequence acknowledgements,
  lifecycle invalidation and actual native receipts. It does not widen the existing
  4 KiB book snapshot or expose a native object to artifact iframes.
- In native book sessions, the shared text-turn path adds a structured room planner
  before the familiar tutor answer. It uses the existing client resolver and usage
  accounting. Typed requests and recorded speech transcribed into that text-turn
  path share this implementation. Ordinary phone/browser turns are unchanged.

## PC verification

The current source passes 62 Unity EditMode and 65 PlayMode checks, including
legacy room loading, invalid recipe rejection, all 17 robot joint names, native
wave movement, saved recipe reload, atomic failure and one-step Undo/Redo.
The three optional private-model/motion tests remain skipped. The Android browser
passes 24 unit checks plus lint. The shared prompt suite passes 65 checks and the
focused room/book/chat suite passes 63 checks. The production web build passes.
Development APK `0FE4C286` was built and its v2 signature, ARM64 architecture and
required manifest entries verified. It is **not installed**. SHA-256:
`0FE4C28658C60BCD1186E88581FE746CFDF2043D2442038E6F7C55B15555D517`.
There are 151 passing required Unity/native checks (62 + 65 + 24).
Native renders `recipe-robot-rest.png` and `recipe-robot-wave.png` were inspected;
the simple geometry and changed arm pose are visible. These are desktop Unity
renders, not headset or real provider acceptance.

## Still required before claiming the full requested experience

- Actual authenticated managed and BYOK provider trials, device installation and
  Quest acceptance; the headset remains on hold while charging.
- A voice-first start/stop/resume flow and room actions in the separate Live voice
  path. The current change alone does not establish entirely hands-free operation.
- Full-page optional visual workspace, shared graph/step identities, manual part
  editing, timeline editing, selection highlighting, accessible input and locks.
- Scene-query answers and read-back of full recipes on demand, bounded patch operations, cross-domain
  transactional edits and inspectable receipts that survive a failed final reply.
- Agent adapters for existing import, animation-library, tutor-state/VR rules,
  gaze/follow, controller bindings and room-scan/physics workflows. Do not claim
  that those already work from language requests.
- Current planner uses up to three structured requests followed by one ordinary
  tutor reply. Even a language-only Quest text turn adds a planning request. All
  requests use the same managed/BYOK accounting, but this adds latency and cost.
  Consolidate planning and conversational output or add reviewed native function
  tools before release. Managed policy currently permits Google Search only;
  this foundation uses the already-supported JSON response schema route.
- Performance and
  sustained memory/thermal measurements on device, production auth/attestation,
  Meta identity/signing and the existing Store release gates.

The initial whole-scene revision guard is deliberately conservative: moving physics
items can invalidate a pending plan. Refine this to per-object preconditions before
release so unrelated simulation does not prevent creation. Expose actual playback
status and an explicit restart operation after pause, rather than treating the
saved playing flag as proof that animation is currently running.
