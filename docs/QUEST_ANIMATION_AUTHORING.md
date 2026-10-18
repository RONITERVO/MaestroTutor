# Shared pose and motion authoring

The agent, programs and optional book action catalog can edit the same saved
motion used by the physical animation tray. `animation.author` is a completion,
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
RoomMotionEdits helpers. Physical keyframe, recording and pose-session saves use
the ordered persisted-edit queue; physical and shared calls validate canonical
animation through RoomEditor.PrepareAnimation. Their captured work remains
available after pre-acceptance failure. Applied/unconfirmed publication requires
room recovery instead of replay. Lifecycle checkpoints now queue detached
snapshots without waiting for disk;
explicit TryFlush and direct compatibility APIs retain synchronous completion
contracts. See [accepted-save ownership](QUEST_ACCEPTED_SAVE.md).

Saved animation actions use the common ordered edit queue. A waiter keeps copied
intent, not a candidate room or held body. When admitted, it checks the original
object revision, availability, current movement/gesture and authoring ownership
again. Active previews, recordings and retained unsaved takes/poses are protected,
including after their controls have released the target. Preparation rechecks
these conditions before native acceptance.

Outside temporary play, completion confirms both native reconciliation and disk
publication. Cancellation before acceptance leaves the edit unapplied. Stop
after acceptance cannot retract the edit or its save. An accepted publication
failure retains the unconfirmed live edit, preserves saved files and blocks
further saves until recovery. Inspect the outcome instead of replaying blindly.
The result comes from the exact accepted candidate and revision, even if a newer
edit succeeds before the observer collects the result.

Content edits and Undo preserve current root placement and momentum. Saving a
pose applies those canonical joints; saving keyframes does not start playback.
Temporary-room edits affect only the fork until Keep. Discard restores the
baseline; Keep persists a detached snapshot. Receipts prevent duplicate dispatch.
The same catalog also exposes live recording sessions below.

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
- `finish {sessionId, target}` freezes samples, releases capture controls and
  joins the room's ordered background edit queue. Its completion confirms native
  application and durable publication of one Undo edit, without playback.
  Physical Record/Stop, lifecycle Stop and repeated Finish calls share the same
  save attempt. Cancelling a caller stops observation; the recording session
  continues saving. Temporary-room completion is live-only until Keep.
  Selection/avatar changes, pause/focus loss and the 30-second/301-frame limit
  freeze the same take. Suspended room interaction defers native acceptance
  until resume. A physical avatar switch must wait for the take to finish
  saving before a subsequent selection can replace the avatar.
- `discard {sessionId}` removes only that in-memory take, preserving the
  previous saved animation when native acceptance has not happened. It cancels
  an unaccepted queued save. After acceptance, wait for publication to settle;
  discard cannot retract an applied edit. Discarding an applied-but-unconfirmed
  failed take only releases its in-memory buffer; recover room saving separately.
  No target resource is required, so an unaccepted retained take
  remains discardable after its object is deleted. Successful save/discard
  issues a new session ID. Old action receipts do not replay recording.

The read-only fact exposes phase (`idle`, `recording`, `saving`, `unsaved`), target, frame
count, sampled duration, object revision, temporary-room state and bounded error
text. Recording samples on a 0.1-second interval with a final endpoint. Failed
preparation stops sampling and retains frozen frames for an exact retry or
discard. Failed publication may have already applied the animation: its live edit
remains unconfirmed, old saved files are preserved and further saving is blocked.
Retry refuses that already-applied take, changed object revisions and different
room sessions. Record is amber while a frozen take is saving or awaiting recovery.
Disabling the workshop cancels unaccepted preparation and retains the take;
accepted publication drains under the editor's existing storage ownership. A retained take blocks workspace and temporary
room boundaries until saved or discarded. App/process termination loses any
unsaved in-memory take; recording is never automatically restored on launch.

## Pose save completion and recovery

Releasing a joint or leaving Pose Maestro freezes the complete canonical pose
and queues a save. Physical controls, lifecycle Stop and shared capability calls
use the same ordered native edit queue. Preparation and publication run off the
owner thread; completion confirms both native application and saving.

Each saved version has one immutable receipt and Undo entry. Live posing can
continue while older versions are being saved: the saved pose and the displayed
preview are separate. Saving an older version never replaces newer live joint
edits. Up to 32 pending pose versions are allowed; reaching the bound stops
authoring and retains the newest preview instead of dropping it.

A preparation failure stops authoring and retains the newest live/requested pose
in memory. The avatar returns to its saved state; the amber Pose Maestro control
and status identify the retained buffer. Save pose retries it even after
selecting another object. Retry requires the original room and exact authored
revision, checks current actors/holds, and cannot silently rebase onto another
edit. Own earlier accepted pose versions advance this expected revision; unrelated
edits do not.

A publication failure can leave an applied but unconfirmed pose. Old saved files
remain intact and room saving is blocked. The retained buffer remains inspectable,
but cannot be blindly replayed. Discard only releases that buffer; it does not
undo an applied edit or repair storage. Recover room saving separately.

