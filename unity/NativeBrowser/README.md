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
Android lint reports zero errors and four obsolete SDK-condition warnings in the
upstream transport. The development APK installs on Quest 3 and displays the
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
Native permission requests, file selection, IME and authentication need integration;
renderer recovery currently occurs on returning from an application interruption.
