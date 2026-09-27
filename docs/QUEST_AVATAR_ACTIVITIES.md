# Per-avatar tutor-state motions

Development implementation, updated 2026-09-27. This is presentation driven by the
existing web tutor state. It never starts another conversation, microphone,
voice or paid session. Quest acceptance remains required; the headset is on
charging hold and no installation is implied by desktop verification.

## Use from the book

1. Load a compatible custom Maestro and save motions from a supported animated
   export. Open **Library** on the physical import tray and select a saved motion.
2. On the right page expand **Tutor-state motions**. Choose Idle, Listening,
   Thinking or Speaking. Set speed, selection weight, reuse gap and whether to
   loop, then **Assign to tutor state**. Up to four choices fit each state.
3. Select an existing assigned motion to adjust it, or Remove it. **Use included
   animation** clears that state. Undo/Redo affects only assignments for the
   currently loaded avatar, separately from room placement and rule history.
4. Return to chat. The actual shared tutor activity chooses the assigned motion.
   Browsing and assignment do not start an automatic preview. The existing
   explicit Preview/Stop controls still work while the library is open.

A nonlooping motion finishes once, then starts its reuse gap. Ready choices are
weighted; the previous choice is excluded when another is ready. A looping
choice stays until the state changes or a higher-priority action interrupts it.
If none is ready, the included animation fills the gap. The gap is measured
from completion/state exit, not from the start of the motion. Cooldowns and
selection history are runtime state, not persisted scheduling. An interrupted
manual takeover resets selection but does not replay a pending load.

The editor supports 0.25–2x speed, weights 1–10 and 0–60-second reuse gaps. No
selection is preassigned to private imported models. The included development
character continues using its built-in animations. Walking has its separate
saved gait assignment; visual-rule sequences retain their existing triggers.
Rules can also use recorded poses/movement through the authoring system. This
editor currently assigns reusable imported library motions only.

## Use through Maestro chat

An explicit request such as “use Friendly wave while you are speaking” can now
be delegated through the original app-owned room agent. It searches the shared
motion library and submits `avatarActivities.v1`; Unity does not own a second
Gemini client. The agent can assign/update one choice, remove it, restore a role's
included animation, or undo/redo assignment changes. It cannot import new files
through this operation or assign motions from an incompatible avatar rig.

The standalone command is `{action:"avatarActivities",activities:{modelHash,
revision,operation:"edit",edits:[{operation:"assign",role:3,choice:{motionId,
weight:1,speed:1,cooldown:0,loop:false}}]}}`. All identifiers and `revision` come
from the native observation/library. Roles are 0 Idle, 1 Listening, 2 Thinking,
3 Speaking. `remove` uses role and motionId; `clear` uses role only. `undo`/`redo`
use modelHash and revision without edits. Up to 16 edits form one atomic profile
save and one assignment Undo. Unmentioned choices/roles remain unchanged.

`scene.activityProfile` and the book share one native projection. Each reports
the current model, revision, four role lists, availability, status and history.
The book additionally gates assignment on its selected motion. A missing payload
cannot be newly assigned; an existing unavailable choice retains its identity.
The shared validator enforces bounds and rejects unknown/mistyped wire fields.

The profile document has an in-memory revision separate from room/rule revisions.
Successful changes and history moves advance it; no-ops and rejected edits do not.
It is fenced by the current native session and model identity. Both book and agent
must submit the revision they observed. A conflicting edit is rejected, never
silently retried. The book preserves a dirty settings draft and offers an explicit
reload when its profile changes. Old book states can still be displayed, but
assignment controls need the new revision field. Old requests without a revision
fail safely on the new runtime. Embedded web and native code ship together.

Saving an assignment does not start a preview or prove immediate playback. The
existing activity runtime retains its priorities and runs choices when appropriate.

## Runtime ownership and transitions

