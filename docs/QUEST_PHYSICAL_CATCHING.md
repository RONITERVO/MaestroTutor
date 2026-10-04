# Native physical catching

`object.physics.catch` is a shared native capability. The book's generated form,
Maestro's delegated task and version 3 programs invoke the same operation. It
builds on ordinary rigid items, exact attachment points, per-channel ownership
and the existing prop carrier. There is no second ball/game simulation.

## Contract

The caller names an incoming created object and an exact object root, recipe
part or current Maestro hand. `holder.revision` or `avatarHash` protects admission;
the active attempt retains the exact loaded holder and socket instances. Moving
an existing holder is allowed; replacing its recipe/rig is not. Both object IDs
remain explicit program resources.

The holder's catch socket is reserved while waiting. Avatar hands also reserve
`upperBody` and use a rotation-only, rate-limited shoulder/elbow solve on the
visible rig. Bone lengths and existing joint limits remain intact. Recipe parts
use their authored motion. This is assisted reaching, without finger gripping,
full-body balance or a guarantee that every trajectory can be intercepted.

The incoming object stays free while waiting. User pickup/release, a separate
throw action and ordinary gravity may move it. A narrow scheduler policy permits
that incoming grip only; edits, explicit Stop, holder grip and catching-side
manual control retain their normal cancellation semantics. Legacy saved version
2 programs reserve entire objects. The catalog declares minimum program version 3;
both validators reject a manually downgraded source, and adding this action through
the book upgrades the draft automatically. One-off invocations use the same declared
minimum and preserve both resource IDs in their terminal receipts.

The object's enclosing collision radius must be at most 0.3 world metres. The
socket offset specifies the intended **collision-volume centre**, in local socket
axes scaled by the holder root, within one holder-scale metre. It is not the
object's mesh pivot. Native shared geometry handles offset pivots and uses an
exact sphere radius or a conservative enclosing sphere for other shapes.

Limits: timeout 0.1–15 seconds, subsequent hold 0.1–10 seconds, grip radius
0.02–0.15 metres, incoming speed 0.1–8 m/s, physics steps 0.005–0.05 seconds.
Relative swept motion catches crossings between fixed samples. New ownership,
teleports, long sample gaps and excessive displacement reset the baseline; they
do not create an invented crossing. Scanned surfaces, other objects, controller
colliders and a head-clearance zone constrain capture and arm reach. Bounded query
buffers fail closed. The short final fit uses the existing checked prop path.

On verified contact, the native reflex role takes the prop only, below manual
control/grip. It carries the object for the requested hold and then drops it into
normal physics. Cancellation retains an already caught object's current placement;
it never returns the ball to its launch point or silently resumes the catch.
Pausing physics, losing the active room or replacing an anchor terminates it.

## Shared observations and composition

- `object.catch` returns live attempts for a prop, including holder, part, phase,
  caught and reason. An empty list means no retained active attempt.
- `object.caught` is emitted once on actual capture. Its source/value is the prop
  ID; fields name holder/part, incoming speed and the captured centre. Ordinary
  bounded event queues, subscriptions and feature checks apply. A nearby ball,
  timeout or replayed receipt does not emit it.
- The terminal action result distinguishes `missed` (`caught=false`) from
  `dropped` (`caught=true`, `dropped=true`). Failures and interruptions do not claim
  successful release. Completed effects are not replayed by duplicate receipts.

A user program can react to contact immediately, wait for the action's drop,
branch on its result and launch toward another anchor. A failed interception is
ordinary program data, not an LLM timing problem. The LLM authors the program;
Unity observes contact and performs the timed work locally. New catch behaviours
therefore compose from existing functions/events rather than a bespoke catch-game
tool. There is no saved autonomous catch component or implicit restart policy.

## Verification boundary

Native tests exercise successful flight interception on the shipped Meshy rig,
rotation-only reaching on fallback and imported bones, root sockets, real
catch/drop physics, human takeover, timeout, overspeed, obstruction queries,
replacement, pause and the typed event scheduler. The full-app shared-client
journey explicitly uses a synthetic floor and the real launch/catch/drop actions.
Its browser fixture replays exact native observations to inspect the generated
form and receipt; it does not implement browser physics. Quest hand/controller
feel, room-scan alignment, frame time and comfort still need device acceptance.

Desktop checkpoint, 2026-10-04: 794 EditMode and 603 PlayMode tests passed (three
optional private-file tests skipped). The full shared-client journey passed 402
observations, including actual launch/catch/drop and duplicate-receipt handling.
The book replay matched the native call and completed result and rejected invalid
time, radius and speed inputs. A full 2,294-test web run and the later native-state
regression passed; TypeScript and lint passed. Android checks passed 76 tests with
two optional private-file skips. The IL2CPP development APK audit matched 1,883
frozen native inputs, the native fixture and catalog, all packaged web files,
default avatar, 178 motions, 24 templates and seven modules. It was not installed.
