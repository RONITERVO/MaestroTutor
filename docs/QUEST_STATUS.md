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
| Verified native runtime source | `f7a252feafcea470ddffe78df62512cd63a04de7`, branch `codex/maestro-quest-v1`, [draft PR #248](https://github.com/RONITERVO/MaestroTutor/pull/248). Later documentation-only edits do not constitute a new runtime build. |
| Last verified installed development app | APK `D85F3CB7`, source `f58dab48`; later desktop source has not received physical acceptance. |
| Last packaged development APK | `5899972E`, source `5100f612`; recorded as not installed. |
| Latest verified unsigned Android compilation | `Android-compile-20261101-035933-f8aafacc`; development package/profile, no APK/AAB, signing or installation. [Exact compile checkpoint](QUEST_RELEASE_BUILD.md#incremental-dormant-movement-compile-checkpoint--2026-11-01). |
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
| Creation, import and reusable authored worlds | Primitive/recipe/drawing/template creation, GLB/VRM models and motion collections, materials/images, editable constructions and shared saved completion. | Candidate import/rendering, memory pressure, interrupted work and restore acceptance with representative user content. Validate the declared capacity; the current 256 saved creations, 128 native roots, 16 liquid containers, 25-metre bounds and four height fields are not a city-scale promise. See [world authoring](QUEST_WORLD_AUTHORING.md) and [model imports](QUEST_MODEL_IMPORT.md). |
| Default Maestro, custom avatars and animation | Included avatar/motions, exact compatible motion IDs, activity/gait bindings, joint posing, recording, shared walk/follow and attention paths; optional exact-model foot fitting through the shared catalog. | Remaining default-motion curation toward the accepted several-hundred-motion direction, current-candidate motion fidelity, humanoid variety, hands-on posing, transitions and sustained performance. Bounded foot fitting uses a rest-bound shoe-plane estimate with explicit trims; visual calibration, real-headset acceptance and general crowd/interior planning remain unfinished. See [animation authoring](QUEST_ANIMATION_AUTHORING.md) and [library](QUEST_ANIMATION_LIBRARY.md). |
| One world with adjustable reality | Per-entity real-room collision participation, accepted virtual ground, swept movement, saved visual layers, passthrough windows and separate camera-source paths have desktop evidence. Older physical depth-occlusion/alignment checks are retained. | Same-candidate physical alignment, independent collision/visibility/audio policy, virtual movement, camera permission/source lifecycle, hand/controller occlusion and recovery. An actor opting out of the real floor still needs valid virtual support. |
| Living terrain, water, weather and objects | Bounded local physics, contact events, terrain, lighting/weather/time, live water quantities, shared grounded policies and owned submerged travel; surface swimming, shore transitions and continuous following have complete desktop and unsigned Android compilation evidence. Observation-driven residency/scheduling remains shared. See the [surface movement evidence](QUEST_V1_PLAN.md#surface-swimming). | Mixed-scene long-session behavior, allocation/thermal/frame budgets and physical input acceptance. Named areas support independent or together loading. Explicit terrain-edge overflow now shares the bounded stream and liquid journal. Broader region streaming, river/landscape flow, underwater interaction and candidate headset acceptance remain open; retain them as explicit unfinished scope rather than silently claiming a complete living-world system. |
| Speech and reusable world audio | Shared output/source ownership, object/joint emitters, finite imports and a bounded live MP3 adapter; direct obstruction is active. | Intelligible simultaneous user speech and world playback, actual audibility/latency, focus/route recovery and Quest performance. The native echo candidate still has 31 aligned quality failures. Reflection rendering/map scheduling remain disabled pending the retained crash/deadline and capture issues. See [audio](../unity/AUDIO.md). |
| Durable user work and recovery | Ordered accepted edits, captured identities/revisions, save completion, undo boundaries, lifecycle checkpoints, backup/restore and failure-injection checks; full offline journeys include confirmed storage checkpoints. | Current Android suspend/process-loss, low-space, interrupted import/save and recovery acceptance. A checkpoint observed during a desktop journey is not end-of-process durability or headset evidence. |
| Hands/controllers, accessibility and sustained use | Prior owner device confirmations plus current physical-control PlayMode tests and shared native handlers. | Recheck current controls, recall, binding conflicts, seated/standing use, text, reduced motion, comfort and sustained mixed-scene performance on Quest. Desktop timing and a short older device session do not satisfy this gate. |
| Identity, managed access, signing and Store review | Meta app 1763835394893209 / package com.maestro.quest; dedicated signing-key USB/paper recovery verified; private Alpha build 1767008267909255 passed malware/security checks. Two Quest bootstrap endpoints are deployed but disabled. | Enable and verify only through the approved rollout process; production account/entitlement/provider access, current signed candidate, privacy/audience/listing/purchase requirements, VRC review and submission evidence remain separate gates. See [release build](QUEST_RELEASE_BUILD.md), [managed access](QUEST_MANAGED_ACCESS.md) and [privacy](QUEST_PRIVACY.md). |

## Latest verified proof

The runtime checkpoint is `f7a252fe`; each row states its own scope. Historical
counts are not added to fresh counts. Ignored `.quest-evidence/` paths in the
linked records are local evidence, not downloadable public artifacts.

| Proof | Result and limitation |
| --- | --- |
| Native tests | 2,252 EditMode passes, thirteen explicit Windows symlink-privilege skips; all 48 selected PlayMode cases pass. Not a fresh rerun of every PlayMode method. [Movement checkpoint](QUEST_V1_PLAN.md#incremental-dormant-movement-directory--2026-11-01). |
| Full offline Headless | Run `5ba9033ea62f4ffca2fcb4cc7c640f4f`: 113 ordinary leaves, final acknowledgement 3,389, 3,165 observations / 3,164 readsets, fourteen exact saved/native objects. Clean exits and exact receipts/metadata; synthetic physics/media, no provider. [Scope](QUEST_NATIVE_ROOM_PROBE.md#incremental-dormant-movement-integration--2026-11-01). |
| Earlier rendered Book baseline | Run `6de22a1773ac450f94e2096e3f884a38`: 2,812 acknowledged requests, 8,671 observations, zero browser errors; desktop visual checks and reload identity. Not a new Book run for the native-only movement slice or Quest optical acceptance. [Fact-read checkpoint](QUEST_NATIVE_ROOM_PROBE.md#exact-chat-offer-reads--2026-11-01). |
| Android compilation | All 41 actionable tasks execute; 22 selected ARM64 libraries / 73 LOAD segments pass 16 KiB checks. 152 fresh web payloads match and 4,943 frozen inputs remain unchanged. Five export warnings and nine distinct C# diagnostics are retained; NativeBrowser's 128 passes/two skips and six lint warnings are reused. No final package ABI filtering or device proof. [Record](QUEST_RELEASE_BUILD.md#incremental-dormant-movement-compile-checkpoint--2026-11-01). |
| Runtime-checkpoint CI | [Release gate 41271970320](https://github.com/RONITERVO/MaestroTutor/actions/runs/41271970320) and [native echo 41271970293](https://github.com/RONITERVO/MaestroTutor/actions/runs/41271970293) pass for `f7a252fe`; exact merge/head verified. 3,941 app tests, 118 Functions units including sixteen compatibility cases, all five emulator groups, 31 gateway units plus sixteen compatibility cases, catalog/build/browser/first-lesson checks. These are offline/emulator checks, not real-provider judgement. |
| Production dependency audits | App, Functions and gateway report zero findings under the strict all-severity gate. Narrow compatibility overrides and removal criteria remain explicit. Development-tooling audits are separate. [Dependency review](QUEST_DEPENDENCY_REVIEW.md). |
| Audio quality | Regression contracts pass, while stock/stationary32/measured32 retain 32/56/31 open quality failures. Reflections remain disabled. V1 headphone guidance and actual route, audibility, simultaneous speech and mixed-scene acceptance remain required. [Audio contract](../unity/AUDIO.md). |

## Current work and next acceptance

The last runtime slice updates exact changed dormant movement/collision identities
through an immutable bounded overlay; it preserves native readiness and existing
world limits. Ground/visual cache refresh, whole-world metadata traversal and
mixed-scene Quest performance remain separate work. A smaller operation count in
a 64-object fixture is not larger-world or frame-rate acceptance.

Current public limits remain 256 saved creations plus book/Maestro, 128 native
roots, sixteen liquid containers, 25-metre authored bounds and four height fields.
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
