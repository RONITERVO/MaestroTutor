# Typed object creation

Development checkpoint, 2026-09-28. The public catalog now exposes one
object.create action with primitive and recipe kinds, replacing two prototype
creation IDs. Together with animation consolidation, this leaves thirteen public
actions. Existing imported-model workflows remain available separately; this
change does not add model generation or bypass asset validation.

## Contract and behavior

A shape call uses the same placement, scale and color inputs as before, plus an
explicit kind:

    {"id":"object.create","version":1,"arguments":{
      "kind":"primitive","shape":"ball","name":"Practice ball",
      "x":0.3,"y":1.3,"z":0.65,"scale":1,"red":0.2,"green":0.6,"blue":0.9
    }}

The recipe kind accepts name, x/y/z, scale and the complete recipe object. Its
native example is the existing nineteen-part robot with two animation tracks,
created idle. Shape-specific fields are forbidden on recipes and recipe data is
forbidden on shapes. The kind selector stays literal; valid scalar parameters
can be expression-bound. Recipe arrays stay literal and editable. Native domain,
room capacity, aggregate geometry and storage checks still apply.

Both kinds return the exact objectId only after saving. Existing native operations
still create through RoomEditor, with one room Undo edit per creation. Stop never
erases completed creations. Duplicate one-off receipt delivery returns the same
historical result without creating again. Programs retain their native-result
authority and per-run creation limits. This checkpoint deliberately preserves
saved-edit semantics; runtime-only edits and grouped commits need their own design
and verification.

## Shared authoring

The discriminated schema supplies choice titles, descriptions, complete examples
and per-kind native feature requirements. The book's visual blocks and action
catalog use those choices. Switching kind keeps compatible fields and expression
wiring, removes incompatible fields, and preserves the existing result destination.
Even an invalid catalog draft retains its other valid fields when kind changes.

The physical tray uses the same schema title and examples. Its choice changes a
detached draft. Apply makes one revision-checked behaviour Undo edit without
creating anything. The full source and structure remain editable in the book.
The detailed recipe can also be authored through the existing app-owned agent.
These expert controls supplement the language-first flow.

Existing simple, prop and motion editors still use private adapters. Their field
paths and literal selectors now come from native-generated descriptors instead of
animation-specific web/native branches. Scheduler execution continues directly
through native modules; no numeric RuleStep enters execution. New public kinds
need no new enum entry or specialized editor. A new kind still requires actual
native implementation, validation, examples and meaningful tests.

## Prototype compatibility

The retired object.create.primitive and object.create.recipe IDs are no longer
public calls. Existing prototype source is retained as unavailable by per-program
compatibility isolation; it is never silently rewritten or executed. Old receipt
history uses the existing explicit recovery path if incompatible. No installed
room/chat/behaviour data has been reset, and no imported model files were moved.
This change precedes the stable v1 release contract.

## Evidence and remaining work

A shared native/web fixture validates all three primitive shapes, a complete
recipe and malformed/mixed-kind requests. Native tests cover exact created IDs,
real robot joint movement, save failure, capacity, duplicate receipts, unrelated
playback, persistence and Undo through the unified calls. A real physical-pointer
test changes kind, preserves result wiring, applies without creating, opens the
same book program, then creates and undoes the robot only after an explicit run.

Browser replay fixtures come from actual Unity outputs with local provenance.
A real Chrome probe replays the physical draft, selects both kinds, edits a name
and expression, and verifies the exact complete save request. It starts no action
and supplies no simulated acknowledgement for that save. The separate catalog
probe verifies the native recipe example with simulated fixture query/edit replies.
Native and book screenshots are inspected; these are PC checks, not headset
acceptance.

Spatial vocabulary, runtime/grouped commits, arbitration, richer world events,
collections/shared libraries, model import consolidation and generic version
negotiation remain open. Quest testing, provider journeys, final default assets
and Meta store acceptance are still release gates.

PC verification: 1,341 app tests across 157 files, 159 EditMode and 106 PlayMode
tests pass; three optional private-model/collection tests are skipped. TypeScript,
lint, catalog provenance, code/prompt guards and 25 Android bridge tests pass.
The ARM64 IL2CPP development APK builds and its v2 signature verifies. All 116
native runtime sources match the tested mirror and all 112 packaged web files
match a final production rebuild byte-for-byte. APK SHA256:
C4F00E349CBA84C854D98323E442455446E237504D6DB08CC1E9E5109BAADB68.
The APK has not been installed or accepted on Quest.
