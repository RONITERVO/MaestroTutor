# Maestro book browser

Android WebView is captured using the MIT TLab renderer. The generic TLab browser
and its JavaScript interface are **not** included. `BookWebView.java` owns the
app-specific browser: bundled HTTPS assets, no file access, no JS-native object,
SSL errors cancelled, opaque artifact frames, microphone limited to the app
origin after Android consent, and native-initiated bounded snapshot polling.

Upstream provenance:

- TLabWebView `3bd84ecb1796312ae871ed94f2501a63e0b25492`: C# capture classes only.
- TLabWebViewPlugin `7d76dd6bea04e556d1f567463b0fc2404640e6ff`: render transport,
  base classes and supporting types; excludes both Chromium `UnityConnect`
  and Gecko sources. Preserve `LICENSE-TLab.md` and nested third-party licenses.

Build with the repository Android Gradle wrapper, this directory as `-p`, and
`MAESTRO_UNITY_CLASSES_JAR` pointing at the pinned Unity editor Android SDK's
classes.jar. Compile-only Unity classes are not redistributed in the AAR.
The release AAR must be copied into `Assets/Plugins/Android` for an Android build,
with the matching AndroidX dependencies in Unity's generated Gradle project.
The packaged web bundle belongs in `Assets/StreamingAssets/maestro-web`.

Current status: ARM64 release AAR compilation succeeds against Unity 6000.3.24f1.
Android lint reports zero errors, four obsolete SDK-condition warnings in the
upstream transport and the deliberate JavaScript-enabled warning for the app
WebView. The earlier development APK installs on Quest 3 and displays the
bundled Maestro application on both book surfaces over passthrough. Android's
Activity must have hardware acceleration enabled, and the offscreen WebView must
use `setOffscreenPreRaster(true)`; without these it produced blank pages and tile
memory warnings on the first device run. Android WebView debugging is enabled
only when the containing application has Android's debuggable flag.
Suspend calls the bounded book lifecycle bridge, stopping capture and media
owners and unmounting runnable artifacts before WebView timers pause. Native
polls the shutdown acknowledgment, with a 1.5-second deadline: if the page fails
to settle, its WebView is destroyed and recreated on return. Saved IndexedDB is
retained; unfinished in-memory work may be lost in this fallback. A physical bell
explicitly resumes media after returning. Its command grants no Android permission
and does not bypass application consent or managed-access checks.

Quest 3 settings interruption and deliberately stalled acknowledgment recovery
have been exercised. Real active Live/microphone sessions still need hardware QA.
Microphone requests from the bundled app now ask Android for `RECORD_AUDIO` at
first use. Camera, opaque-frame, remote-origin and unknown-resource requests are
denied. Cancellation, navigation, disposal and app interruption invalidate the
pending browser request. A late Android grant never starts capture automatically.
If the permission dialog interrupts the app, the notice beside the book asks the
user to resume with the bell and try speaking again. Unity's startup permission
dialog is disabled so consent follows an actual microphone request. This first-use
flow still needs Quest hardware acceptance.

Top-document file inputs use Android's `ACTION_OPEN_DOCUMENT` picker. A one-use
recent input-click gate distinguishes the app's hidden file inputs from opaque
artifact frames; no JS-native object is exposed. MIME hints are used where Android
recognizes them, with an unrestricted picker for extensions such as NDJSON/VRM.
The input's normal application validation still applies. Only external content
providers can supply files; app-owned providers and `file:`, network and resource
URLs are rejected. Selected streams are copied on a worker with limits of 64 MiB
per file, 128 MiB per browser document and eight files per selection. Single-file
inputs remain single-file. A private, read-only provider exposes only these copies
to WebView; it exposes no directory or app-private storage. Copies are removed on
browser replacement and orphaned session caches are cleaned on the next use.

File callbacks wait for foreground return and are cancelled on browser replacement,
disposal, picker cancellation or a five-minute timeout. Read cancellation propagates
to the provider. Unsupported devices get a readable notice instead of a hanging
input. Folder selection, saving/downloads, capture-camera intents, IME and
authentication remain work. The actual Quest picker and selected-file WebView
reading have not yet been tested. This is file access infrastructure, not a finished
GLB/VRM model importer. Renderer recovery currently occurs on returning from an
application interruption.

Verification: `testReleaseUnitTest assembleRelease lintRelease` runs twelve
Robolectric tests plus the native build/lint. The development APK build invokes
these checks and verifies that the expected test suite ran. Web tests exercise
the gate and existing lifecycle behavior. With a local Vite server on 5178,
`node scripts/probe-quest-file-gate.mjs` (from the repository root) checks real
Chrome user activation, hidden file input clicks and iframe isolation. Set
`MAESTRO_TEST_URL` or `MAESTRO_CHROME` for a different local server/browser binary.

API references: [Android file chooser callbacks](https://developer.android.com/reference/android/webkit/WebChromeClient#onShowFileChooser(android.webkit.WebView,%20android.webkit.ValueCallback%3Candroid.net.Uri[]%3E,%20android.webkit.WebChromeClient.FileChooserParams)),
[Storage Access Framework](https://developer.android.com/training/data-storage/shared/documents-files),
[permission lifecycle](https://developer.android.com/reference/android/app/Fragment#requestPermissions(java.lang.String[],%20int)).
