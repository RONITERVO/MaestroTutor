# Shared movement and room view

`controller.mode.set` lets the generated book controls, delegated agent and saved
programs request the same live movement/view transitions as the solid movement
tray. Original Maestro still owns the conversation and provider connection.
Use these actions only for an explicit user request to change movement or view.
Entering Virtual view hides the real room; it is not an implicit prerequisite an
agent should add to an unrelated task.

## State and operations

Read the `controller.mode` fact immediately before a change and pass its exact
`stateId`. It includes the accepted configuration ID, both movement opt-ins,
Virtual/MR view, head tracking and focus. Read `controller.settings` for the
independent stick bindings. Changes to preferences, modes, focus/tracking and
recovery invalidate stale identities, including a manual change and reversal.
Execution receipts separately deduplicate requests. Replaying a successful enable
after an explicit disable returns its old receipt without re-enabling anything.

| Operation | Result |
|---|---|
| `maestro.enable` / `maestro.disable` | Opt Maestro's bound stick in or out; enabling requires its loaded rig, accepted ground and running physics under its collision policy |
| `user.enable` / `user.disable` | Opt the user's separate stick in or out; enabling requires the tracked world and a ready room scan; available at any backdrop opacity |
| `view.virtual` | Set an opaque virtual backdrop and disable real-depth occlusion; creates no floor and enables neither stick |
| `view.mixedReality` | Restore ordinary passthrough presentation while retaining movement opt-ins, world pose, physics and other activity |

An enable resets input gates. A held stick must return to neutral and a held
button must be released before input can act. The action itself never supplies
movement input. Existing collision and navigation checks still govern physical
steps. An already satisfied mode request is inert: it does not reset gates or
rotate state identity. A receipt reports completion at that moment, not lasting
tracking or a clear path. Inspect again for current state.

## Ownership and recovery

Shared Maestro enable refuses a competing actor rather than taking over its
movement. User opt-in and presentation changes can coexist with other actors and
programs. Disabling a stick can also coexist with other runs. Manual Maestro
takeover retains its higher priority; it uses the same transition and neutral-input
logic but can interrupt the affected actor. Presentation never rewinds the world.
The finishing physical tool click is permitted; shared calls wait for released
input. B/Y and palm Recall recover the book/tools through their separate placement
transaction; they do not reset movement modes, actors or the world.

Live modes are never persisted. Focus/head-tracking loss or pause restores MR,
disables movement and requires explicit reenable. Controller tracking loss stops
that controller's input and requires neutral after tracking returns. Returning to
MR changes presentation without moving the physical tracking origin or undoing
world travel. The workspace saves an authored viewpoint bookmark; physical
tracking is never rewritten by these controls. Cancelling an
already completed action does not reverse it: use a fresh explicit disable or MR
request. Saved preferences remain separate from temporary room Undo/Discard.

## Verification limits

Native journeys exercise actual sampled inputs, visible avatar motion, user
world-frame translation/turning with an unchanged physical origin, scheduler ownership, fresh state checks, held input,
focus/tracking loss and recovery. The generated book flow replays captured native
receipts and proves exact typed requests; it does not operate a headset. Physical
Quest passthrough alignment, comfort, controller/hand switching, provider intent
and Store acceptance remain release gates.
