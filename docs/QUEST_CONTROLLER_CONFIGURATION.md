# Shared controller configuration

`controller.configure` connects the physical movement tray, generated book fields
and delegated room agent to the same `MovementControls` preference save. It edits
controller settings. Separate `controller.mode.set` actions now expose explicit live
movement and MR/Virtual changes through the same native controls. Original Maestro owns conversation and delegation.

## Inspect and edit

Read `controller.settings` before each edit. Its native `configurationId` identifies
the accepted preferences in the current room runtime. Every successful manual or
shared save rotates that ID; rejected writes leave it unchanged. Reopening a room
issues a new ID, so old requests cannot change a new runtime even when the saved
values happen to match. Native action receipts separately deduplicate execution.

The fact includes the two stick assignments, user walking speed, dead zone, all
four editable buttons, and live movement/view flags. `storageReady: false` means
the saved preference file is unavailable for editing; displayed values may be
startup fallback defaults. A true value does not override workspace preservation
or other action readiness checks. Program availability means the exact saved
program exists and parses, not that all its targets and assets are ready.

| Operation | Changes | Preserves |
|---|---|---|
| `movement.left`, `movement.right`, `movement.none` | Maestro stick from the operation; `userStick`, `deadZone`, `userSpeed` from explicit fields | All button assignments |
| `button.program` | One `button` bound to an exact existing `programId` | Movement and other buttons |
| `button.none`, `button.snapLeft`, `button.snapRight` | One button cleared or assigned a snap turn | Movement and other buttons |

Movement schemas permit every independent assignment, including disabling either
or both sticks, but reject sharing one stick. Speed is 0.2–1.2 m/s and dead zone
0.1–0.4, matching the physical controls. Editable button names are `x`, `a`,
`leftStickClick` and `rightStickClick`. B/Y, system input, trigger/grip and palm
Recall are not reassignable through this capability. New program bindings require
a readable saved program. Existing missing bindings remain visible and can be
replaced or cleared without blocking unrelated preference changes.

## Save and interaction behavior

Saving never enables movement, enters Virtual view or runs a bound program.
Existing enabled modes remain enabled unless their stick becomes `none`. Input
gates reset after every successful save: sticks must return to neutral and buttons
must be released before new input acts. A held button therefore cannot trigger
its newly assigned program. Later presses use the same native scheduler as solid
controller-mounted program buttons; holding does not repeatedly restart it.

Both entries use one completion operation and the shared ordered edit queue.
The worker saves a detached preference copy; the active bindings and identity stay
unchanged until Unity receives confirmed success. The physical tray shows saving
status, and the shared action receipt remains preparing. The physical
`MovementControls.Apply` return value reports request admission, not durable
completion; consumers observe changes or the shared receipt. Disk work does not
run on the input thread. Before dispatch, Stop leaves settings unchanged. After
dispatch, saving continues even when the caller stops observing; confirmed settings
can still apply. Inspect the current settings before making another request.

Failed writes keep prior live settings. An ordinary failed write permits explicit
retry with a new receipt. An independently changed/newer file or unconfirmed
publication preserves the files and blocks further writes until reopening or
recovery. Duplicate receipts return their retained outcome without saving again.
Completed output retains its exact configuration identity even after another edit;
it is not a live settings observation. Inspection and saved program IDs never
grant object-edit authority.

Preferences persist independently in the selected workspace, including during a
temporary room session. Room Undo and temporary Discard do not revert them. To
restore prior settings, inspect the current ID and explicitly set the prior
values. This preserves the existing physical tray's persistence boundary. Activity
review may still allow manual preference edits while movement stays stopped;
a workspace preservation/write freeze blocks both entry paths. An accepted writer
is retained until file saving and native application finish, so a backup cannot
capture old live preferences alongside the new preference file. Closing the native
owner ends live completion immediately while its detached writer drains before
workspace reuse. Settings never join geometry Undo or create a scene revision.

## Verification and remaining scope

Native journeys exercise actual controller preference files, durable execution
receipts, the program scheduler and sampled button input. Coverage includes
manual/shared identity changes, exact program bindings, held-button release,
stale and reserved inputs, missing programs, failed writes/retry, restart,
newer-format storage, temporary rooms and workspace preservation. Delayed-worker
regressions cover caller-copy isolation, old bindings during saving, queued stale
edits, cancellation on either side of dispatch, ambiguous publication, listener
failure and native-owner destruction with workspace retirement. The fact fits
the existing structured-value budget with all four program buttons populated.

The full offline headless journey changes and restores actual Unity preferences,
inspects an earlier immutable completion and replays its receipt without changing
the restored settings. The full original-book journey edits the visible generated
fields, waits for native completion and restores the starting preferences. Both
check unchanged geometry revision and no implicit movement opt-in.

Book contract and lightweight browser tests also use exact captured native results. They verify
generated fields, reserved-button exclusion and the exact requests emitted, but
do not prove real headset input or a provider's interpretation of spoken intent.
See QUEST_CONTROLLER_MODES.md for live opt-ins. Physical Quest comfort,
controller/hand switching, provider and Store gates remain unverified.
