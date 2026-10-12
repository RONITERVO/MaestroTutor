# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Extract the pinned candidate into a new isolated, ignored evaluation project."""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import shutil
import sys
import tarfile
import zipfile

EXPECTED = "9b7e3689561670e6b4b8e0c579a2d951bb4a0589f7d46c23bc9df923d62940f0"
archive = Path(sys.argv[1]).resolve()
project = Path(sys.argv[2]).resolve()
mode = sys.argv[3] if len(sys.argv) > 3 else "stock"
if mode not in {"stock", "owned", "android"}:
    raise ValueError("Unknown evaluation mode")
owned = mode != "stock"
android = mode == "android"
source = Path(__file__).resolve().parent
repo = source.parents[2]
evidence = repo / ".quest-evidence/audio-unity"
if not project.is_relative_to(evidence) or project.exists():
    raise ValueError("Use a new project within this checkout's audio-unity evidence directory")
with archive.open("rb") as stream:
    if hashlib.file_digest(stream, "sha256").hexdigest() != EXPECTED:
        raise ValueError("Expected the reviewed official Steam Audio Unity 4.8.1 archive")
with zipfile.ZipFile(archive) as bundle:
    packages = [entry for entry in bundle.infolist() if entry.filename.endswith("/SteamAudio.unitypackage")]
    if len(packages) != 1 or packages[0].file_size > 256 * 1024 * 1024:
        raise ValueError("Missing or unexpected Unity package")
    package = io.BytesIO(bundle.read(packages[0]))
project.mkdir(parents=True)
with tarfile.open(fileobj=package, mode="r:gz") as content:
    paths = {}
    for entry in content.getmembers():
        if not entry.name.endswith("/pathname"):
            continue
        if not entry.isfile() or entry.size > 4096:
            raise ValueError("Unexpected asset path member")
        name = content.extractfile(entry).read().decode("utf-8").strip()
        relative = PurePosixPath(name)
        if relative.is_absolute() or ".." in relative.parts or not name.startswith("Assets/Plugins/SteamAudio/"):
            raise ValueError("Unexpected vendor path")
        if owned and name not in {
            "Assets/Plugins/SteamAudio/Scripts/Runtime/SteamAudio.cs",
            "Assets/Plugins/SteamAudio/Binaries/Windows/x86_64/phonon.dll",
            *( ["Assets/Plugins/SteamAudio/Binaries/Android/arm64/libphonon.so"] if android else [] ),
        }:
            continue
        if "/Binaries/" in name and "/Windows/x86_64" not in name and not (android and name.endswith("/Android/arm64/libphonon.so")):
            continue
        if name.endswith(("GPUUtilities.dll", "TrueAudioNext.dll")):
            continue
        paths[entry.name.rsplit("/", 1)[0]] = relative
    for key, relative in paths.items():
        target = project / relative
        for member, suffix in [(key + "/asset", ""), (key + "/asset.meta", ".meta")]:
            try:
                entry = content.getmember(member)
            except KeyError:
                continue
            if not entry.isfile() or entry.size > 64 * 1024 * 1024:
                raise ValueError("Unexpected vendor asset")
            output = Path(str(target) + suffix).resolve()
            if not output.is_relative_to(project):
                raise ValueError("Asset path escaped the new project")
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_bytes(content.extractfile(entry).read())
settings = project / "ProjectSettings"
settings.mkdir()
(settings / "ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.24f1\n", encoding="utf-8")
# Copy the app's tested Unity audio settings into the isolated project only.
# No app settings are edited. This candidate project contains no Meta plugin.
audio = (repo / "unity/MaestroQuest/ProjectSettings/AudioManager.asset").read_text()
audio = audio.replace("m_SpatializerPlugin: Meta XR Audio", "m_SpatializerPlugin: Steam Audio Spatializer")
if "m_SpatializerPlugin: Steam Audio Spatializer" not in audio:
    raise ValueError("Review the new app audio configuration before evaluating the candidate")
if owned:
    audio = audio.replace("m_SpatializerPlugin: Steam Audio Spatializer", "m_SpatializerPlugin:")
