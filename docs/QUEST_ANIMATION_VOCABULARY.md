# Typed animation sources

Development checkpoint, 2026-09-28. The public catalog has one animation.play
verb, replacing six prototype animation/gesture IDs. Fourteen public actions
remain. This is the animation portion of vocabulary consolidation; creation,
spatial verbs and the other release architecture work remain separate.

## Public call

A library selection is explicit data, for example:

    {"id":"animation.play","version":1,"arguments":{
      "target":"maestro","seconds":2,"loop":false,"channel":"wholeTarget",
      "source":{"kind":"library","motionId":"0123456789abcdef0123456789abcdef"}
    }}

The example ID illustrates the shape only. Real calls use an exact downloaded
motion ID returned by the native library. Playback never replaces it by a tag,
fallback motion, similarly named clip or a different rig.

The supported combinations are:

| Source | Source fields | Channels |
| --- | --- | --- |
| gesture | gesture name | wholeTarget or upperBody |
| recording | target's saved recording | wholeTarget |
| embedded | exact modelHash and clipIndex | wholeTarget |
| library | exact motionId | wholeTarget |
| recipe | target's saved recipe tracks | wholeTarget |

Only supported inputs appear in each variant. Upper-body gestures exclude Walk
and props. Full-body gesture/recording/embedded/library sources retain fitted
props; recipe playback has none. Each source keeps its prior duration/loop
constraints, readiness, graph/clip/lease ownership and cancellation behavior.
Recorded throwing remains a separate physical release action.

## One schema, several editors

The native registration generates a discriminated oneOf schema. String selector
paths are listed in x-discriminators; x-static selectors must be literal. Each
variant includes its own channels and prerequisites. The definition's channel
list describes possibilities; the actual validated call claims only the selected
source's channels and prop. Search includes source titles and prerequisites.

The book's generated form selects a source/channel and displays its fields. An
explicit source change keeps compatible values, removes incompatible fields and
keeps compatible expression wiring. Editing duration leaves exact motion/model
identities untouched. The physical tray offers the same Source and channel
choice and schema fields; wired source changes use the full book editor. Apply
saves a revision-checked draft as one Undo edit and never starts playback.

Scalar fields inside objects can use dotted binding paths, such as
source.gesture, source.clipIndex or prop.objectId. Source/channel selectors and
arrays cannot be bound. Every binding needs a schema-valid literal placeholder;
computed values are validated again before native dispatch. In version 3,
bound object placeholders do not grant resource authority: execution still needs
a declared existing object or an exact native creation result. Literal source
JSON is copied and never mutated during evaluation.

Version-2 programs retain their conservative whole-object reservation policy.
Version-3 programs and one-off calls reserve the selected operation channels.
This change does not introduce arbitration, arbitrary animation masks or resume.

## Native ownership and prototype compatibility

One animation module selects a typed native source. The existing independent
operation objects still own preparation, playback and cleanup. No numeric enum
or RuleStep enters scheduler dispatch. Numeric simple/prop/motion adapters remain
an explicit authoring boundary; their source mappings and labels are generated
with the manifest. A future source needs a native source implementation and
registration, without adding a public play verb or numeric adapter.

The six old IDs are absent from the public catalog. Older development programs
using them are preserved as unavailable source by the existing compatibility
isolation; they are not rewritten or run under a guessed interpretation. Old
incompatible receipt history retains the existing explicit recovery workflow.
No installed data was reset, no model file moved and no headset queried or updated.
This prototype change precedes the promised stable v1 release contract.

## Evidence and limits

A shared native/web fixture covers all supported combinations and rejects mixed
source fields, unsupported channels, upper-body Walk/props, missing selectors and
tag-based substitutions. Native tests execute recordings, imported/library and
recipe playback, real walking plus upper-body gestures, preparation/cancellation,
prop cleanup and exact-model checks through the unified call. Nested binding
checks preserve typed validation and resource authority.

Physical-pointer tests choose the upper-body variant, retain compatible values,
apply without playback, open the same book program and undo the change. Actual
Unity observations refresh the browser replay fixtures. The Chrome probe opens
that native-authored program, changes its source/channel and nested expression,
and verifies the exact save request. The probe does not execute or simulate an
acknowledgement for its later save. Both native and book screenshots are inspected.

Quest performance/readability, real-provider journeys, final default assets and
Meta release acceptance remain open. No store submission is included.

PC verification: 1,324 app tests across 156 files, 156 EditMode and 105 PlayMode
tests pass; three optional private-model/collection tests are skipped. TypeScript,
lint, catalog provenance, code/prompt guards and 25 Android bridge tests pass.
The ARM64 IL2CPP development APK builds with a verified v2 signature. All 114
native runtime sources match the tested mirror and all 112 packaged web files
match the production build byte-for-byte. APK SHA256:
C1D8087F19463E9C9D0066D710ECCD76EFCA866D93856539BF1E695C89977056.
This package has not been installed or accepted on Quest.
