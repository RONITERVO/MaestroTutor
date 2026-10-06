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