(settings / "AudioManager.asset").write_text(audio, encoding="utf-8")
packages = project / "Packages"
packages.mkdir()
(packages / "manifest.json").write_text(json.dumps({"dependencies": {
    "com.unity.test-framework": "1.6.0", "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.physics": "1.0.0", "com.unity.modules.terrain": "1.0.0",
    "com.unity.modules.imgui": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0"
}}, indent=2), encoding="utf-8")
evaluation = project / "Assets/Evaluation"
evaluation.mkdir()
runtime = evaluation / "Runtime" if owned else evaluation
runtime.mkdir(exist_ok=True)
tests = evaluation / "Tests" if owned else evaluation
tests.mkdir(exist_ok=True)
if owned:
    runtime_files = ["NativeDsp.cs", "AcousticFrame.cs", "PreparedGraph.cs", "PreparedGraph.Motion.cs", "OwnedLifecycle.cs", "OwnedLifecycle.Motion.cs", "PcmVoice.cs", "PreparedGraph.Pcm.cs", "PreparedGraph.Tails.cs", "PcmControls.cs", "PcmTransformBinding.cs", "AcousticSceneData.cs", "PreparedGraph.Geometry.cs", "RuntimeAssembly.cs", "NativePlayerProbe.cs", "GeometryBridge.cs"]
    test_files = ["OwnedLifecycleTests.cs", "MovingAudioTests.cs", "PcmAudioTests.cs", "PcmTailTests.cs", "PcmAdmissionTests.cs", "PcmPresentationTests.cs", "GeometryAudioTests.cs", "NativePlayerTests.cs", "GeometryBridgeTests.cs", "PresentationConcurrencyTests.cs"]
    for name in runtime_files:
        shutil.copyfile(source / "Unity" / name, runtime / name)
    for name in test_files:
        shutil.copyfile(source / "Unity" / name, tests / name)
    (runtime / "Maestro.AudioCandidate.Runtime.asmdef").write_text(json.dumps({
        "name": "Maestro.AudioCandidate.Runtime", "references": ["Maestro.NativeBinding"]
    }, indent=2), encoding="utf-8")
else:
    shutil.copyfile(source / "Unity/CandidateTests.cs", tests / "CandidateTests.cs")
(tests / "Maestro.AudioCandidate.Tests.asmdef").write_text(json.dumps({
    "name": "Maestro.AudioCandidate.Tests", "references": ["Maestro.NativeBinding", "Maestro.AudioCandidate.Runtime"] if owned else ["SteamAudioUnity"],
    "optionalUnityReferences": ["TestAssemblies"]
}, indent=2), encoding="utf-8")
if owned:
    for name in ["SpeechPcmStream.cs", "AcousticGeometry.cs", "AcousticGeometryCatalog.cs"]:
        shutil.copyfile(repo / "unity/MaestroQuest/Assets/Maestro/Runtime/Book" / name, runtime / name)
    shutil.copyfile(repo / "unity/MaestroQuest/Assets/Maestro/Runtime/Creation/RoomPreparationBudget.cs", runtime / "RoomPreparationBudget.cs")
    binding = project / "Assets/Plugins/SteamAudio"
    (binding / "Maestro.NativeBinding.asmdef").write_text(json.dumps({"name": "Maestro.NativeBinding"}), encoding="utf-8")
    if not (binding / "Binaries/Windows/x86_64/phonon.dll").exists():
        raise ValueError("Missing pinned native library")
if android:
    editor = evaluation / "Editor"
    editor.mkdir()
    shutil.copyfile(source / "Unity/AndroidCandidateBuild.cs", editor / "AndroidCandidateBuild.cs")
    (editor / "Maestro.AudioCandidate.Editor.asmdef").write_text(json.dumps({
        "name": "Maestro.AudioCandidate.Editor", "references": [], "includePlatforms": ["Editor"]
    }, indent=2), encoding="utf-8")
    if not (binding / "Binaries/Android/arm64/libphonon.so").exists():
        raise ValueError("Missing pinned ARM64 library")
print("Prepared isolated Unity project: " + str(project))
