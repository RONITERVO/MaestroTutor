# Shared pose and motion authoring

The agent, programs and optional book action catalog can edit the same saved
motion used by the physical animation tray. `animation.author` is an instant,
whole-target capability; discovery, schema validation, readiness, execution
receipts and Undo use the existing shared paths. It adds no model-provider
connection. Saving an edit never starts playback.

## Read, edit, inspect

Read `animation.authored {target}` first. Its result includes the current object
revision, frame count, duration, looping and whether a saved pose overrides
automatic tutor activity. Every edit must supply that exact revision. A manual
edit, Undo, or discarded temporary room invalidates older revisions.

- `animation.frame {target, revision, index}` reads one saved keyframe. Index `-1`
  reads saved object placement at time zero, not its current animated or physical
  position. `joints` names the ordered channels; `jointChannels` distinguishes a
  null channel list from a present empty one.
- `animation.joint {target, revision, index, joint}` reads one canonical local
  quaternion: `-2` is canonical rest, `-1` is saved pose, and nonnegative indices
  select keyframes. These are not imported-model raw bone rotations. Supported
  imported Maestro models use the same retargeting as physical posing.
- Missing data and stale reads are unavailable. No identity rotations, frames or
  channels are invented. Facts do not acquire ownership or enter posing mode.

`animation.author` selects one operation:

| Operation | Effect |
| --- | --- |
| `frames` | Replace a motion or patch up to eight frames by exact time. Existing loop setting is preserved. |
| `removeFrames` | Remove up to eight exact existing times; rebase the first survivor to zero. Removing all frames clears motion. |
| `settings` | Set looping and/or total duration; retiming needs at least two frames. |
| `pose` | Replace Maestro's complete saved canonical pose. `null` returns control to automatic activity; motion stays saved. |
| `clear` | Remove recorded motion while preserving saved pose. |

The native runtime validates the complete resulting room before committing. Each
motion starts at zero, has distinct increasing times, and uses identical ordered
joint channels on every frame. Existing limits remain: 301 frames / 30 seconds,
17 canonical joints, 1,200 frames / 6,000 joint poses across the room. Root
placement is relative to the object's room parent; scale is uniform and the
object kind's limits apply. Joint arrays and frames are literal inputs; scalar
arguments such as revision can be bound to typed program expressions.

Each call returns `{target, revision, frames, duration, loop, poseSaved}`. Inspect
the returned revision before the next edit. Large clips can be built in batches;
every intermediate result must be valid, so supply the complete joint-channel set
for every authored frame. Animation-library imports and immutable motion IDs are
unchanged. Recorded keyframes are bounded room content, not a replacement for the
large imported animation library.

## Saving and ownership

Physical Add/Replace/Remove frame, timing and loop controls share the detached
`RoomMotionEdits` helpers. Physical and agent saves share `RoomEditor.WriteAnimation`:
one validated durable edit and one Undo entry. Storage failure leaves the saved
motion, journal and revision unchanged. Physical recording can still sample an
object while the user grips it. A remote edit requires the target released,
physical authoring stopped and compatible ownership available; it cannot overwrite
a live take or pose preview.

Temporary-room edits affect only the fork until Keep. Discard restores the
baseline; Keep persists a detached snapshot. Stopping an already completed edit
does not undo it. Action receipts prevent duplicate dispatch; a separate Undo
reverts an edit. The same catalog also exposes live recording sessions below.

## Shared recording sessions

`animation.record` (feature `animationRecording.v1`) controls the same recorder
as the solid Record and Discard take buttons. Inspect `animation.recording`
first. Its native-issued session ID identifies the next start, or the exact
live/retained take; stale commands cannot finish or discard a later take.

- `start {sessionId, target, revision}` requires the current authored object
  revision. Its receipt completes immediately while the recorder continues to
  own the object, permitting grip movement. Cancelling the completed receipt
  does not end the recording. A program can start, wait and finish through the
  same capability. Agent/program starts refuse another live actor; only the
  trusted physical Record entry point has manual interruption priority.
- `finish {sessionId, target}` freezes samples and saves one motion with one
  Undo entry, without playback. Temporary-room recording stays in the fork until
  Keep. Selection/avatar changes, pause/focus loss, physical Stop and the
  30-second/301-frame limit attempt the same finish operation.
- `discard {sessionId}` removes only that in-memory take, preserving the
  previous saved animation. No target resource is required, so a retained take
  remains discardable after its object is deleted. Successful save/discard
  issues a new session ID. Old action receipts do not replay recording.

The read-only fact exposes phase (`idle`, `recording`, `unsaved`), target, frame
count, sampled duration, object revision, temporary-room state and bounded error
text. Recording samples on a 0.1-second interval with a final endpoint. Failed
saves stop sampling and release controls while retaining the frozen frames in
memory. Record retries that exact take; Discard take abandons it. The Record
button is amber while a failed take awaits resolution. Retry refuses a changed
object revision or room session. A retained take blocks workspace and temporary
room boundaries until saved or discarded. App/process termination loses any
unsaved in-memory take; recording is never automatically restored on launch.

## Failed manual pose saves

Releasing a joint or leaving Pose Maestro attempts to save the complete canonical
pose. If that write fails, authoring stops and the displayed avatar returns to its
saved state. The failed pose remains frozen in memory; the amber Pose Maestro
control and tray status identify it. Save pose retries the retained pose even if
another object is selected. Discard pose explicitly abandons only that failed
pose, leaving the saved pose and animation unchanged.