The native avatar observes version-1 book snapshots with one of the four known
activity values. Paused audio, app pause/focus loss, reduced motion, editing,
a saved static pose, library browsing, walking and explicit clip playback
suppress these automatic motions. A loading result must still match its
request generation, avatar identity and active state before it may start.
Missing, incompatible or failed motions fall back to the included animation.
A failed choice is skipped for that state until it changes or the profile is
edited. Empty/cooling selections are polled at most four times per second.

State transitions blend canonical body rotations and hips position over 0.25
seconds. The existing retargeter presents that pose on the custom avatar.
Other imported channels still use the validated same-rig clip; arbitrary
fingers, facial morphs and noncanonical bones do not receive this body blend.
This is not a layered full-body blend tree, foot-plant solver or cross-rig motion
converter. Idle waits for the initial built-in greeting to finish.

Room position and heading remain owned by placement/navigation. Imported root
travel is anchored by the existing motion sampler. A motion can extend hands or
feet outside the navigation capsule; no per-limb wall/prop collision or authored
throw/hand attachment is added here. Hair and cloth physics remain excluded.

## Persistence and transport

`room/avatar-activities.v2.json` stores up to 64 profiles, keyed by exact model
content hash with its compatible rig fingerprint. Switching away and back
restores that model's assignments. Another model with the same rig does not
silently inherit them. Revisions require deliberate reassignment; names and
list positions are not identifiers. References use stable library GUIDs.

The separate 256 KiB file uses the existing atomic/backup persistence path and
strict validation. Unknown versions or invalid data are preserved read-only.
Undo/Redo retains up to 32 in-memory changes per history list, with operations
selected for the current avatar. It does not survive app restart. Current and
history motion references protect downloads in the existing library maintenance
workflow; see QUEST_MOTION_MAINTENANCE.md.

The bounded library bridge adds assignment/remove/clear/Undo/Redo commands and
an optional profile view. Native validates bounds, role, rig and current avatar
hash and shared profile revision. Stale avatar/session/profile requests cannot
redirect or overwrite an assignment. Book pages
remain the only flat editing surface; no persistent toolbar is overlaid on the
conversation.

Android snapshot storage now invalidates delayed WebView callbacks on pause,
resume, navigation and browser replacement. Unity also clears its snapshot on
suspension transitions and empty/invalid native updates, including navigation. A resumed activity must come from a fresh snapshot,
not the object held before interruption. Existing audio-resume rules still
apply. Actual Android/WebView lifecycle acceptance remains a device check.

## Verification and remaining acceptance

Automated coverage includes persisted per-model assignments, validation and
future-version preservation, per-avatar Undo isolation, real-rig movement,
state changes, transition continuity, weighted choices/cooldowns, manual
ownership, loading interruption, model switching, stale book requests and
native snapshot invalidation. Web tests check profile parsing and bounded
requests. The desktop browser fixture consumes synthetic Unity-emitted native
state; its acknowledgement simulation is presentation evidence only.

After reconnecting Quest, check readable/reachable controls, assignment
persistence, changes during a live tutor session, walking/posing/rule priority,
paused audio and headset interruption. Profile memory, imports and sustained
playback with the growing Meshy collection on Quest. Desktop test success does
not establish headset performance, complete provider parity or Store readiness.

Development checkpoint **906ED6F1** passed the complete build and 131 automated
checks and remains uninstalled. Exact artifact hashes and verification scope are
in QUEST_DEVICE_QA.md.

Shared agent checkpoint adds native/web wire conformance fixtures, an actual
Unity-emitted `avatarActivityState.json`, atomic two-role edits, stale agent/book
conflicts, manual-to-agent read-back, independent Undo/Redo, real head rotation,
state transitions, authoring priority and missing-download rejection. App-owned
provider journeys use a mock provider; they establish orchestration and rejection
reporting, not real-provider acceptance. Latest artifact details are in
QUEST_DEVICE_QA.md.
