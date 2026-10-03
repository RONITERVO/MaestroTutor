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
after Recall returns its old receipt without re-enabling anything.

| Operation | Result |
|---|---|
| `maestro.enable` / `maestro.disable` | Opt Maestro's bound stick in or out; enabling requires its loaded rig and running aligned room physics |
| `user.enable` / `user.disable` | Opt the user's separate stick in or out; enabling requires Virtual view |
| `view.virtual` | Hide passthrough and show the virtual floor; does not enable either stick |
| `view.mixedReality` | Disable both sticks, restore the captured physical origin and camera settings, and pause physics for alignment review |

An enable resets input gates. A held stick must return to neutral and a held
button must be released before input can act. The action itself never supplies
movement input. Existing collision and navigation checks still govern physical
steps. An already satisfied mode request is inert: it does not reset gates or
rotate state identity. A receipt reports completion at that moment, not lasting
tracking or a clear path. Inspect again for current state.

## Ownership and recovery

Shared enables/view changes require other runs and animation authoring to finish.
They refuse an active actor instead of stopping it. The current invocation is
excluded from that scheduler check, so it can finish normally. Disabling a stick
can coexist with other runs. Manual tray takeover retains its higher priority;
it uses the same transition and neutral-input logic but can interrupt work.
The finishing physical tool click is permitted; shared calls wait for released
input. B/Y and palm Recall remain immediate recovery controls.

Live modes are never persisted. Focus/head-tracking loss or pause restores MR,
disables movement and requires explicit reenable. Controller tracking loss stops
that controller's input and requires neutral after tracking returns. Returning to
MR restores the original local camera origin, even after walking and snap turns;
artificial camera offsets are never saved as room placement. Cancelling an
already completed action does not reverse it: use a fresh explicit disable or MR
request. Saved preferences remain separate from temporary room Undo/Discard.

## Verification limits

Native journeys exercise actual sampled inputs, visible avatar motion, user
origin translation/turning, scheduler ownership, fresh state checks, held input,
focus/tracking loss and recovery. The generated book flow replays captured native
receipts and proves exact typed requests; it does not operate a headset. Physical
Quest passthrough alignment, comfort, controller/hand switching, provider intent
and Store acceptance remain release gates.