Pause retires unaccepted preparation and queued admissions; the same frozen
versions rejoin in order after native interaction resumes. Accepted publication
remains observable while suspended. Disabling the workshop retains outstanding
work as a failure; accepted storage publication drains under the editor's
ownership. Repeated lifecycle notifications cannot erase or silently retry a
failed buffer.

Pending poses block other authoring modes, avatar replacement, workspace changes
and temporary-room Begin/Keep/Discard. Joint editing in the existing live pose
may continue while saving. Temporary completion stays in the fork until Keep.
App/process termination loses unsaved in-memory poses.

## Shared live posing

`animation.pose` (feature `animationPosing.v1`) shares the physical joint-handle
session. Inspect `animation.posing`: it reports the native session ID, opaque
pose version, phase (`idle`, `posing`, `saving`, `unsaved`), object revision, held-joint
state, `editing` state, temporary-room state, supported joint names and bounded error
text. `editing` indicates whether joint editing continues during saving. Idle
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
- `save {target, sessionId, version}` freezes that version and queues one Undo,
  keeping the live pose open. Physical Save pose and handle releases use the same
  path, including current agent edits. Completion confirms the captured version
  and its accepted object revision; it never claims later preview edits are saved.
- `finish {target, sessionId, version}` freezes the latest pose and closes the
  physical controls. Earlier queued versions finish first. Physical Stop, focus
  loss, pause and selection changes use the same path. Repeated save/finish of an
  already queued version observes that attempt. Cancelling a receipt stops
  observation only; the pose session continues saving.
- `discard {target, sessionId, version}` cancels unaccepted queued saves and drops
  the live/retained preview. While an accepted save publishes, wait for its
  outcome first. Previously accepted edits remain; Undo is separate. Physical
  Discard pose uses the same path.

Inspect the session again before each mutation. Handle movement, applied frames
and saved edits advance the pose version; do not assume the next value is +1.
Finished sessions issue a fresh identity. Duplicate execution receipts do not
repeat edits, and stale identities/versions cannot affect a newer session.
Preparation failures release authoring while preserving the latest unsaved pose
and identity; `save` or `finish` retries its exact room/object revision.
An applied/unconfirmed publication failure requires recovery instead of replay. Shared recovery cannot
interrupt another live actor. `discard` explicitly removes only that retained
pose. Both facts can inspect retained poses even after selecting another object.

During recording, physical and shared rotations feed the same sampled rig.
Save/finish/discard of the pose must wait for recording finish/discard, which
ends both sessions; a failed recording retains the combined take through
`animation.record`. Temporary poses stay in the fork until Keep. No authoring
session auto-resumes on app launch, and unsaved memory is lost on termination.

## Pending physical keyframe edits

Save frame, Replace, Remove, Loop, speed and Auto gestures freeze one immutable
edit, its target, original object revision and room/fork identity. The workshop
retains that edit until confirmed or explicitly discarded. Other keyframe edits
wait. Save frame retries the same captured edit while one is pending; it cannot
accidentally record another frame. Discard edit is a solid button on the existing
animation tray. Selection changes cannot redirect either operation.

The original authoring lease can cooperate with a physical grip or live pose
while a captured frame saves. Later movement and joint edits remain visible.
Only acceptance of this workshop's exact edit may advance already queued pose
saves; unrelated changes do not silently rebase them. A retry after authoring
released control must not interrupt a newer actor.

The user and agent inspect `animation.edit.pending`: sessionId, target, phase
(idle/saving/unsaved), original revision, frame count, duration, loop, writesPose,
poseSaved, temporary, applied and bounded error. Applied is not a durability
claim. Idle has a fresh session identity and no target.

`animation.edit.resolve {operation, sessionId}` uses that exact identity.
`retry` observes a running attempt or retries its frozen values at the original
room/object revision. It completes only after native application and successful
publication, returning sessionId, target, phase, accepted revision and temporary.
A discard receipt identifies the original captured revision.
`discard` cancels unaccepted work and clears its buffer; accepted publication
must settle first. Discard does not undo accepted edits or repair failed saving.
Cancelling an execution receipt stops observation only. Both operations use the
same workshop as the physical controls and require `animationEditResolution.v1`;
no separate LLM tool or mutable target parameter is introduced.

Pause retires unaccepted preparation and rejoins the ordered queue on resume.
Accepted publication stays observable, including while the workshop component
is disabled. Disable retains unaccepted work for retry; destruction retires
observation while accepted storage drains. Temporary edits remain live-only
until Keep. Unsaved buffers are memory-only and do not survive termination.

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
request and completion match the captured native call, with a freshly loaded
revision and object-definition guard. The Chrome acknowledgement is a fixture;
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


Keyframe verification includes actual XRI grip movement and solid Save frame/
Discard edit pointer taps, immutable shared receipts, selection changes, exact
retry, stale revisions and observers, competing actors, queued later poses,
preparation/publication failure, suspension, disable and temporary forks.
`test-fixtures/browser/keyframeSessions.json` is captured from real native
pending/completed execution. `scripts/probe-keyframe-sessions.mjs` reads that
session through the book and sends the exact generated retry request. Chrome
replays the captured acknowledgement and never shows saved output during
preparation. The complete offline Headless journey additionally discovers the
native pending fact and resolution contract. Physical-save mutation is covered
by native PlayMode integration; the browser replay is not headset evidence.
