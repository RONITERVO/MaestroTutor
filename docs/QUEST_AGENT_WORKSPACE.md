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

## Implemented foundation and first workspace

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
  lifecycle invalidation and actual native receipts. Room observations allow 64 KiB
  for an inspected recipe; requests remain 32 KiB. It does not widen the existing
  4 KiB book snapshot or expose a native object to artifact iframes.
- In native book sessions, the shared text-turn path adds a structured room planner
  before the familiar tutor answer. It uses the existing client resolver and usage
  accounting. Typed requests and recorded speech transcribed into that text-turn
  path share this implementation. Ordinary phone/browser turns are unchanged.

## First shared workspace checkpoint

- A physical Workshop token opens an optional full-page book workspace. The
  normal chat stays mounted and is restored when the workspace closes.
- The left page lists room objects and recipe parts. The right page edits primitive
  size/tint and recipe part shape, local position, dimensions, colour and rest
  rotation. Selecting a recipe part requests a native joint-following outline.
- The animation view edits quaternion keys, adds a rotation track, inserts/removes
  interior keys and changes looping. Play/Stop reports actual native playback and
  supports restarting after focus loss. Duration and key times are not yet freely
  editable; this is the initial key editor, not the complete timeline authoring UI.
- Manual edits and agent edits use the same native executor and room journal.
  Applying a draft creates one Undo entry. Inspection returns the canonical recipe.
  Drafts survive target changes or reconnects and cannot overwrite stale data.
- Version-2 requests check the object revisions actually observed. An unrelated
  moving ball does not invalidate another object's edit. Undo/Redo and legacy
  version-1 requests retain the whole-scene check.
- Cancelling or reconnecting rotates a client nonce and requires a fresh native
  session handshake. Old acknowledgements cannot satisfy a reused sequence.
- Scene-query answers receive current native state; existing recipes can be
  inspected before editing. Full receipts are still repeated in planner context,
  so context size and planning cost need further work.

## PC verification

Checkpoint `71987229` passes 63 Unity EditMode and 67 PlayMode checks; three
optional private-model/motion checks are skipped. Android passes 25 unit checks
and lint (155 required Unity/native checks total). The room/Quest web suite passes
27 checks, the shared prompt suite 65, TypeScript and production web build pass.
APK v2 signature, ARM64 and required manifest entries are verified. SHA-256:
`71987229044DCDEAB6108DDAE42E8315C512859DD2C1A0C354109E07EFB7B83F`.
It is **not installed** and no headset access occurred.

Native robot rest/wave renders and browser workspace captures were inspected.
The browser fixture uses the real book/workspace components and a Unity-exported
recipe, with explicitly simulated native receipts. At 1024 x 768 it checks opening,
part editing/apply, Undo, key editing, Stop/Play and return to the mounted chat,
without page errors. This does not establish Unity-WebView end-to-end operation,
real-provider behaviour or headset usability.

## Still required before claiming the full requested experience

- Authenticated managed and BYOK trials, device installation and Quest acceptance.
  The headset remains on hold until the user returns/reconnects.
- Voice-first start/stop/resume and actions in the separate Live voice path.
- Behaviour blocks backed by existing rule data, stable step/channel identities,
  add/delete/reparent parts, timeline duration/time editing, accessible input and
  explicit edit ownership/locks. Selection highlighting also needs complete cleanup
  when physical selection changes independently of workspace inspection.
- Bounded patch operations, cross-domain transaction/recovery semantics and receipts
  visible in chat even if the final provider reply fails. Future schema versions
  need explicit migrations and preservation instead of silent field loss.
- Agent adapters for import, animation-library, tutor-state/VR rules, gaze/follow,
  controller bindings and scan/physics workflows. These are not yet language actions.
- Planner cost/latency: up to three structured requests precede the tutor reply;
  even a language-only Quest turn currently adds a planning call. Consolidate
  conversational output and planning or add reviewed native function tools.
- Sustained device performance, production auth/attestation, Meta identity/signing
  and existing Store release gates.

See [QUEST_SHARED_ACTIONS.md](QUEST_SHARED_ACTIONS.md) for the comparison with
StateBeats, Scetch-War and StateWork, and the proposed shared action, observation
and development-testing contract. That contract is a staged direction, not a claim
that all current controls already have agent and test parity.
