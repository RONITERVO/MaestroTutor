# Root application dependency review — 2026-10-02

Updated Transformers.js 4.2.0 to 4.3.0 (ONNX Node 1.30.0 / web
1.31.0-dev.20260914-8d85527a0), sharp to 0.35.5, adm-zip to 0.6.1 through
ONNX's supported dependency, and brace-expansion to compatible patched versions.
No forced Firebase or Capacitor downgrade/major override was used.

`sharp` is used in the headless attachment pipeline, so its image-processing
advisories are relevant; it is not merely an unused browser dependency. Adm-zip
is in the ONNX Node installer, not our runtime native ZIP/model importer. The
updated speech worker must pass real browser q4 WASM inference on generated audio,
in addition to mocked client tests, before packaging.

Primary advisories:
[sharp/libvips](https://github.com/lovell/sharp/security/advisories/GHSA-f88m-g3jw-g9cj),
[sharp/libheif](https://github.com/lovell/sharp/security/advisories/GHSA-rgj7-g3m4-5g8c),
[adm-zip](https://github.com/cthackers/adm-zip/security/advisories/GHSA-7q85-xj36-vmfc).

## Remaining root Firebase finding

Root `firebase@11.10.0` brings `@firebase/firestore@4.8.0` and
`@grpc/grpc-js@1.9.16` (`~1.9.0`). The npm audit reports six high package entries
through this one dependency path, not six independently used servers. The two
underlying advisories concern server-side certificate authentication and exposing
server handler errors:
[GHSA-m9gg-hp2v-232j](https://github.com/grpc/grpc-node/security/advisories/GHSA-m9gg-hp2v-232j),
[GHSA-f596-whhp-79r4](https://github.com/grpc/grpc-node/security/advisories/GHSA-f596-whhp-79r4).

The root source imports Firebase App/Auth/App Check only. It imports neither
Firestore nor gRPC and creates no gRPC server. The browser worker uses ONNX WASM,
not Node. Functions uses its own separately audited dependency tree; this
exception must never be applied to backend or Live gateway deployments.

`verify-app-audit.mjs` allows only those exact advisory URLs and known root parent
edges, rejects critical escalation/new findings/nested copies/registry failure,
and expires on **2026-11-01**. CI no longer accepts arbitrary high findings under
a blanket critical-only threshold. Remove the exception when a supported SDK
upgrade resolves it; review again sooner if any Firestore/Node server use is added.
Do not use `npm audit fix --force`: its current proposed Firebase 9 / Capacitor
Firebase 5 downgrade conflicts with the existing supported app integration.

The ordinary `npm audit --omit=dev` result is still nonzero. This is a documented,
temporary reachability assessment, not a claim that all installed dependencies
are vulnerability-free. Optional development-only `@capacitor/assets` still has
its own older sharp tree; it is not the headless runtime's selected sharp module.

## Capacitor proxy navigation patch — 2026-10-06

The release audit began reporting
[GHSA-rvm3-566m-v7fv](https://github.com/ionic-team/capacitor/security/advisories/GHSA-rvm3-566m-v7fv)
against the locked Capacitor native packages. The upstream issue permits
untrusted frame navigation through the internal HTTP proxy to acquire the app's
origin privileges. Disabling CapacitorHttp alone does not fix affected versions.

Root Android/core/CLI are now pinned to **8.5.1**. The secure-storage package's
nested Android/core/iOS copies move from 7.4.5 to **7.6.9**, within its existing
supported ranges. No major-version override or Firebase downgrade was introduced.
Only these six package versions changed. The upstream
[8.5.1](https://github.com/ionic-team/capacitor/releases/tag/8.5.1) and
[7.6.9](https://github.com/ionic-team/capacitor/releases/tag/7.6.9) releases identify
the proxy-navigation fixes.

The shipped-dependency gate passes with its original exact, expiring Firebase
exception; no new advisory exception was added. Verification passed: **2,396
app tests**, production TypeScript/web build, phone Android debug assembly and
its one unit test, plus the actual original-book/Unity integration journey with
scripted provider responses. Evidence is in
`.quest-evidence/android-byte-storage-20261006/` and book journey
`.quest-evidence/native-room/7b41b75f4c664673b1e5ddeac153d121/`.

The phone APK was built but not installed or released. Unity's book uses its
separate native WebView library, whose dependency graph contains no Capacitor
Android library. The installed Quest checkpoint remains D522191F; a later full
Quest package will include the updated shared JavaScript dependency. These
checks do not establish human phone-layout or real-provider acceptance.
## Functions proxy-address patch — 2026-10-06

The Functions lockfile now resolves `proxy-addr` **2.0.8**, fixing
[GHSA-jqcg-44mw-7w3h](https://github.com/jshttp/proxy-addr/security/advisories/GHSA-jqcg-44mw-7w3h).
The upstream defect lets certain incorrectly written IPv4-mapped IPv6 trust
subnets match every IPv4 caller, undermining forwarded-address throttling.
Only this transitive package changed; application proxy configuration and the
existing dependency audit policy are unchanged. This is a dependency repair,
not evidence of exploitation or a claim about deployed ingress configuration.

Verification passed: **96 Functions unit tests**, including existing CORS,
untrusted forwarded-header and nearest-untrusted-hop checks; the complete local
Firestore emulator command (billing, Live gateway, **57 managed Gemini cases**,
Quest attestation and account-link transaction checks); and five direct checks
of malformed-mapped-subnet rejection and correct IPv4/mapped-subnet matching.
These local checks used Node 25.4.0; release CI separately uses the declared
Node 22 runtime. Evidence is in
`.quest-evidence/android-byte-storage-20261006/functions-*-patched.*` and
`proxy-addr-boundary.json` in that directory.

The Functions shipped-dependency audit has no high/critical findings; six
moderate entries through the existing `uuid` dependency remain reported. The
Live gateway shipped audit has no findings. No audit exception was added, no
provider request or deployment was made, and no installed Quest package changed.


## Functions request-parser and SDK dependency patches — 2026-10-24

Release-gate run `40048263190` on `3ecab791` failed its shipped Functions audit
on two high-severity advisories published/reviewed since the preceding green run.
The Functions lockfile now resolves body-parser **1.20.9 / 2.4.0** and Zod
**4.6.7**, covering the upstream
[compressed-request parser failure](https://github.com/advisories/GHSA-27r3-jxmh-p828)
and [oversized-collection validation issue](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5).
No new audit exception or forced major-version override was introduced.

Updates stay within the existing manifest ranges. Firebase Admin moves from
14.3.0 to [14.5.0](https://github.com/firebase/firebase-admin-node/releases/tag/v14.5.0),
including its supported Firestore 9.4.0 and Storage 8.4.0 dependency trees.
Firebase Tools moves to 15.34.1. The manifest is unchanged. The final production
Functions audit reports **zero findings**. Its development tooling retains five
moderate findings through the Pub/Sub/OpenTelemetry and gaxios/uuid chains;
the reported automatic remedy requires downgrading Firebase Tools to a different
major version, which this update deliberately does not do. The Live gateway audit
remains clean. The root application's existing exact Firebase server-path exception
and 2026-11-01 expiry remain unchanged; this does not claim a universally clean
application dependency tree.

Verification used **Node 22.23.4**, matching the declared backend runtime:
**102 Functions unit tests**, the complete local Firestore emulator command
(billing, Live gateway, **57 managed Gemini cases**, Quest attestation and
account-link transactions), plus **31 Live gateway tests** and its TypeScript build.
The new HTTP regression tests exercise the actual API entry point using malformed
gzip/deflate input and verify subsequent CORS and valid compressed requests still
work. They use only loopback requests to an unknown route, with no authentication,
database, provider or billing action. Deflate truncation is tested because zlib
accepts a trailing deflate suffix; gzip covers the advisory's trailing-byte case.

Evidence, lockfile version deltas, the initial test correction, official Node archive
checksum and process exits are retained in
`.quest-evidence/quest-integration-20261024/`. This is local dependency verification;
no deployment, provider request, release-key use or device operation occurred.


Commit `bbd8343a` subsequently passed the full
[release gate](https://github.com/RONITERVO/MaestroTutor/actions/runs/40049803301)
and all four [native-echo workflow jobs](https://github.com/RONITERVO/MaestroTutor/actions/runs/40049803305).
This confirms the CI dependency, web, backend and harness gates; it does not
resolve the separately recorded native audio-quality failures or establish
headset/provider acceptance. Full current-source Unity and Android integration
verification was still running when this dependency checkpoint was recorded.


## Root audit exception removed — 2026-11-01

Release gate [41261435896](https://github.com/RONITERVO/MaestroTutor/actions/runs/41261435896)
on `6332efd7` stopped at the expired root Firebase exception, before its test,
lint, build and backend steps. A fresh production lock audit also found the
new glob/minimatch path through gaxios; renewing the old deadline would not have
resolved that finding. The separate native-echo workflow passed on that commit.

The root now selects gaxios **7.1.4** within all three existing parents' `^7.0.0`
ranges. Its [upstream patch](https://github.com/googleapis/google-cloud-node-core/releases/tag/gaxios-v7.1.4)
removes the unused production rimraf dependency. All shipped CJS/ESM runtime
code and declarations are byte-identical to 7.1.3. This removes the production
[glob cancellation](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4) and
[minimatch amplification](https://github.com/advisories/GHSA-g3hv-75wg-m249)
chain without adding gaxios as a direct app dependency.

One deliberately narrow override selects **@grpc/grpc-js 1.14.7** only below
**@firebase/firestore 4.8.0**. It crosses Firestore's declared `~1.9.0` range;
this is a locally reviewed compatibility override, not a claim of upstream
support for that combination. Current Firebase major upgrades still retain the
old gRPC range, so a major Firebase/Capacitor migration would not solve it.
The selected release is newer than the primary advisories' fixed versions for
[certificate authorization](https://github.com/grpc/grpc-node/security/advisories/GHSA-m9gg-hp2v-232j)
and [handler-error disclosure](https://github.com/grpc/grpc-node/security/advisories/GHSA-f596-whhp-79r4).
Revisit this exact-version override when the owning Firebase dependency updates;
remove it when the supported parent selects a patched release. Any future
Firestore use needs its own integration assessment.

The lock delta has two existing version changes, two new gRPC dependencies,
26 removed unused dependencies and thirteen retained packages now used only by
development tooling. No Firebase, Capacitor or GenAI SDK major changed, and no
package moved from development-only to production. Independent review verifies
the installed gRPC/gaxios files against their integrity-checked public tarballs.

The production root audit now reports **zero findings**. `verify-app-audit.mjs`
requires a complete clean npm v2 report with consistent severity counters;
registry errors, partial reports and every severity of finding fail. There are
no advisory URL or time-based exceptions. The gate's 55 regression cases pass.
Development-only dependencies and backend trees are separate; this does not
claim that every installed dependency is free of advisories.

Verification used Node **22.23.4**: **3,908 app tests across 392 files**, app
lint and the production TypeScript/Vite build pass. The new three-case gRPC
compatibility test passes unchanged before and after the override: actual
Firestore CJS/ESM client construction/offline termination, and generated-client
unary/streaming/cancellation over bounded loopback connections. It resolves
Firestore's own proto-loader 0.7.15 separately from gRPC's new 0.8.1 child.
External fetch and non-loopback HTTP/2 are rejected. This is client compatibility
coverage, not an exhaustive upstream gRPC or remote database test.

All **152** rebuilt browser payloads are byte-identical to the saved baseline,
Unity mirror and prior Android export. The actual configured browser Firebase
and GenAI dependency closures are also unchanged and exclude Firestore/gRPC
and Google Auth/gaxios respectively. The root Node headless path does load
gaxios; its runtime byte identity and full app tests matter for that path.
Prior Unity/Android browser-payload evidence therefore still applies exactly.
There was no new Unity build, native journey, APK, provider call or device run.
The old full-source freeze is not described as unchanged after this lock edit.

Functions still reports five moderate production entries and Live gateway four,
with no high/critical findings. Existing release-gate backend thresholds pass,
but the strict gateway staging audit would fail. The gax5 filesystem dependency
is unused by its shipped code, yet no supported in-range parent repair currently
removes it. A backend repair needs separate dependency review and emulator/gateway
verification; no audit threshold was relaxed. Functions also has an independently
available compatible Hono patch. These remain explicit follow-up work.

Evidence: `.quest-evidence/dependency-review-20261101/`, including the before/after
audits, exact package baselines, public tarball comparisons, compatibility logs,
full app report, `independent-root-repair-review.json` and
`dependency-payload-review.json`. All sixteen unrelated protected files remain
unchanged. Device, provider, signing, deployment and Meta holds remain in force.


## Backend production dependency repair — 2026-11-01

The remaining Functions five and Live gateway four moderate production findings
are repaired. Both fresh production lock audits now report **zero findings**.
The release gate now rejects backend findings at every severity, matching the
existing strict gateway staging gate. No advisory exception or relaxed threshold
was added. Development tooling is outside this clean-production claim.

Functions selects **@hono/node-server 2.1.5** within its existing MCP parent range,
covering the [static-path decoding advisory](https://github.com/advisories/GHSA-rmxm-3fg6-px4f).
Both backends narrowly override **glob 14.0.3** only below
`google-gax@5.0.8 -> rimraf@5.0.10`; its resolved minimatch is **11.0.3**.
These versions cover the [glob cancellation](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4)
and [minimatch amplification](https://github.com/advisories/GHSA-g3hv-75wg-m249)
findings. This deliberately crosses rimraf's declared glob major: it is a tested
local compatibility repair, not a claim of upstream support. Current compatible
gax/Firestore parent releases do not remove the old chain. Remove the exact
version-scoped override when a supported parent selects a patched dependency;
review the compatibility tests at that time instead of silently skipping them.

The installed gax runtime does not import rimraf, but source reachability alone
was not used to accept vulnerable versions. A shared offline compatibility suite
resolves each backend's actual Firebase/Firestore/gax/rimraf branch and passes
**16 cases per backend unchanged before and after** on Node **22.23.4**. It
exercises real CommonJS and ESM exports, absolute glob results, literal and glob
sync/async deletion, preserved nonmatches, names with spaces and cancellation.
Its temporary files stay inside uniquely owned backend-local directories; a
missing or incorrectly resolved dependency branch fails instead of skipping.
These API checks do not claim exhaustive glob security regression coverage.

Verification also passes **118 Functions unit tests**, the complete five-part
Firestore emulator command (billing, Live gateway, **57 managed Gemini cases**,
Quest attestation and account linking), **31 gateway unit tests**, its added
16 compatibility cases, both TypeScript builds and app lint. No real provider
or database request, deployment, device operation or release-key use occurred.
Root browser dependencies and product code did not change in this backend slice.

Independent lock review found no SDK version change, unrelated version change
or local file dependency. **379 files across 13 installed package paths** match
seven official package tarballs after both lockfile and registry integrity
verification. Functions retains the old glob closure only for development tools;
gateway removes 32 obsolete descendants. Official glob/minimatch license metadata
changes from ISC to BlueOak-1.0.0 and remains recorded with the package evidence.

Evidence: `.quest-evidence/backend-repair-20261101/` and the before-update
compatibility runs in `.quest-evidence/backend-dependency-repair-20261101/`.
The first gateway resolution unexpectedly added a local parent dependency; it
was rejected before installation, preserved as evidence and rerun from the
actual gateway directory. The final reviewed lock contains no such reference.
A local test-wrapper argument error was likewise corrected before the successful
full Functions run; failed receipts remain preserved. All sixteen unrelated
protected files are unchanged. CI for this checkpoint is recorded separately;
these backend results do not substitute for Unity or headset acceptance.
