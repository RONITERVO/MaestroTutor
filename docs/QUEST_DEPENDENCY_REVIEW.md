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
