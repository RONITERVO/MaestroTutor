# Quest release packaging

This is a packaging path, not Store acceptance. A dedicated release key has been
prepared locally at the owner's request, with a verified encrypted USB copy.
Independent paper-password recovery is verified. The public release profile now
passes strict configuration checks, and the two Quest bootstrap endpoints are
deployed but disabled. The first owner-approved local candidate is now signed
and audited. Its approved private Alpha draft upload was rejected for an SDK-generated
unsupported device identifier. A corrected Quest 3 candidate is signed and verified,
uploaded successfully as a private draft after replacement approval; details are below. Account/provider
acceptance and server-side Store compatibility results remain open; see [the scoped rollout](QUEST_MANAGED_ACCESS.md#scoped-backend-rollout--2026-10-06).
The existing development command still produces `com.maestro.quest.development`.
The release command prepares a non-development IL2CPP/ARM64 APK from the same
source, web app, native browser, included avatar and motions.

## Private Alpha packaging rejection and target correction — 2026-10-06

The owner authorized draft-only upload of signed candidate **848FFA51** to app
`1763835394893209`, private `ALPHA`, with no publication or tester invitations.
Official Meta CLI sign-in succeeded. The upload command omitted `--publish` and
terminated with exit 1. Platform Utility 208 rejected the manifest device name
`stanley`; no accepted build is verified. The four release channels still report
empty build lists. Channel lists alone do not prove absence of unattached drafts.

The pinned Meta Core SDK 207 enables VR Glasses by default and writes `stanley`
for that target, while Meta's current uploader/public manifest specification uses
`vrglasses`. Maestro does not have VR Glasses acceptance evidence. Both Meta and
OpenXR project configuration now explicitly select **Quest 3 only**, the hardware
used for current testing, instead of inheriting device additions from SDK defaults.
No package-cache code is patched. Broader headset targeting requires device QA.

The release packager checks the **final APK's merged manifest**, after both SDK
writers, for exactly one supported-device entry with value `quest3`. The same
PowerShell check accepts the valid case and refuses eight missing, duplicate,
unsupported or broader declarations in release CI. It also rejects the actual
manifest from 848FFA51. The replacement passed the full build and content audit, then was signed and
verified with the existing key as recorded below. Automatic approval review
required explicit approval of the replacement bytes before external upload; the
owner supplied that approval and the subsequent upload succeeded.

The uploader also warned about Quest 1 and mixed 32/64-bit libraries. The actual
rejected APK declares neither Quest 1 nor any 32-bit native directory: its 20
native libraries are all under `lib/arm64-v8a`. Those warnings do not override the
package inventory. Their cause has not been established.

Evidence: `.quest-evidence/meta-private-alpha-20261006/` contains the guarded
attempt, redacted uploader error blocks and before/after channel reads. The
replacement preparation is under `.quest-evidence/quest-device-targets-20261006/`.
No backend activation, policy certification, channel publication or headset
installation is part of this correction.

## Corrected signed Quest 3 candidate — 2026-10-06

Source `d7d5d847` produces
`MaestroQuest-intermediate-20261006-051907.apk`,
SHA-256 `8F460E1B8CF7AD80970052C887ADF4C8B6180E3143C07C577A5232B455F3FC4F` (174,552,012 bytes).
The signed replacement is `MaestroQuest-release-1-8F460E1B.apk`,
SHA-256 `DA6896832902832176A66406EDF328E3B01D4FF2E6DA295760AECB470163DC9B` (174,671,215 bytes).
It is non-debuggable `com.maestro.quest`, version 1.0.0/code 1, with exactly
`quest3` as its supported device. The existing backed-up certificate verifies.

The complete helper exited successfully: **837 EditMode / 647 PlayMode** checks
(three optional private-file skips), **82 Android tests** (two optional skips),
**470 native-room / 87 original-book observations**, using offline scripted
providers. Observation counts include runtime sampling and differ from preceding
runs; both complete journey contracts passed. All **3,150 frozen inputs**, native
sources/metas, AAR, **148 web files**, avatar/178 motions/26 templates/seven modules
match their audited inputs. All **25 release CI steps** passed on this source,
including the new final-manifest policy regression cases.

Relative to the previously approved package inputs, all **3,144 unchanged inputs**
match, including chat/runtime source, assets and the public production profile.
Only device target configuration and packaging checks changed (four changed files,
two added check scripts). Signing preserved all **1,070 non-signature ZIP entries**
of the corrected intermediate. The v3 signature, explicit v2 verification, ARM64
inventory and 16 KiB alignment pass; all four original key files remain unchanged.

Automatic approval review initially stopped the replacement upload before process
creation because the earlier approval named **848FFA51**. The owner then explicitly
approved **DA689683** for the same private Alpha draft. The official CLI completed
with exit 0 at **05:30:56 UTC**, after three recovered network retries, and created
**build `1767008267909255`**. APK validation reported **zero error types/messages**.
The underlying uploader parameters confirm `draft: true`; `--publish` was absent.

The upload receipt says server compatibility tests are running. Their final result
is **unverified**: the CLI has no draft-test-results command, and browser automation
still fails before reaching the dashboard. The owner has been asked to read
[the build test results](https://developers.meta.com/vr/manage/applications/1763835394893209/builds/1767008267909255/test-results/).
All four channels still show empty current-build lists, consistent with an
unpublished draft; this does not mean the uploaded build is missing. No channel
publication, tester invitations, policy certification, backend activation or
headset installation occurred. The APK hash is unchanged after upload.

The successful uploader retained the two warnings above despite the verified
Quest-3-only manifest and ARM64-only native inventory. These warnings are recorded
rather than treated as proof of unsupported binaries. Their cause and server-side
compatibility outcome are not established.

Evidence: `.quest-evidence/quest-device-targets-20261006/` contains frozen inputs,
input delta, complete package audit, tests/journeys, signing receipt and public
signature/manifest/alignment outputs. The completed guarded replacement upload plan and public receipt are in
`.quest-evidence/meta-private-alpha-20261006/retry-quest3/`. The installed D522191F
development app was not touched by this packaging correction. Store entitlement,
account/provider access and release readiness remain unverified.

## Verified real-settings intermediate — 2026-10-06

`MaestroQuest-intermediate-20261006-041018.apk` was prepared from `855e96f6` with
3,148 frozen source/tooling/profile/content inputs and the verified public release
configuration. SHA-256:
`BB9C6A89321FBDF13F8FE68E98630EDD02E434DE77940F7DEDADE11E9043C776`;
174,552,032 bytes. It is non-debuggable `com.maestro.quest`, version 1.0.0/code 1,
ARM64-only and 16 KiB aligned. Its **development certificate** is verified; it is
not signed with the backed-up release key and must not be uploaded as a release.

The full helper exited successfully after **837 EditMode / 647 PlayMode** tests
(three optional private-model skips), **82 Android tests** (two optional skips),
475 native-room and 91 original-book observations using scripted offline providers,
and the public-profile web build. All 148 packaged web files and the profile
receipt match. The included avatar, 178 motions, 26 templates and seven modules
match their source bytes. The packaged Meta configuration is enabled with the
owner's app ID; the source/mirror setting was restored afterwards. Development
input/render tools, the XR Operator native layer and storage-fault injection are
absent from the package.

A temporary installation under the separate release package identity on Quest 3
was non-debuggable, emitted the native Store-access refusal message, and exited.
This verifies rejection of that unverified installation, not the precise Meta
failure reason, positive Store entitlement, real account access or release-mode
performance. The test app was removed. Installed development APK D522191F and all
200 external saved files match their fresh pre-test hashes; it remains stopped.
The initial file comparison was corrected for Windows newline conversion in the
local hash listing; no device data was restored or changed to make it pass.

Evidence: `.quest-evidence/release-profile-build-20261006/verified.json`, package
and source inventories, native reports/journeys, signature/alignment/manifest,
launch record and cleanup receipt. The owner subsequently approved local signing
of this exact intermediate with the existing backed-up key; the signed result is
recorded below. No Store upload, backend enablement or paid provider call occurred.

## Signed local candidate — 2026-10-06

After explicit owner approval, the unchanged BB9C6A89 intermediate was signed
once into `MaestroQuest-release-1-BB9C6A89.apk`. SHA-256:
`848FFA51C1E03B36A5B9DC63E397A4DC0D7122C67B523937EB3241271A7908EF`;
174,671,215 bytes. Its verified certificate is
`EDD758283DB1A2A2D033119E25C0312332421B154250AC6C7F38003B84AA92A9`.

Default verification for minimum API 32 passes with APK Signature Scheme v3;
an explicit API 24–27 verification also passes v2. The one-off checking helper
initially required v2 in the default output and stopped after signing. Public
verification identified this checking error; the existing output was verified
without signing it again or modifying it. The tracked packaging helper checks
the certificate without that incorrect v2-only condition.

All **1,070 non-signature APK entries** match the audited intermediate byte
for byte, including executable code, web content and bundled models/motions.
Only the JAR signature metadata is excluded from that comparison. The manifest
is identical: non-debuggable `com.maestro.quest`, version 1.0.0/code 1, minimum
API 32/target 34. ARM64-only libraries and 16 KiB alignment pass. All four original
key/certificate/protected-password/receipt files match their saved fingerprints.
Signing passwords stayed inside the signing process and were cleared afterwards.

Public evidence: `.quest-evidence/release-profile-build-20261006/local-signing-result.json`,
`release-entry-inventory.json`, signature results, alignment and manifest. The
APK also has adjacent `.release.json`, `.sha256`, `.signature.txt` and
`.manifest.xml` files in the local build directory. This signed APK has not been
installed. Its later approved draft upload was rejected as recorded below; the
earlier temporary headset check used the intermediate.
Real Store entitlement/integrity, account/provider access and device acceptance
remain separate release gates.

## Public profile

Copy `unity/Release/quest-release.example.json` to an ignored
`unity/Release/quest-release.local.json` and fill only public configuration.
The example deliberately fails validation until the certificate and web values
are supplied. Do not add Meta secrets, provider keys, signing passwords or tokens.
The owner selected `com.maestro.quest` subject to Meta availability; the public
Meta app ID is `1763835394893209`. A syntactically valid profile cannot establish
package availability, Firebase app ownership or working provider credentials.

`versionCode` must increase for successive Store uploads. `signingCertificateSha256`
is the fingerprint of the intended release signer, without colons. It must also
agree with the backend's admitted certificates. Native and server Meta app IDs,
release package/minimum version and Firebase registration IDs must be configured
consistently; see [managed access](QUEST_MANAGED_ACCESS.md). The Quest registration
and ordinary web registration remain distinct in the original Firebase project.

The `web` section configures only the web bundle packaged inside Quest. Its
verification URL must be the exact native allowlisted public URL. The original
website must separately deploy the approval page with its own matching web
configuration; this command neither deploys nor changes the original website.
The profile does not create a second backend, identity or Gemini client.

```powershell
node scripts/quest-release.mjs check unity/Release/quest-release.local.json
```

Validation rejects development identities, invalid versions, unknown fields,
private/debug options, mixed Firebase project numbers, reused app registrations,
unsafe endpoints and an unapproved browser URL. The build blanks inherited
`VITE_*` settings (including dotenv values) before setting its public profile.
App Check debug tokens and external checkout flags cannot carry into this build.
The resulting web receipt hashes the profile and every built web file; Unity
checks that exact inventory before packaging, including the approval page.

## Prepare without using a release key

Use the same pinned toolchain and owned build mirror as development. The mirror
must remain separate from the checkout. This example prepares but does not sign
with a release key, install, upload or contact any provider:

```powershell
./unity/Tools/Build-QuestRelease.ps1 `
  -Editor 'D:/Tools/Unity/6000.3.24f1/Editor/Unity.exe' `
  -BuildMirror 'D:/Projects/Builds/MaestroQuestVerify' `
  -AndroidSdk 'C:/Users/ronit/AppData/Local/Android/Sdk' `
  -AndroidJdk 'C:/Program Files/Eclipse Adoptium/jdk-17.0.16.8-hotspot' `
  -ReleaseProfile ./unity/Release/quest-release.local.json `
  -PrepareOnly
```

The output is named `MaestroQuest-intermediate-<timestamp>.apk`. It is
non-debuggable, has no development entitlement bypass, and still has a development
certificate. It is **not a signed release candidate**. Store-recognized attestation
will not treat it as the intended release. Do not upload it as a release.
The synthetic fixture under `Tests/Fixtures` is only for offline tool validation;
its Firebase/endpoint/certificate settings are not real release configuration.

The shared packaging helper runs Unity checks, web compilation, Android tests,
lint and Unity packaging. It checks the final manifest and signature, records
hashes and requires the editor to exit successfully; an APK left after failure
is not a successful result. Development batches explicitly request normal editor
exit only after saving a successful build report. The source Meta configuration remains disabled;
release configuration is applied to the mirror for packaging and restored afterwards.

## Authorized release signing

Only after the owner has chosen and backed up the release key and authorized its
use, set these locally in the packaging process:

- `MAESTRO_QUEST_KEYSTORE`: absolute path to the intended keystore.
- `MAESTRO_QUEST_KEY_ALIAS`: its private-key alias, not `androiddebugkey`.
- `MAESTRO_QUEST_STORE_PASSWORD` and `MAESTRO_QUEST_KEY_PASSWORD`: local secrets.

Keep them out of chat, profiles, source control and command-line arguments.
Then invoke the same release command **without** `-PrepareOnly`. Missing signing
inputs stop before Unity; a debug key or certificate mismatch is refused.
Unity does not receive these inputs. The final Android signing step reads
passwords via environment variable names, signs a separate output, verifies its
signature/certificate and checks package, version and non-debuggable manifest.
The result is named `MaestroQuest-release-<versionCode>-<timestamp>.apk` with a
`.release.json` receipt. Neither command performs a Store upload or installation.

The current helper supports a single release signing key. Key rotation and
signing lineage need a separately reviewed migration; changing the fingerprint
alone is not an update-compatibility plan. Android documents environment-based
password input and signing-lineage handling in [apksigner](https://developer.android.com/tools/apksigner).
Meta describes APK/manifest requirements in [application manifests](https://developers.meta.com/vr/resources/publish-mobile-manifest/).

## Acceptance still required

A local successful package is not proof of Store package availability, accepted
signing identity, entitlement/offline behavior, Meta attestation, Firebase minting,
original-browser approval, managed/BYOK parity, device behavior, frame rate,
comfort, audience/privacy/payment configuration or Store compliance. Those remain
separate gates in [the delivery record](QUEST_V1_PLAN.md). The signed local
candidate above closes the local packaging/signing check; no build has been submitted.


Packaged development and release builds pass `BuildOptions.CleanBuildCache` to
Unity. Incremental Android APK generation retained large unused archive gaps in
a tested development artifact (230.5 MiB on disk for 176.7 MiB of live entries).
A clean build avoids carrying those stale packaging blocks into the distributable;
Unity can still reuse imported assets and cached shaders. This trades some build
time for predictable artifacts. Do not rewrite a signed APK with a generic ZIP
utility: that would invalidate its signature/alignment. Check the fresh package's
manifest, live file hashes, size and signature through the normal pipeline.
[Unity clean builds](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildOptions.CleanBuildCache.html).

## Release audit, 2026-10-04

This separates implementation from evidence needed to ship. It is not a new
feature backlog or permission to use credentials, deploy or submit.

| Gate | Current implementation/evidence | What closes it |
| --- | --- | --- |
| Reproducible package | Verified real public profile, backed-up release key, full release-mode checks and the owner-approved signed candidate 848FFA51. | Local packaging/signing is verified; Store recognition and runtime acceptance remain separate. |
| Account and managed AI | Shared book, browser pairing and Meta-to-App-Check endpoints; offline/native tests. Endpoints deployed but disabled; runtime non-token signing verified. Quest changes to the original API and the approval page remain undeployed. | Matching Firebase registrations and certificate, actual ingress config, authorized deployment, real Store-channel entitlement/integrity and account round trip. |
| Purchase model | Quest checkout hidden and refused by client/server; existing prepaid credit balances remain shared. BYOK remains available. No Meta commerce integration. | Resolve eligibility for the actual prepaid-credit/BYOK and interactive model using the [prepared Meta review request](QUEST_MANAGED_ACCESS.md#purchase-model-decision-and-prepared-meta-request-2026-10-05), then implement and verify any required payment changes. |
| Familiar book and provider parity | Original components, desktop chat-to-native journeys with scripted responses, and on-device book texture/page input/field focus checks. | Human virtual-key selection, microphone, Live, artifacts, interruption/recovery and managed/BYOK tests with real providers. |
| Physical play and resource limits | Automated native suites and current Quest controller/hand grip, Recall, chalk/ink, measured packing and submerged-vessel checks; earlier user-confirmed basics. | Remaining interactions/room-scan alignment, actual full-disk/prolonged save stress and sustained performance/comfort on supported hardware. Four partial-write terminations, six injected ENOSPC recoveries and 256 consecutive saves now pass in the [separate Android diagnostic](QUEST_DEVICE_QA.md#android-partial-write-and-enospc-probe--2026-10-06); this does not establish actual disk exhaustion or prolonged use. Transaction-boundary recovery has both the [desktop matrix](QUEST_DEVICE_QA.md#reproducible-process-termination-storage-probe) and a [13-case / 15-kill Android diagnostic](QUEST_DEVICE_QA.md#android-process-termination-storage-probe--2026-10-05). Completed-save recovery now has [three device restarts](QUEST_DEVICE_QA.md#saved-room-and-action-history-across-restart--2026-10-05). The [controller bucket-to-cup check](QUEST_DEVICE_QA.md#controller-bucket-to-cup-pour--2026-10-06) verifies a 200 mL physical pour and paired liquid Undo/Redo on D522191F, with a fixed receiver; hand, overflow and broader flow acceptance remain open. The latest animated 32-brick development run completed five minutes, averaging 71.43 FPS at 72 Hz; steady 72 FPS remains unmet. See [the current device measurement and its limits](QUEST_DEVICE_QA.md#animated-book-icons--2026-10-06). See the [chat layout fix and shortened candidate measurement](QUEST_DEVICE_QA.md#quest-chat-layout-feedback-fix--2026-10-06) and earlier ten-minute comparison. |
| Audience and data | Owner chose 18+; book self-confirmation, prepared privacy/data map and local/cloud deletion paths. | Truthful Store audience/IARC setup, provider eligibility, published privacy/support/deletion details and actual deletion/consent QA. |
| Content and listing | Included avatar/motion/template inventories and provenance checks. | Final content acceptance, real-app screenshots/store metadata, dashboard review and authorized submission. |

Manual editor work can be evaluated without activating paid AI. Desktop simulated
identity/provider results do not satisfy production access tests, and an unsigned
or development-signed intermediate cannot satisfy Store-channel integrity. Device
work resumed on 2026-10-05 after the owner reconnected Quest 3; see [current device evidence](QUEST_DEVICE_QA.md).
The checkout client restriction remains included in the audited development APK.
The backend restriction remains undeployed.

### Development checkpoint, 2026-10-05

`MaestroQuest-scanned-ink-45468330.apk` adds shared scan-layout inspection and persistent
scanned ink layers. SHA-256: `454683300229BAA24AB9F8210308AC46B44270AD9DA0A905462966E91EAED9C8`.
The package audit matched 2,998 frozen source/asset inputs, 147 web files and all
included content, with ARM64-only libraries, a development manifest, v2 signature
and 16 KiB alignment. Verification: 831 EditMode / 634 PlayMode tests (three
optional private-model skips), 474 native-room and 77 original-book observations
with scripted offline providers, production web, Android lint and 76 Android tests
(two optional skips). This is **development-signed and was installed on Quest 3 on 2026-10-05**.
Book/native creation, animation, scan-layout and anchored-ink checks are recorded
in [device QA](QUEST_DEVICE_QA.md); these are partial acceptance. Physical
Quest, real-provider, signing/account/payment and Store acceptance gates above
remain open; the full v1 goal is active.

## Controller interaction checkpoint (2026-10-05)

`MaestroQuest-grip-fix-FD2C7B15.apk` supersedes the scanned-ink checkpoint for
current development testing. It fixes page/button collider ownership and adds
read-only development input diagnostics. Exact package identity, full checks,
actual controller/page acceptance and the separate liquid follow-up are recorded
in [device QA](QUEST_DEVICE_QA.md#gripping-through-book-pages-and-tray-buttons--2026-10-05).
No release signing, provider acceptance, deployment or Store upload is implied.

## Submerged vessel checkpoint (2026-10-05)

`MaestroQuest-submerged-vessel-0EC7EC29.apk` was installed as a development
checkpoint. It includes the grip fix and prevents a full, tilted submerged vessel
from repeatedly spilling/refilling. Full checks passed (831 EditMode / 637
PlayMode, native/book journeys, web and Android), and the headset trial verified
stable 2,000 mL transfer through actual controller input. Exact package identity,
cleanup and remaining acceptance limits are in
[device QA](QUEST_DEVICE_QA.md#submerged-vessel-stability--2026-10-05).

The same installed APK also passed automated hand-input, palm Recall, chalkboard
ink/Undo/Redo and prop-obstruction checks. Book text focus requested Meta's IME
and Android key events reached the field. These do not establish human hand
comfort or virtual-key placement/selection. Exact evidence and cleanup are in
[hand-input QA](QUEST_DEVICE_QA.md#hand-input-ink-and-text-field-checks--2026-10-05).


## Fingertip contact checkpoint (2026-10-05)

Development APK at this checkpoint: `MaestroQuest-hand-contact-F4A5B4B4.apk`,
SHA-256 `F4A5B4B478D6B7FF7EDFCCE62BCF0FD405BB7759E275011960A124757A652473`.
It fixes distant page/button hover canceling physical fingertip packing/sculpting.
831 EditMode / 639 PlayMode tests, both integration journeys and the complete
web/Android/source/content/signature/alignment audit passed. Quest input checks
confirmed contact, one measured ball on lift, and exact physical Undo/Redo.
A separate post-Recall placement error remains open in this checkpoint; see
[the device result](QUEST_DEVICE_QA.md#post-fix-package-and-device-result).
This is development-signed, not Store/provider/performance acceptance.


## Post-Recall packing checkpoint (2026-10-05)

Development APK at this checkpoint: `MaestroQuest-recalled-packing-11B4972E.apk` (188,154,895 bytes),
SHA-256 `11B4972E0DA36A1DDA7ACEBFC3D29F0831CEC63B4D32BEFD09BD3A1BE8A56B1B`.
Physical packing now converts between world contact/clearance and saved room-local
placement. It uses the existing shared operation and storage format. 831 EditMode /
641 PlayMode tests, 189 targeted catalog/book tests, both integration journeys and
the complete web/Android/source/content/signature/alignment audit passed. On Quest,
the packed sphere exactly matched its preview after Recall; material conservation
and physical Undo/Redo passed. Installed-package bytes matched the audit.
See [device evidence](QUEST_DEVICE_QA.md#recalled-material-packing-placement--2026-10-05).
The development package contains both hand-hover and Recall fixes. Production
services, real-provider acceptance, human comfort/performance and Store gates
remain open.


Development checkpoint `5C50CD1F` removes whole-room copies from periodic object
observations and one Android JSON serialization pass. It passed 833 EditMode /
641 PlayMode checks, 129 focused web tests, both native-room/original-book journeys,
Android tests/lint and the exact-input/content/signature/alignment audit. It was
installed in place and measured on Quest; the 32-brick workload improved modestly
but still missed 72 Hz. See [workload and limits](QUEST_DEVICE_QA.md#construction-workload-and-room-observation-cost--2026-10-05).
This remains development evidence, not sustained-performance or Store acceptance.


## Shared recipe materials checkpoint (2026-10-05)

Development APK at this checkpoint: `MaestroQuest-recipe-materials-76F97F56.apk`
(188,167,871 bytes), SHA-256
`76F97F56A3EB76AC20A284AD3F0E2EE77DE8D406770D4910B9498967739C8124`.
Recipes share immutable materials through bounded ownership; repaint, rebuild
and deletion preserve other objects. 833 EditMode / 644 PlayMode tests, 129 web
tests, both native-room/book journeys, Android checks and the full package audit
passed. Installation bytes matched. The construction comparison measured
69.56 / 70.51 FPS with physics paused/running, with lower GPU time than the
previous checkpoint; steady 72 FPS remains unmet. Full evidence, thermal/profile
limits, the workshop-transition check and cleanup are in
[device QA](QUEST_DEVICE_QA.md#shared-recipe-materials--2026-10-05).
Release/provider/comfort/Store gates remain open.


## Receipt publication checkpoint (2026-10-05)

Current installed development APK: `MaestroQuest-receipt-publication-6A7C780E.apk`
(188,184,431 bytes), SHA-256
`6A7C780E423DABD82244BB7EA4E350DA08C7EE1A45070FF09EB636A4E927FC0C`.
Periodic receipt summaries omit large call arguments before copying; selected
receipts retain their exact calls. Structured observations serialize without a
second JSON-tree clone. Four new native regressions verify allocation scaling,
detachment, completion/restart and wire ownership/values.

837 EditMode / 644 PlayMode tests, 129 web tests, 474 native-room / 69 book
observations, Android lint / 76 tests and the complete 3,017-input package audit
passed. The catalog remains 93 actions / 102 facts / 16 events with 302 checked
sources. Installation bytes matched. The same temporary construction measured
71.18 / 70.89 FPS paused/running; native frame-interval p95 was 23.13 / 22.33 ms.
The room and device settings were restored and the app stopped for charging.
These short warm development measurements do not establish sustained 72 Hz or
release acceptance. Details are in [device QA](QUEST_DEVICE_QA.md#receipt-publication-cost--2026-10-05).


## Device persistence evidence (2026-10-05)

The unchanged installed `6A7C780E` package passed three real Quest process
restarts. Exact saved ink and object source survived; unsaved temporary edits
did not. Interrupted animations remained stopped, matching scan anchors resolved
saved ink, and creation/deletion receipts retained exact calls and results within
the 16-terminal-receipt history window. Cleanup restored `room.v20.json` byte for
byte. This is completed-save evidence, not mid-write power-loss or low-disk stress.
Two early workshop-open requests were interrupted by native startup rebinding;
startup opening remains a usability follow-up. See [device QA](QUEST_DEVICE_QA.md#saved-room-and-action-history-across-restart--2026-10-05).

Earlier construction performance windows included the robot geometry but only
requested its two-second saved clip. They do not demonstrate continuously animated
workload performance; see the [benchmark correction](QUEST_DEVICE_QA.md#workload-animation-correction--2026-10-05).


## Workshop startup checkpoint (2026-10-05)

`MaestroQuest-workshop-startup-3FB9A8AF.apk` is the earlier startup checkpoint.
It retains a workshop-open request through initial native room binding, while
navigation cancellation and mutation session guards remain intact. Three single-open
cold launches and a fourth cancellation/reopen check passed on Quest 3. Full
native, book/web, Android and package audits passed; the original saved room
remained byte-identical. Exact package identity, test counts and evidence limits
are in [device QA](QUEST_DEVICE_QA.md#workshop-opening-during-native-startup--2026-10-05).
The earlier performance measurements were not repeated for this navigation change.
All production access, provider, signing and Store gates above remain open.

## Objects navigation checkpoint (2026-10-05)

`MaestroQuest-objects-navigation-CB3309AB.apk` is the previous development
checkpoint. It fixes returning from Behaviours when the selected object is empty
or gone, and corrects the catalog's room-session identity description. Full Unity,
shared book/room, web, Android and package audits passed. Actual Quest navigation
passed after empty selection, valid Maestro selection and temporary-object discard;
the original saved room remained byte-identical. See [device QA](QUEST_DEVICE_QA.md#objects-navigation-after-selection-disappears--2026-10-05)
for package identity and evidence limits. The ten-minute animated workload on
previous 3FB9A8AF averaged 70.96 FPS at 72 Hz; sustained performance remains open.
No performance gain is claimed for this navigation-only update.


## Book frame-copy checkpoint (2026-10-05)

`MaestroQuest-frame-copy-496EC9BB.apk` is an earlier installed development
checkpoint. The hardware-buffer renderer retains valid book content between
browser frames and avoids duplicate GPU copies; capture rate, resolution and
visual effects are unchanged. Six renderer regressions and full Unity,
integration, Android and package audits passed. Actual stereo screenshots showed
chat and workshop updating on the 3D pages, and native counters confirmed the gate.

The sleep/wake check reached Quest's **Finding position in room** warning.
Same-process browser controls recovered, but full 3D resume and the planned
same-APK performance comparison await normal headset tracking. The warning was
not bypassed for a benchmark. No frame-rate improvement or sustained 72 Hz pass
is claimed. Temporary objects were discarded and both saved room and behaviour
files remained byte-identical; the app is stopped for charging. Exact package
identity, counts and limits are in [device QA](QUEST_DEVICE_QA.md#book-hardware-buffer-copies--2026-10-05).
The production/provider, physical comfort, signing and Store gates remain open.


## Desktop process-termination storage evidence (2026-10-05)

The new opt-in `Test-QuestStorageCrash.ps1` passed 13 cases with 15 forced Unity
process terminations. Fresh processes recovered exact paired room/memory state,
kept the expected backups and completed a second idempotent startup. Cases cover
replacement saves, first saves and interruption during recovery. An independent
file audit verified the expected numeric values and all 1,165 source identities.
The ordinary Unity and shared book/room checks also passed.

This adds Editor-only verification tooling and changes no production storage or
APK. It is Windows transaction-boundary evidence, not Android/full-disk/physical
power-loss acceptance. The device save-refusal attempt did not reach the app while
the Guardian tracking prompt remained present; no test edit or obstruction was
applied. See [procedure, evidence and limits](QUEST_DEVICE_QA.md#reproducible-process-termination-storage-probe).
The installed frame-copy checkpoint and remaining release gates are unchanged.


## Android storage diagnostic evidence (2026-10-05)

A separate ARM64 development diagnostic passed 13 transaction-boundary crash
cases with 15 SIGKILL terminations on Quest 3 under Unity IL2CPP. Production
runtime sources matched all 709 recorded inputs. Exact recovered room/memory
bytes, numeric values, backups, retired journals and repeat Capture passed;
rollback and roll-forward also survived interruption during recovery itself.
The diagnostic had its own package/data and no network or VR requirement.
Its installed hash was verified, synthetic evidence archived, and the package
removed. The actual Maestro APK/room remained unchanged.

This closes Android transaction-boundary evidence for the paired storage kernel,
not mid-byte-write/full-disk/power-loss testing, sustained save stress, or the
normal book recovery UX. The installed **496EC9BB** development checkpoint and
other release gates are unchanged. [Procedure and retained evidence](QUEST_DEVICE_QA.md#android-process-termination-storage-probe--2026-10-05).


## In-app save-error acceptance (2026-10-05)

The tracking warning cleared, allowing the unchanged **496EC9BB** app to relaunch.
Through the normal book catalog, a deliberately unavailable staging path caused
object creation to fail visibly without changing the saved room. Removing the
owned obstruction and explicitly retrying saved the exact returned object ID;
deleting that QA object restored the original room bytes. Test receipts/history
remain ordinary action history. No full-disk or paired Keep-failure claim is made.
The app is stopped with properties/forwards restored. [Evidence and limits](QUEST_DEVICE_QA.md#normal-book-save-refusal-and-retry--2026-10-05).


## Same-package rendering measurement (2026-10-05)

The unchanged **496EC9BB** app completed continuous/new-frame/continuous copy
windows and a ten-minute new-frame window in the same process/workload. New-frame
copying reduced texture copies by about 60% and mean app GPU time by roughly
0.5 ms. The long window averaged **70.90 FPS at 72 Hz**, so sustained performance
remains unmet. A separate browser profile showed substantial layout/style work;
it does not identify the exact cause yet. Fixed view, initial API setup screen
and development-build limits are recorded in [device QA](QUEST_DEVICE_QA.md#same-package-book-copy-comparison--2026-10-05).
Both animations and room physics remained active. Ordinary book cleanup restored
original room/behaviour bytes and avatar; the app is stopped for charging with
test properties and forwards restored. No new APK or production service changed.


## Quest chat layout checkpoint (2026-10-06)

The current installed development APK is `MaestroQuest-book-layout-FD9AEBDE.apk`
(**188,178,107 bytes**; SHA-256
`FD9AEBDE5B88A78142542F57AFEED998F485EAC4966D10D1FEE0584E2729FD69`).
A Quest-only stable scrollbar gutter removes repeated chat layout/style feedback
without disabling animations. Controlled Android screenshots were byte-identical;
scrolling, page/bookmark navigation and adult entry passed. Full Unity, shared
native/book integration, web, Android and exact package checks passed, followed
by in-place installation and installed-hash verification.

The animated 32-brick workload stopped at its battery limit after 300.31 seconds,
with mean **70.81 FPS at 72 Hz**. This shortened development run does not establish
sustained performance or an end-to-end frame-rate improvement. The original saved
room/behaviours remained byte-identical; the app is stopped for charging. Package
counts, measurements and limits are in [device QA](QUEST_DEVICE_QA.md#quest-chat-layout-feedback-fix--2026-10-06).
All production/provider, signing and Store gates above remain open.


## Optional tool-tray candidate (2026-10-06)

`MaestroQuest-optional-tools-C43EBC56.apk` (**188,198,291 bytes**;
SHA-256 `C43EBC566BF86F98135EB2497379CCC84A877C6925488FC6ADD8756200DA2B17`)
was the audited development package at that checkpoint. The seven physical authoring trays
start hidden and use a shared catalog action to show/hide them. The permanent
3D Workshop control and book/palm recovery stay available; room activity and
saved data are independent of visibility.

Full native, original-book/room integration, web, Android and exact package
checks passed. The owner is charging the headset, so this candidate is not yet
installed or measured. **FD9AEBDE** remains installed. No sustained-performance,
real-provider or Store acceptance is added. [Counts and evidence limits](QUEST_DEVICE_QA.md#optional-physical-tool-trays--2026-10-06).
All production/provider, signing and Store gates above remain open.

## Dedicated release key prepared — 2026-10-06

The owner selected a new Quest-specific signing key. A local PKCS#12 keystore
with alias `maestro-quest-release` contains an RSA-3072 key and SHA-256 certificate
valid until 2076-10-05. Its public certificate SHA-256 fingerprint is:

`EDD758283DB1A2A2D033119E25C0312332421B154250AC6C7F38003B84AA92A9`

The keystore and password are outside the repository in the current Windows
user's local application data, under `Maestro/QuestReleaseSigning`. The directory
has a verified user-only ACL; the password is protected with Windows DPAPI.
Recovering the password and using the resulting private key to sign and verify
an in-memory challenge passed. No app was signed during this check.

This is local recoverability, not a portable backup: the DPAPI password file
depends on the current Windows account. At the owner's explicit direction, the
password-encrypted keystore was copied to
`E:/MaestroQuestSigningBackup/maestro-quest-release.p12`. Its byte hash, certificate
and a signature from the copied private key were verified. Drive encryption was
not verified; no password was copied to USB or written as plaintext.

The owner chose a handwritten password backup. A local manual helper is prepared
beside the protected key (`Write-PaperBackup.cmd`); it defaults to a non-disclosing
inspection path, refuses redirected/captured interactive execution, and requires
the owner to retype the password after clearing its display and confirm safe
paper storage. A successful run writes only a public verification receipt after
unlocking the exact expected private key and signing an in-memory challenge.
The helper's preflight and captured-session refusal passed. On 2026-10-06 at
03:09:28 UTC, the password retyped from paper unlocked the exact expected private
key and passed a signature challenge. The helper recorded safe paper storage,
and the owner also confirmed completion in chat. Its public receipt and the
current keystore/certificate fingerprints were verified. The four original key,
certificate, DPAPI password and public receipt files remain byte-identical.

Independent password recovery is now prepared through the owner-held paper and
the previously verified encrypted USB copy. The USB was absent during the paper
check, so it used the identical local keystore; this is not a fresh USB readback
or a restore performed on another computer. Physical paper storage is the owner's
attestation. No plaintext password file was created or APK signed during recovery.
The later authorized signing is recorded above. Do not regenerate or replace
this key. Actual production/Store verification remains open.

The ignored public release-profile draft contains the verified certificate,
shared production web settings and dedicated Quest Firebase registration.
The subsequent approved rollout supplied actual attestation and account-link URLs;
strict profile validation now passes while both endpoints remain disabled. See
[the scoped rollout and remaining activation gaps](QUEST_MANAGED_ACCESS.md#scoped-backend-rollout--2026-10-06).
Evidence includes the public paper receipt and configuration readbacks under
`.quest-evidence/account-release-config-20261006/`.

## Manual book controls checkpoint — 2026-10-06

`MaestroQuest-book-action-forms-A8D296E4.apk` was the audited development candidate at that checkpoint,
SHA-256 `A8D296E40395E889FE86497EF739E6C8D9DF6F696ECD971970E13C60F6E7F63D`. Full native, original-book, web, Android,
source/content/signature/alignment checks passed; exact counts and limits are in
[device QA](QUEST_DEVICE_QA.md#manual-book-action-forms--2026-10-06). The same audited
APK is installed; book/tool/controller checks and a ten-minute development
measurement are recorded there. It was not signed with the new release key. Quest performance, independent password
recovery, production configuration and Store gates remain open.

## Animated shared icons checkpoint — 2026-10-06

The current audited and installed development package is
`MaestroQuest-animated-icons-D522191F.apk`, SHA-256
`D522191F126BD8F1D6D09958F0637780367C30C9E57C8DEDA02669FD78A96ED3`.
The shared web UI keeps its animated icon shapes, colours and dimensions while
avoiding repeated page layouts. Phone/book viewport, native integration, packaged
content/signature and Quest checks passed;
[exact counts and measurement limits](QUEST_DEVICE_QA.md#animated-book-icons--2026-10-06)
include the 71.43 FPS five-minute development run. This is not sustained 72 FPS
acceptance or a release-signed build. Independent password recovery, production
configuration, human/provider testing and Store gates remain open.


## Account and signing recovery preparation — 2026-10-06

The owner completed paper-password recovery; the typed password and exact signing
key passed verification. The encrypted USB copy was verified earlier. The dedicated
Quest Firebase client registration is created, its alternate providers/debug tokens
are absent, and the original app registrations are unchanged. Verified public
settings are saved in the ignored release draft. Production bootstrap endpoints,
browser approval page, Meta/IAM/ingress configuration, authorized deployment and
provider/Store-channel acceptance remain open. No release build was signed or
uploaded. See [the account evidence](QUEST_MANAGED_ACCESS.md#verified-account-preparation--2026-10-06)
and the dedicated-key section above. Earlier checkpoint notes retain their scope.
