# Shared object copying

`object.create` now has a `copy` kind alongside primitive and recipe creation.
`objectCopy.v1` advertises support. Read `object.definition` for the exact source
revision and choose a new room-local origin, x/y/z. An empty name preserves the
source name. The included book and Maestro are excluded.

The source retains its identity and data. The copy gets a native-issued object ID,
preserves current rotation/scale, colour, physics settings, drawing geometry,
recipe parts/tracks, imported asset references and recorded object motion. Recorded
frame positions receive the same translation as the object's copied origin. Timing,
rotation, scale and loop remain unchanged. This does not retarget recordings or
force their first frame to coincide with the current pose.

Copies start idle. Recipe tracks remain editable with playing=false. Live playback,
velocity, grabs, program/button bindings and unsaved authoring are excluded.
Imported geometry loads as an independent instance from the same exact asset,
through the existing bounded importer. The action acknowledges saved creation;
it does not claim asynchronous model loading has finished or unavailable bytes
have been repaired. Model and external motion bytes are not duplicated.

The physical Duplicate control calls this same native operation and selects the
new object. Agent/program calls leave selection unchanged. Each copy has one
saved room Undo edit; temporary-room copies stay in the fork until Keep. Failed
validation or storage cannot create a partial object. Receipt replay returns the
historical result, including after Undo, without another copy. A program can use
the returned objectId in later actions through existing result authority and limits.

## Ownership and limits

Copying reserves the source and requires it to be unheld, outside active authoring
and free of another actor's ownership. It does not stop unrelated actors. Its
revision must still match when execution starts. The source's live transform is
sampled then; revision guards authored changes, not every physics frame.

Existing object/model/drawing/frame/recipe aggregate budgets apply to the entire
candidate room. Translated recordings must remain within the room position bound.
There is no new numeric action enum, specialized book editor or provider endpoint.
The catalog supplies the copy fields, feature gate, source resource, ownership and
current-revision mapping to both clients.

`object.definition` reads saved metadata, including position, rotation, scale,
revision and content counts. It reads the active temporary fork when present.
It is not live pose, full geometry, asset availability or edit authority. Display
names are bounded; exact identities and model hashes are preserved.

## Verification boundaries

Native coverage exercises drawings and translated recordings, recipe tracks,
independent imported geometry, physical Duplicate, temporary isolation, Undo,
replay, stale/held/owned sources, failed writes, aggregate limits and returned-ID
program chaining. Web checks use a captured native receipt and definition facts.
Browser replay tests the generated book controls without executing a Quest action.
Physical Quest interaction/performance acceptance remains pending on device hold.
