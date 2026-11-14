# Maestro Quest current delivery status

Status: active implementation; **not ready for Store release**. This is the
canonical current release checklist and operational status. Read the
[accepted v1 scope](QUEST_V1_SCOPE.md) for requirements and exclusions, and the
[checkpoint index](quest/checkpoints/README.md) for dated implementation evidence.
Historical approvals or green tests do not establish acceptance of later builds.

## Operational holds

Device/ADB and paid-provider testing, release signing, deployments and
Meta changes remain on hold. Continue unblocked implementation, offline
verification and the existing draft PR; do not request held actions just to mark
a release row complete. These are carried-forward restrictions from the
[recorded release checklist](https://github.com/RONITERVO/MaestroTutor/blob/f7a252feafcea470ddffe78df62512cd63a04de7/docs/QUEST_V1_PLAN.md#release-evidence-still-required),
not new approval requirements. A later owner instruction can change their scope.
Dated device or deployment authorizations elsewhere describe their recorded work.

The chosen audience is **18+**. The original Maestro backend owns identity,
subscription/credits and managed provider access; BYOK remains supported.
Chat history is currently local IndexedDB: a shared backend does not establish
cross-device chat synchronization. Hair/cloth interaction physics and arbitrary
executable native-world scripts remain excluded from v1. The original app's
supported interactive HTML/SVG artifacts remain in scope.

## Source and candidate identities

These are distinct evidence records, not interchangeable release candidates.
Device and Meta rows are the **last recorded observations**, not fresh external
checks while those operations are held. APK values below are SHA-256 prefixes.
Some older records retain only prefixes; they do not independently verify current
package bytes or acceptance. The signed-package record retains its full hash.

| Record | Identity and evidence boundary |
| --- | --- |
| Current frozen runtime source | **Uncommitted included-world increment**, branch `codex/maestro-quest-v1`, [draft PR #248](https://github.com/RONITERVO/MaestroTutor/pull/248). Catalog-v4 pins 3,281 unchanged native inputs. The latest web prebuild covers 5,035 broad inputs; earlier full Headless used its recorded 5,034-input map before the web/driver fix. The fresh Android compilation below uses this frozen source; no new runtime commit or installed candidate is implied. |
| Previous committed runtime checkpoint | `1db401172e68b1f00d4b64f7784fdba1336f4675`. Its verified frozen content matches the commit after Git line-ending normalization. This historical static-support checkpoint predates the current included-world source. |
| Last verified installed development app | APK `D85F3CB7`, source `f58dab48`; later desktop source has not received physical acceptance. |
| Last packaged development APK | `5899972E`, source `5100f612`; recorded as not installed. |
| Latest verified unsigned Android compilation | `Android-compile-20261114-072523-02ff8c01`, from the current frozen uncommitted source; development package/profile, no APK/AAB, signing or installation. [Included-world checkpoint](quest/checkpoints/2026-11.md#included-editable-world-lifecycle--2026-11-14). The earlier `020734-02eff427` compilation remains historical static-support evidence. |
| Signed/private Meta checkpoint | APK `DA689683`, source `d7d5d847`, app `1763835394893209`, package `com.maestro.quest`, build `1767008267909255`. Owner screenshots later confirmed it current in private Alpha. Package malware/security checks passed; VRC/Store/account/provider acceptance did not. [Signed package](QUEST_RELEASE_BUILD.md#corrected-signed-quest-3-candidate--2026-10-06) and [channel observation](https://github.com/RONITERVO/MaestroTutor/blob/f7a252feafcea470ddffe78df62512cd63a04de7/docs/QUEST_V1_PLAN.md#L471-L480). |
| Backend rollout | Two Quest bootstrap endpoints were deployed but disabled; the temporary private diagnostic was removed. [Rollout record](QUEST_MANAGED_ACCESS.md#scoped-backend-rollout--2026-10-06). Existing application/account/provider rollout remains separate. |

The supported local toolchain is Unity **6000.3.24f1** with Android support and an
owned short-path build mirror. Current build configuration targets Quest 3;
broader headset acceptance is not inferred from a dashboard device list.
[Build instructions](../unity/README.md) retain the exact verification commands.

## Release evidence still required

Every row remains open until the chosen release candidate satisfies its full
scope. Implementation, desktop tests and an older device confirmation are useful
evidence, but cannot substitute for the remaining acceptance in that row.

| Requirement | Current evidence | Remaining work and acceptance limits |
| --- | --- | --- |
| Familiar language-learning chat and full-page book | Original web components, both book layouts, inline artifacts and ribbon bookmark; recorded offline Headless and visible Book journeys against their named desktop Unity sources. A separate production-entry offline English/Spanish lesson covers onboarding, proactive artifact plus image, chat/native/Live composition and a native restart after completed saves. These use scripted provider responses and synthetic microphone input; the focused Book fixture alone does not cover every Live/media controller. | Fresh first-lesson, smalltalk, proactive artifact/tool combinations, image, weather, room actions and text-to-Live continuation on the chosen Quest candidate, through both managed and BYOK providers. See [coverage](QUEST_AGENT_RELEASE_COVERAGE.md) and [device QA](QUEST_DEVICE_QA.md). |
| Language-first, shared user/agent actions | Shared capability catalog, generated user controls, event-driven programs, traces, cancellation and ownership; deterministic and actual Unity integration coverage. | Real-provider multi-step completion, recovery and understandable learner feedback on the release candidate. Offline execution cannot establish model reliability. |
| Creation, import and reusable authored worlds | Primitive/recipe/drawing/template creation, GLB/VRM models and motion collections, materials/images, editable constructions and shared saved completion. The included editable Observatory now prepares as an explicitly chosen new workspace, preserves the previous accepted world and uses archive-covered historical provenance. See the [included-world contract](quest/plans/DEFAULT_WORLD.md). | Candidate import/rendering, memory pressure, interrupted work and restore acceptance with representative user content. Validate the declared capacity; the current 256 saved creations, 128 native roots, four imported room objects, 16 liquid containers, 25-metre bounds and four height fields are not a city-scale promise. See [world authoring](QUEST_WORLD_AUTHORING.md) and [model imports](QUEST_MODEL_IMPORT.md). |
| Default Maestro, custom avatars and animation | Included avatar/motions, exact compatible motion IDs, activity/gait bindings, joint posing, recording, shared walk/follow and attention paths; optional exact-model foot fitting through the shared catalog. | Remaining default-motion curation toward the accepted several-hundred-motion direction, current-candidate motion fidelity, humanoid variety, hands-on posing, transitions and sustained performance. Bounded foot fitting uses a rest-bound shoe-plane estimate with explicit trims; visual calibration, real-headset acceptance and general crowd/interior planning remain unfinished. See [animation authoring](QUEST_ANIMATION_AUTHORING.md) and [library](QUEST_ANIMATION_LIBRARY.md). |
| One world with adjustable reality | Per-entity real-room collision participation, accepted virtual ground, swept movement, saved visual layers, passthrough windows and separate camera-source paths have desktop evidence. Older physical depth-occlusion/alignment checks are retained. | Same-candidate physical alignment, independent collision/visibility/audio policy, virtual movement, camera permission/source lifecycle, hand/controller occlusion and recovery. An actor opting out of the real floor still needs valid virtual support. Ordinary fixed platforms and compound stairs now use the [shared support role](quest/plans/STATIC_SUPPORT.md), with desktop traversal/readiness tests. Current-candidate physical and sustained-use acceptance remain open. |
| Living terrain, water, weather and objects | Bounded local physics, contact events, terrain, lighting/weather/time, live water quantities, shared grounded policies and owned submerged travel; surface swimming, shore transitions and continuous following have complete desktop and unsigned Android compilation evidence. Observation-driven residency/scheduling remains shared. See the [surface movement evidence](QUEST_V1_PLAN.md#surface-swimming). | Mixed-scene long-session behavior, allocation/thermal/frame budgets and physical input acceptance. Named areas support independent or together loading. Explicit terrain-edge overflow now shares the bounded stream and liquid journal. Broader region streaming, river/landscape flow, underwater interaction and candidate headset acceptance remain open; retain them as explicit unfinished scope rather than silently claiming a complete living-world system. |
| Speech and reusable world audio | Shared output/source ownership, object/joint emitters, finite imports and a bounded live MP3 adapter; direct obstruction is active. | Intelligible simultaneous user speech and world playback, actual audibility/latency, focus/route recovery and Quest performance. The native echo candidate still has 31 aligned quality failures. Reflection rendering/map scheduling remain disabled pending the retained crash/deadline and capture issues. See [audio](../unity/AUDIO.md). |
| Durable user work and recovery | Ordered accepted edits, captured identities/revisions, save completion, undo boundaries, lifecycle checkpoints, backup/restore and failure-injection checks; full offline journeys include confirmed storage checkpoints. | Current Android suspend/process-loss, low-space, interrupted import/save and recovery acceptance. A checkpoint observed during a desktop journey is not end-of-process durability or headset evidence. |
| Hands/controllers, accessibility and sustained use | Prior owner device confirmations plus current physical-control PlayMode tests and shared native handlers. | Recheck current controls, recall, binding conflicts, seated/standing use, text, reduced motion, comfort and sustained mixed-scene performance on Quest. Desktop timing and a short older device session do not satisfy this gate. |
| Identity, managed access, signing and Store review | Meta app 1763835394893209 / package com.maestro.quest; dedicated signing-key USB/paper recovery verified; private Alpha build 1767008267909255 passed malware/security checks. Two Quest bootstrap endpoints are deployed but disabled. | Enable and verify only through the approved rollout process; production account/entitlement/provider access, current signed candidate, privacy/audience/listing/purchase requirements, VRC review and submission evidence remain separate gates. See [release build](QUEST_RELEASE_BUILD.md), [managed access](QUEST_MANAGED_ACCESS.md) and [privacy](QUEST_PRIVACY.md). |

## Latest verified proof

The current included-world evidence refers to **frozen uncommitted source**:
3,281 unchanged native inputs and sixteen protected files. Latest web/prebuild
inputs total 5,035; earlier Headless retains its own 5,034-input map before the
five-file web/driver correction. Earlier broad runs, focused corrections and the
final test-only type annotation have separate source boundaries below.
The [included-world checkpoint](quest/checkpoints/2026-11.md#included-editable-world-lifecycle--2026-11-14)
records exact identities and preserved failures. Ignored `.quest-evidence/` paths
are local receipts, not public downloads. Older installed, signed and compiled
candidates above remain historical.

| Proof | Result and limitation |
| --- | --- |
| Broad native baseline | 2,380 EditMode passes/thirteen Windows link-privilege skips and 396 selected unique PlayMode cases with passing union evidence. These runs precede the final fact typing repair; they were not all rerun on the final source. |
| Final focused native checks | Fourteen EditMode cases pass across ten-plus-four actual selections on identical inputs; the first wrapper's missing-case failure is retained. A clean fourteen-case PlayMode run includes three new runtime/RoomAgent fact-pipeline cases. Overlapping counts are not added to the baseline. |
| Web and shared drivers | Fresh Node22.23.4 suite: 4,071 tests in 398 files, no snapshot updates; 173 focused shared/UI/driver checks also pass. These runs precede only a 23-byte erased test annotation, verified by exact reverse-byte proof; production TypeScript checking passes afterward. Earlier 4,010/45-case evidence remains historical. |
| Native catalog | Catalog-v4 `00abc3a7…777df95a`: 151 actions, 256 facts, nineteen events and 856 source-provenance entries. Schemas are unchanged from v3; only the corrected fact reader's source hash changed. |
| Focused actual Unity workflow | Run `bd49cf4c87f64b809e31b7439edc8bf5` passes prepare/cancel/open/review/paint/Undo/restore with clean client/Editor/root exits and independent review. This focused ready-fixture check is separate from the full journeys. |
| Full offline Headless | Run `3ef2a16c0b034b0f8ff469c228e23140` passes 113 ordinary leaves plus the included-world workflow; all fourteen original saved/native identities are restored. Clean exits and independent review on its named inputs. Native/catalog remain unchanged; later web/driver changes have separate coverage. Synthetic media/physics, not provider or physical Quest acceptance. |
| Focused rendered Book | Run `fb4ad34c26b44a39adc72839de5eb0dd` passes with clean exits, independent review and all five original fixture objects restored. Five advanced catalog-page captures were inspected; this is not a 3D-scene, novice-lesson or Quest optical review. |
| Full rendered Book | v4 run `1c681381df5845bf9911b12115fc8994` passes all 71 ordinary leaves plus the pack workflow, with 11 completed pack actions and all 13 original objects restored exactly. Independent review finds no recorded errors. Five advanced catalog pages were visually inspected; this is not a 3D scene, novice chooser or Quest optics review. |
| Android compilation | v6 fresh export/compile and independent review pass: 41 executed tasks, 22 selected ARM64 libraries with at least 16 KiB ELF LOAD alignment, 154 exact web payloads and the exact included-world source. All 5,035 broad/3,281 native inputs and sixteen protected files match. NativeBrowser 128 passes/two skips/six lint warnings are exact prior reuse. Build warnings remain documented; no APK/AAB, signing, installation or final-package filtering proof. |
| Web editing feedback | Current action drafts, requests and previous results are distinct; technical argument detail is collapsed below the editable fields. 116 focused UI tests, four development-replay contract cases, full app lint and production build pass on Node 22.23.4. Rendered replay evidence and limits are recorded in the [November checkpoint](quest/checkpoints/2026-11.md#action-feedback-and-default-world-integration-review--2026-11-14). No new Unity/Android/device claim. |
| Per-pushed-head CI | [PR #248 checks](https://github.com/RONITERVO/MaestroTutor/pull/248/checks) are authoritative for the currently pushed head. Local checks and historical green runs do not establish CI for a new head. |
| Dependency audits | November 14 compatible backend lock updates repair newly reported advisories. Installed production app, Functions and gateway audits have zero findings; backend units/builds and all five emulator groups pass. Current-head CI must renew that evidence; development-tooling audits are separate. [Dependency review](QUEST_DEPENDENCY_REVIEW.md#backend-advisory-refresh--2026-11-14). |
| Audio quality | Regression contracts pass, while stock/stationary32/measured32 retain 32/56/31 open quality failures. Reflections remain disabled. Actual route, audibility, simultaneous speech and mixed-scene acceptance remain required. [Audio contract](../unity/AUDIO.md). |

## Current work and next acceptance

Ordinary fixed platforms and explicit compound stairs now share accepted primary
collision, ground dependencies, physics, foot sampling and navigation. Their saved
intent survives movement/ownership suspension, Undo/Redo and archives; bounded
navigation reservations remain charged through native completion and destruction.
See the [support contract](quest/plans/STATIC_SUPPORT.md). Moving platforms,
automatic arbitrary-stair conversion, larger-world streaming and sustained Quest
performance are not supplied by this increment.

The workshop now distinguishes current draft/request feedback from previous
action results and puts technical schema detail in the expandable reference.
The proposed default world remains source-reviewed, with ownership gaps recorded
in the [integration contract](quest/plans/DEFAULT_WORLD.md). Its separate scene
owners have not been merged. A bounded shared-world path now prepares the included
editable Observatory through the existing workspace lifecycle and archive-covered
provenance. Final focused checks, full offline Headless and rendered Book journeys,
and unsigned Android compilation pass within the evidence boundaries above. The wider
Cosmos content, learner-facing world selection, additive multi-document merging,
shared lesson/attention integration, visual-cache refresh, whole-world metadata
synchronization and broader region/route costs remain work.

Current public limits remain 256 saved creations plus book/Maestro, 128 native
roots, four imported room objects, sixteen liquid containers, 25-metre authored
bounds and four height fields.
These are implementation constraints to verify and evolve, not a replacement for
the accepted larger living-world direction or proof of city-scale support.
[World authoring](QUEST_WORLD_AUTHORING.md) and the
[larger-world gate](QUEST_V1_PLAN.md#larger-world-capacity-implementation-gate)
retain the remaining design/acceptance requirements.

Keep future implementation evidence in dated checkpoint files; update this page's
proof pointer and affected gate rather than accumulating parallel current-status
claims. Preserve old source/build identifiers, failed attempts and superseded
permissions as history. Do not mark the complete v1 goal achieved until every
required release gate has matching current-candidate evidence.

Operational guides: [device QA](QUEST_DEVICE_QA.md),
[agent/provider coverage](QUEST_AGENT_RELEASE_COVERAGE.md),
[native probe](QUEST_NATIVE_ROOM_PROBE.md), [packaging](QUEST_RELEASE_BUILD.md),
[managed access](QUEST_MANAGED_ACCESS.md), [privacy](QUEST_PRIVACY.md).
The separate [Play Store checklist](RELEASE_CHECKLIST.md) covers the original
Android app and is not the Quest release checklist.
