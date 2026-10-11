# Editable recipes as shared actions

Historical development checkpoint. The retired creation IDs and test counts below
describe that checkpoint, not the current public contract. See
[Typed object creation](QUEST_CREATION_VOCABULARY.md) for the current
`object.create` primitive/recipe vocabulary and verification.

`object.create.recipe` version 1 creates native geometry and optional animation
tracks using the same room editor, recipe player, scheduler and durable one-off
receipts as existing actions. The book action catalog, simple controls, function
blocks and original-app agent all consume the native definition. The catalog now
contains 14 actions. This is a PC development checkpoint, not Quest acceptance.

## Contract

Inputs are `name`, `x/y/z` in room metres, `scale` (0.1–4), and `recipe`.
Placement is within 25 m of the room origin. The output is the exact new
`objectId`, saved before success is reported. The new assembly uses fixed physics
and retains its complete editable recipe.

The shared schema now supports bounded arrays and nullable root parents.
Recipes contain 1–32 box/sphere/cylinder parts, parent-before-child hierarchy,
positive dimensions, opaque RGB colors and unit quaternions. Native and web
validation also check unique IDs, local reach, matching track targets and time
ordering. There may be up to 17 tracks and 2–16 keys per track; keys begin at zero
and end at the recipe duration (0.1–30 seconds). A room permits at most 256 recipe
parts across its objects and the existing 64-creation capacity.

The catalog exports a detached native example: a 19-part box robot with two
waving tracks. It defaults to `playing=false`. An agent can change that example
or construct another valid recipe; no opaque robot-specific execution tool is
required. CI source hashes include the native recipe validator and template as
well as catalog/program definitions. Unity compares the exported definition
against the committed manifest.

Nested action inspection is bounded to 24,000 argument characters, 4,096 values,
depth 12, 64 entries per generic array, and 128 characters per string. The
stricter recipe schema still applies. The outer command/program transport caps
are unchanged: a large recipe plus surrounding instructions may exceed them.
Split work into explicit actions/programs when needed; no silent truncation.

## Create, then animate

A version-3 invoke can store `results: {objectId: "robot"}`. A later
`animation.recipe.play` uses `bindings: {target: {var: "robot"}}`.
The same native-created resource authority and 16-created-ID run budget apply
as for primitive creation. A guessed ID is not authorized by a placeholder.

Use `recipe.playing=false` for program-controlled playback. Creation stays idle
until the subsequent animation action starts. Stopping that action leaves the
object in the room and stops its current animation. Each creation is one room
Undo entry; Undo removes it and Redo reconstructs the editable object. A later
failure does not roll back completed creation.

Setting `playing=true` explicitly requests saved automatic animation. That
animation belongs to the saved recipe, independently of the completed creation
action. Cancelling a completed creation cannot remove the object or undo its
saved animation request. Use the object's existing playback controls to stop it.

Duplicate one-off delivery returns its retained output without another object.
Receipts preserve the exact JSON request and result across reconstruction.
A completed receipt remains historical after Undo; it does not prove the object
currently exists. Readiness rejection (including the aggregate part limit)
occurs before an effect is authorized.

## Human editing and verification

Simple book controls edit name, placement and scale while preserving all recipe
parts and tracks. The function/source editor can edit the full creation call.
After creation, the existing object inspector edits its hierarchy and tracks.
Normal chat remains the default interface; these are optional workshop controls
inside the book.

Native tests execute create → returned ID → actual joint animation, then Stop,
persistence and Undo/Redo. Additional coverage checks receipt reconstruction,
duplicate delivery, aggregate capacity and invalid hierarchy/keyframes.
Real native program and receipt fixtures also pass through the web contract.
Chrome authoring checks use simulated acknowledgements; they do not execute
native actions or call a provider. The reusable probe is
`scripts/probe-recipe-creation.mjs`; its native inputs are
`test-fixtures/browser/recipeCreationProgram.json` and
`recipeCreationResult.json`.

PC checks passed: 1,271 app tests across 154 files, 125 Unity EditMode and
91 PlayMode tests, TypeScript, lint, catalog provenance and prompt/core ownership.
Three optional private-model/collection tests remain skipped without external
files.

## Remaining release work

Recipe input is structured data, not arbitrary C#/JavaScript. Nested recipe
arrays are literal program arguments; scalar inputs and returned IDs are the
current computable bindings. Drawing, model import and other room edits still
have existing command paths awaiting broader catalog coverage.

Creation serializes a room save before reporting completion. Quest frame-time
and storage-latency measurements remain necessary; an asynchronous completion
lifecycle may be required. This change does not establish performance for many
simultaneously animated objects, add cloth/hair simulation, or complete the
full v1/store acceptance.
