# Room action ownership

PC development increment, 2026-09-30. This is not headset or Store acceptance.

## One native policy

`RoomEditor.Ownership` is shared by `RuleScheduler`, the native capability host,
direct avatar movement, animation authoring, registered object grips, and Maestro's
automatic tutor-state animation. Acquisition reserves every requested channel
before invoking displaced actors' cleanup. A blocked channel prevents the whole
request; it never partially cancels other actors.

| Native role | Priority | Takeover policy |
| --- | ---: | --- |
| Grip | 40 | Stops conflicting movement/animation; preserves the grasped placement |
| Direct control / authoring | 30 | Replaces conflicting lower roles and other direct controls after readiness checks |
| Reflex | 20 | Reserved native role; no hit/catch reaction is implemented by this increment |
| Program / one-off action | 10 | Replaces ambient work; equal program claims remain refused |
| Ambient tutor activity | 0 | Runs only when eligible and no stronger owner conflicts |

Roles are assigned by native entry points, never by program JSON or an agent
priority field. Agent and book one-off catalog calls remain Program role; the
shared direct avatar control remains Control role. This does not grant an agent
permission to choose a stronger route merely to bypass a conflict.

Claims name a target and channel. `wholeTarget` conflicts with every channel on
that target. Walking plus gaze can still run alongside the existing upper-body
layer; taking over movement does not stop the arms. Version-2 programs retain
whole-target reservations, while version-3 programs release claims between active
invocations and own nothing while waiting for an event.

A recorder or manual keyframe session explicitly allows cooperative grips.
Recording continues while either hand moves the object; other programs remain
excluded. Playback cannot begin while a hand is driving it. The grip lease lasts
until XRI releases the last hand. Joint handles remain part of their authoring
session; they do not create independent competing body owners.

## Interruption and lifecycle

Interruption cancels the displaced operation, releases its resources and reports
the interrupting actor in the native outcome. It does not suspend a program stack
for later automatic resume. Repeated direct movement commands obtain a fresh
lease; disposing an older lease cannot remove its replacement. Successful direct
takeover also cancels older queued version-2 work for those targets; refused
inputs leave it intact. Invalid tracking
or navigation prerequisites do not stop the currently valid action/authoring.

Pause/focus loss revokes ownership and retains the existing operation Stop policy,
including returning an unreleased animation prop. Grips/direct takeovers preserve
placement where required. Focus return alone never replays cancelled programs or
previews. Ambient selection remains subject to avatar eligibility and fresh tutor
state after the avatar lifecycle callback. Recall, edits and model changes retain
their existing broader cancellation hooks.

All cleanup callbacks are attempted even if one fails. A cleanup failure prevents
further acquisitions for that room instance and is visible as an error; reopening
the room creates a clean instance. Reentrant acquisition during takeover/pause is
refused, and ownership is bounded to 64 actors and 128 claims per actor.

## Shared evidence

`roomOwnership.v1` advertises `scene.ownership`: suspended/error plus owners with
identity, label, role, cooperative-grip flag and claims. The web bridge validates
this bounded native observation. The agent receives it in its ordinary room
context; no extra tool or provider is introduced. Optional object/behaviour book
workspaces expose the same evidence under **In control now**. This does not add
controls around the familiar chat pages. Existing Run/Stop controls remain the
ways to act on behaviours. An empty ownership list does not mean queued or waiting
programs have completed; their existing execution observations remain authoritative.

## Verification and remaining release work

EditMode checks atomic multi-channel refusal, priority and same-role behavior,
compatible channels, cooperative grips, stale leases, pause and callback failures.
PlayMode checks actual XRI movement while recording, two-hand release, recorded
animation cancellation without snapping, direct movement/arm composition,
readiness before authoring takeover, and no restart after focus recovery. Existing
imported motion, prop and avatar lifecycle tests run alongside these regressions.
Chrome replays native held/released observations in both book workspaces; it does
not pretend to execute Unity. Build evidence records exact test counts and APK.

This is not a complete world actor model. Recipe track autoplay, physics bodies,
editor transactions and user locomotion still have their dedicated lifecycle
controls; recipe programs and physics capabilities enter through the scheduler.
Subsequent increments added typed contact, proximity and physical-motion
subscriptions ([event programs](QUEST_EVENT_PROGRAMS.md)), structured collections
([program data](QUEST_PROGRAM_DATA.md)) and pinned local module libraries
([program modules](QUEST_PROGRAM_MODULES.md)). Named robot-part channels,
declarative resume policies, reflex behaviors, broader world/action coverage and
Quest timing remain release work. No automatic replay was added to approximate
resume.
