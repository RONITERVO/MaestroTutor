# Named recipe-part playback

The existing `animation.play@1` action accepts another typed variant when
`recipePartPlayback.v1` is advertised:

```json
{"target":"<created recipe object ID>","source":{"kind":"recipe","part":"RightUpperArm"},"channel":"recipePart","seconds":1,"loop":false}
```

The exact named part must have a saved recipe rotation track. Use the shared
`object.recipe.edit` action or book recipe editor to author its keys. This action
plays those same keys and does not introduce another motion format. An imported
GLB/VRM is not a recipe; its skeletal animations retain their existing routes.

Each execution owns `recipePart:<part ID>` on its target. Distinct parts can run
concurrently, including parent and child joints: each writes only its own local
rotation, while ancestor transforms naturally carry descendants. Two actions on
the same part conflict. Whole-object playback, saved edits, authoring and grips
still conflict with every part channel. A physical grip has the existing higher
priority and cancels affected programs; parallel-program cancellation stops the
whole related group. No priority or automatic-resume argument is exposed.

Each selected track starts at time zero on the scheduler’s unscaled clock. `seconds:0` uses the
recipe's saved duration. `loop` repeats the track during the requested lifetime;
a non-looping track holds its final key if the lifetime is longer. Completion
samples the requested endpoint. Stop/cancellation freezes the latest sampled pose.
Neither completion nor Stop restores a former root transform or edits saved keys.

Starting a part action suppresses whole-recipe autoplay on that same part for the
current recipe instance. Other autoplay tracks continue. Suppression remains after
the action ends, preventing silent takeover or restart. An explicit whole-recipe
Restart clears suppression and restarts all tracks. Recipe replacement creates a
fresh evaluator; old handles cannot control it. Pause/focus loss, disabling the
object and the room runtime hold cancel playback; focus return alone never resumes.
Saved autoplay preferences retain their existing meaning on a fresh room load.

Part animation leaves the root rigid-body state and stable whole-assembly proxy
collider unchanged. These are visual joints, not independently colliding physics
links. Root motion and separate grabbable physical links still require the relevant
whole-object capabilities. Named parts can now anchor a carried prop through
`object.hold`; see QUEST_OBJECT_ATTACHMENTS.md. This does not create physics joints.

`object.recipe.pose@1 {target,part}` reports the current local and world
position/quaternion, parent ID, saved object revision and track playback flag.
Parts without tracks remain observable. Missing/inactive/non-recipe parts fail
explicitly. World coordinates depend on the current room origin. Reading a pose
grants no mutation authority and never writes it back to the saved recipe.

The catalog supplies the same source selector and part field to the book's typed
block editor and the app agent. Programs can bind part IDs through scalar
expressions and use parallel functions; no second interpreter or model tool exists.
Ownership inspection names the actual parts. Source/feature validation prevents a
saved program from silently degrading to whole-object playback on older runtimes.

## Verification

Native checks cover part/whole claims, schema rejection, the exact shared parallel
program, independent parent/child playback, live pose readback, saved-data and root
physics preservation, explicit autoplay restart, missing tracks, conflicting work,
recipe replacement, disabled objects, lifecycle pause and real XRI grip cancellation.
Web checks cover the same source variant, resource/feature gates and typed authoring.
Physical Quest performance and user acceptance remain release gates.
