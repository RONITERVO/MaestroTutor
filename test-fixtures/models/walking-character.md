# Original skinned walking fixture

`walking-character.glb` is original synthetic content under this repository's
Apache-2.0 license. Run `node scripts/create-walking-character-fixture.mjs` from
the repository root to reproduce it. It contains a skinned triangle, 17 named
humanoid joints, and one one-second clip with horizontal hip travel and independent
head and leg rotation. It has no external resources or user-provided content.

The explicit offline `-SyntheticGait` Headless/Book option seeds it through the
normal private model library and native importer. Selection and movement then use
the shared catalog and ordinary action paths. PlayMode tests build the corresponding
fixture in `ModelFixture.WalkingCharacter` and inspect joints and deformed vertices.

This is a pipeline fixture, not a visually plausible walking character, a shipped
avatar, or evidence of foot planting, imported-asset quality or Quest performance.
