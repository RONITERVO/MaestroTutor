# Quest release packaging

This is a packaging path, not Store acceptance. No real release profile, release
key, production deployment or Store upload has been created by this checkpoint.
The existing development command still produces `com.maestro.quest.development`.
The release command prepares a non-development IL2CPP/ARM64 APK from the same
source, web app, native browser, included avatar and motions.

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
separate gates in [the delivery record](QUEST_V1_PLAN.md). This checkpoint has not
used a real release key or submitted a build.


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
| Reproducible package | Audited ARM64 development APK; release profile/signing pipeline with offline refusal tests. | Owner's real public profile, backed-up release key, authorized signing and audited signed candidate. |
| Account and managed AI | Shared book, browser pairing and Meta-to-App-Check endpoints; offline/native tests. Endpoints disabled/undeployed. | Matching Firebase registrations and certificate, actual ingress config, authorized deployment, real Store-channel entitlement/integrity and account round trip. |
| Purchase model | Quest checkout hidden and refused by client/server; existing prepaid credit balances remain shared. BYOK remains available. No Meta commerce integration. | Resolve eligibility for the actual prepaid-credit/BYOK and interactive model using the [prepared Meta review request](QUEST_MANAGED_ACCESS.md#purchase-model-decision-and-prepared-meta-request-2026-10-05), then implement and verify any required payment changes. |
| Familiar book and provider parity | Original components, desktop chat-to-native journeys with scripted responses, and on-device book texture/page input/field focus checks. | Human virtual-key selection, microphone, Live, artifacts, interruption/recovery and managed/BYOK tests with real providers. |
| Physical play and resource limits | Automated native suites and current Quest controller/hand grip, Recall, chalk/ink, measured packing and submerged-vessel checks; earlier user-confirmed basics. | Remaining interactions/room-scan alignment, save stress and sustained performance/comfort on supported hardware. The 32-brick development workload measured about 68 FPS at 72 Hz after the observation optimization; see the [workload evidence](QUEST_DEVICE_QA.md#construction-workload-and-room-observation-cost--2026-10-05). |
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

Current installed development APK: `MaestroQuest-recalled-packing-11B4972E.apk` (188,154,895 bytes),
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
