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
termination loses unsaved in-memory poses. These recovery controls are currently
physical tray controls; shared live posing and its exact session identities are
still a separate unfinished increment.

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
