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
| Familiar book and provider parity | Original components and real desktop chat-to-native journey with scripted provider responses. | Current Quest texture, keyboard, microphone, Live, artifacts, interruption/recovery and managed/BYOK tests with real providers. |
| Physical play and resource limits | Automated room/physics/material/creation tests plus earlier user-confirmed headset basics. | Current APK hands/controllers, room scans, representative busy rooms, save stress and sustained performance/comfort on supported hardware. |
| Audience and data | Owner chose 18+; book self-confirmation, prepared privacy/data map and local/cloud deletion paths. | Truthful Store audience/IARC setup, provider eligibility, published privacy/support/deletion details and actual deletion/consent QA. |
| Content and listing | Included avatar/motion/template inventories and provenance checks. | Final content acceptance, real-app screenshots/store metadata, dashboard review and authorized submission. |

Manual editor work can be evaluated without activating paid AI. Desktop simulated
identity/provider results do not satisfy production access tests, and an unsigned
or development-signed intermediate cannot satisfy Store-channel integrity. Device
work remains on hold until the owner's pending availability question is answered.
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
(two optional skips). This is **development-signed and not installed**. Physical
Quest, real-provider, signing/account/payment and Store acceptance gates above
remain open; the full v1 goal is active.