Retry requires the original room session and exact Maestro revision, checks
runtime/write holds and held objects, and cannot replace another manual owner.
It rechecks the revision after interrupting a lower-priority actor. A successful
retry saves one Undo entry and restores the pose through the normal imported or
included rig. Repeated Stop, pause and focus notifications cannot silently retry
or erase the retained pose. A stale retry stays retained until explicitly
discarded; it never merges into newer edits.

Pending poses block new animation authoring, avatar replacement, workspace
switches and temporary-room Begin/Keep/Discard. A pose retained in a temporary
room saves into that fork; Keep is still required for persistence. App/process
termination loses unsaved in-memory poses. The shared live-pose capability below uses these same recovery controls and
retained data; the physical tray can also save or discard a live pose.

## Shared live posing

`animation.pose` (feature `animationPosing.v1`) shares the physical joint-handle
session. Inspect `animation.posing`: it reports the native session ID, opaque
pose version, phase (`idle`, `posing`, `unsaved`), object revision, held-joint
state, temporary-room state, supported joint names and bounded error text. Idle
has version zero and no live joint values. A completed action receipt does not
mean the pose session has ended; stopping that receipt does not stop posing.

- `start {target: "maestro", sessionId, revision}` enters posing using the current
  idle identity and Maestro revision. The session owns Maestro until it ends.
  Physical Pose Maestro uses the same entry point with manual interruption
  priority. Agent/program starts cannot steal another live actor. An active
  pencil stroke blocks posing until released. An idle pencil is put away without
  a global stop signal, preserving the initiating program and unrelated actors.
- `rotate {target, sessionId, version, joints}` applies 1–8 distinct supported
  joints atomically to the preview. Rotations are normalized canonical local
  quaternions. The included and imported display rigs use the physical limits
  relative to rest: head 75 degrees, spine/chest 45, other joints 150. Read the
  actual clamped rotation with `animation.pose.joint {sessionId, version, joint}`.
  Joint reads/edits refuse a held physical handle. No save or playback occurs.
- `save {target, sessionId, version}` saves one Undo and keeps the session open.
  Physical Save pose does the same. Physical handle releases save automatically,
  including any agent edits currently in the shared preview.
- `finish {target, sessionId, version}` saves and closes posing. Physical Stop,
  focus loss, pause and selection changes attempt the same save on exit.
- `discard {target, sessionId, version}` drops the unsaved preview and closes the
  session. It keeps previous explicit saves and physical handle-release saves;
  Undo is separate. Physical Discard pose uses the same path.

Inspect the session again before each mutation. Handle movement, applied frames
and saved edits advance the pose version; do not assume the next value is +1.
Finished sessions issue a fresh identity. Duplicate execution receipts do not
repeat edits, and stale identities/versions cannot affect a newer session.
Failed saves release authoring while preserving the frozen pose and identity;
`save` or `finish` retries its exact room/object revision. Shared recovery cannot
interrupt another live actor. `discard` explicitly removes only that retained
pose. Both facts can inspect retained poses even after selecting another object.

During recording, physical and shared rotations feed the same sampled rig.
Save/finish/discard of the pose must wait for recording finish/discard, which
ends both sessions; a failed recording retains the combined take through
`animation.record`. Temporary poses stay in the fork until Keep. No authoring
session auto-resumes on app launch, and unsaved memory is lost on termination.

## Book and verification

The expert action catalog derives typed fields, nested frame/joint lists and
operation choices from the native schema, while retaining the source editor.
Maestro-only target fields exclude other objects. The agent uses the same contract
through the existing delegated-room task; no separate animation-specific LLM tool
is required. Native feature `animationAuthoring.v1` is required for direct calls
and saved program invocations.

Native tests cover actual included/imported-rig playback, physical/agent edits,
Undo/Redo, stale and invalid input, active authoring refusal, failed writes and
temporary Keep/Discard. `test-fixtures/browser/animationAuthoring.json` is captured
from native execution, including a three-frame, 17-joint nod and exact readback.
The web verifies its receipt and fact types. `scripts/probe-animation-authoring.mjs`
fills the nested book controls without entering JSON and checks that the generated
request matches the captured native call. The Chrome acknowledgement is a fixture;
it is not a real-provider or headset test. Device comfort, authoring performance
and real-provider journeys remain release gates.

Recording verification additionally covers physical ray taps, program start/wait/
finish, gripped object movement, manual interruption priority, stale session IDs,
duplicate receipts, write-failure retry, deletion/discard, lifecycle auto-finish
and temporary Keep. `test-fixtures/browser/recordingSessions.json` captures native
start/finish receipts and facts. `scripts/probe-recording-sessions.mjs` reads the
session identity in the book, fills typed controls, saves and observes the next
idle identity; its acknowledgement uses the native capture as a local fixture.

Pose recovery tests reproduce the previous failed-write loss and cover exact
retry after selection changes, repeated lifecycle stops, stale revision refusal,
Undo/Redo, temporary Keep, imported-rig readback, competing owners and real ray
taps on the solid Save pose/Discard pose controls. The desktop-rendered tray
capture checks layout only; headset readability remains a device acceptance gate.

Shared posing verification covers actual rig rotations and clamps, physical XRI
holds, stale versions and session turnover, duplicate receipts, direct program
start/finish, physical/agent save and discard, failed-save recovery and refusal
to interrupt another actor, recording interoperation, imported retargeting and
temporary Keep. `test-fixtures/browser/posingSessions.json` contains native
start/rotate/finish receipts and session/joint facts. The Chrome probe reads each
version and sends matching requests through generated fields; acknowledgements
replay the native capture and are not provider/headset acceptance.
