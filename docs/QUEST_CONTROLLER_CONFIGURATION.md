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

The save persists before accepting the new configuration and notifying the tray.
Failed writes keep the accepted values and identity for explicit retry with a new
execution request. Duplicate receipts return their retained outcome without
saving again. Inspection and saved program IDs never grant object-edit authority.

Preferences save immediately in the selected workspace, including during a
temporary room session. Room Undo and temporary Discard do not revert them. To
restore prior settings, inspect the current ID and explicitly set the prior
values. This preserves the existing physical tray's persistence boundary. Activity
review may still allow manual preference edits while movement stays stopped;
a workspace preservation/write freeze blocks both entry paths.

## Verification and remaining scope

Native journeys exercise actual controller preference files, durable execution
receipts, the program scheduler and sampled button input. Coverage includes
manual/shared identity changes, exact program bindings, held-button release,
stale and reserved inputs, missing programs, failed writes/retry, restart,
newer-format storage, temporary rooms and workspace preservation. The fact fits
the existing structured-value budget with all four program buttons populated.

Book contract and browser tests use exact captured native results. They verify
generated fields, reserved-button exclusion and the exact requests emitted, but
do not prove real headset input or a provider's interpretation of spoken intent.
See QUEST_CONTROLLER_MODES.md for live opt-ins. Physical Quest comfort,
controller/hand switching, provider and Store gates remain unverified.
