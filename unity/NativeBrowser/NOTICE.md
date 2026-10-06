# Third-party notices

The capture transport is derived from TLabWebViewPlugin (MIT), at the commit
recorded in README.md. Its upstream copyright and license are in LICENSE-TLab.md.
Maestro changes omit the JavaScript interface and Gecko implementation, remove
the Gecko-only selection adapter, recycle dispatched touch events, and correct
the case of the JniLog include for portable builds. Further capture changes copy
only newly received frames, reject callbacks from replaced surfaces, and schedule
surface recovery on the GL thread after an Android lifecycle resume.

`src/main/java/com/android/grafika/gles/{EglCore,GlUtil}.java` are from Google's
Grafika project, as vendored by that same upstream commit. Copyright 2013/2014
Google Inc., Apache License 2.0; their source headers are retained and the license
is provided in LICENSE-Apache-2.0.txt.

Unity native plugin headers retain the license in `src/main/cpp/Unity/LICENSE.md`.
Other transport source keeps its original copyright notices. The compiled Unity
classes.jar used for compile-only type resolution is not included in the AAR.
