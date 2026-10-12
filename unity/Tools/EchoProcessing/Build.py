# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Offline, hash-pinned source evaluation. Does not modify an installed app."""
import argparse
import hashlib
import importlib.metadata
import json
import os
import re
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import tarfile
import time
import zipfile

INPUTS = {
    "apm": "ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe",
    "abseil": "f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3",
    "overlay": "12dd8df1488a314c53e3751abd2750cf233b830651d168b6a9f15e7d0cf71f7b",
}
SOURCES = ("EchoProcessor.h", "EchoProcessor.cpp", "Probe.cpp", "meson.build", "Build.py", "Make-SpeechFixtures.ps1")

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def member_path(name, root):
    value = PurePosixPath(name)
    if value.is_absolute() or ".." in value.parts or "\\" in name or ":" in name:
        raise ValueError("Unsafe archive member")
    target = root.joinpath(*value.parts)
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError("Archive member escaped output")
    return target

def extract_tar(archive, root):
    with tarfile.open(archive) as source:
        members = source.getmembers()
        if len(members) > 10000 or sum(x.size for x in members) > 100 * 1024 * 1024:
            raise ValueError("Archive exceeds extraction budget")
        for entry in members:
            member_path(entry.name, root)
            if not (entry.isfile() or entry.isdir()):
                raise ValueError("Links and special archive members are unsupported")
        source.extractall(root, members=members, filter="data")

def extract_overlay(archive, root):
    with zipfile.ZipFile(archive) as source:
        entries = source.infolist()
        if len(entries) > 10 or sum(x.file_size for x in entries) > 1024 * 1024:
            raise ValueError("Unexpected Meson overlay")
        for entry in entries:
            target = member_path(entry.filename, root)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(source.read(entry))

def windows_env(env):
    program = Path(os.environ["ProgramFiles(x86)"])
    vswhere = program / "Microsoft Visual Studio/Installer/vswhere.exe"
    vs = subprocess.check_output([str(vswhere), "-latest", "-products", "*", "-requires",
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"],
        text=True, timeout=30, creationflags=subprocess.CREATE_NO_WINDOW).strip()
    def version(path):
        return tuple(int(x) for x in path.name.split("."))
    compiler = max((Path(vs) / "VC/Tools/MSVC").iterdir(), key=version)
    sdk = program / "Windows Kits/10"
    kit = max((x for x in (sdk / "Include").iterdir()
        if (sdk / "Lib" / x.name / "um/x64/kernel32.lib").is_file()), key=version).name
    env["INCLUDE"] = ";".join([str(compiler / "include")] + [str(sdk / "Include" / kit / x)
        for x in ("ucrt", "shared", "um", "winrt")])
    env["LIB"] = ";".join([str(compiler / "lib/x64")] + [str(sdk / "Lib" / kit / x)
        for x in ("ucrt/x64", "um/x64")])
    env["PATH"] = os.pathsep.join([str(compiler / "bin/Hostx64/x64"), str(sdk / "bin" / kit / "x64"), env["PATH"]])
    return {"compiler": str(compiler), "windowsSdk": kit}

def cross_file(ndk, output):
    ndk = ndk.resolve(strict=True)
    host = "windows-x86_64" if os.name == "nt" else "linux-x86_64"
    binary = ndk / "toolchains/llvm/prebuilt" / host / "bin"
    extension = ".exe" if os.name == "nt" else ""
    def tool(name):
        return (binary / (name + extension)).resolve(strict=True).as_posix()
    content = "[binaries]\n"
    content += f"c = ['{tool('clang')}', '--target=aarch64-linux-android32']\n"
    content += f"cpp = ['{tool('clang++')}', '--target=aarch64-linux-android32']\n"
    content += f"ar = '{tool('llvm-ar')}'\nstrip = '{tool('llvm-strip')}'\n"
    content += "[host_machine]\nsystem = 'android'\ncpu_family = 'aarch64'\ncpu = 'armv8'\nendian = 'little'\n"
    content += "[properties]\nneeds_exe_wrapper = true\n[built-in options]\n"
    content += "cpp_link_args = ['-static-libstdc++', '-Wl,--exclude-libs,ALL', '-Wl,-z,max-page-size=16384']\n"
    path = output / "android-cross.ini"
    path.write_text(content, encoding="utf-8")
    return path, tool("llvm-readelf"), tool("llvm-nm")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in INPUTS:
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--meson-root", type=Path, required=True, help="Local Meson 1.9.1 install directory")
    parser.add_argument("--ninja", type=Path, required=True)
    parser.add_argument("--platform", choices=("windows", "android"), required=True)
    parser.add_argument("--ndk", type=Path)
    parser.add_argument("--speech-wav", type=Path, action="append", default=[])
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    repo = here.parents[2]
    evidence = repo / ".quest-evidence/echo-native"
    output = args.output.resolve()
    if not output.is_relative_to(evidence) or output == evidence or output.exists():
        parser.error("Use a fresh child directory of this checkout's .quest-evidence/echo-native")
    archives = {}
    for name, expected in INPUTS.items():
        path = getattr(args, name).resolve(strict=True)
        if digest(path) != expected:
            parser.error("Archive hash mismatch: " + name)
        archives[name] = path
    meson_root = args.meson_root.resolve(strict=True)
    versions = [x.version for x in importlib.metadata.distributions(path=[str(meson_root)]) if x.metadata['Name'].lower() == 'meson']
    if versions != ["1.9.1"]:
        parser.error("Expected isolated Meson 1.9.1")
    ninja = args.ninja.resolve(strict=True)
    if args.platform == "android" and not args.ndk:
        parser.error("--ndk is required for Android")
    output.mkdir(parents=True)
    record = dict(version=1, status="running", platform=args.platform, started=time.time(),
        sourceHashes={name: digest(here / name) for name in SOURCES}, archives=INPUTS,
        meson="1.9.1", python=sys.version, ninjaSha256=digest(ninja),
        providerUsed=False, physicalAudio=False, unityIntegration=False,
        questAcceptance=False, speechQualityAccepted=False, commands=[])
    receipt = output / "result.json"
    def save():
        receipt.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    env = os.environ.copy()
    env["PYTHONPATH"] = str(meson_root)
    env["PATH"] = str(ninja.parent) + os.pathsep + env["PATH"]
    # No dependency downloads or unpinned system Abseil are allowed.
    def run(command, name, timeout=600, required=True):
        command = [str(x) for x in command]
        record["commands"].append({"argv": command, "log": name + ".log"})
        save()
        with (output / (name + ".log")).open("w", encoding="utf-8") as log:
            result = subprocess.run(command, cwd=output, env=env, stdout=log, stderr=subprocess.STDOUT,
                timeout=timeout, creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        record["commands"][-1]["exitCode"] = result.returncode
        save()
        if required and result.returncode != 0:
            raise RuntimeError(name + " failed; inspect its retained log")
        return result.returncode
    try:
        if os.name == "nt":
            record["hostTools"] = windows_env(env)
        source_root = output / "source"
        extract_tar(archives["apm"], source_root)
        source = source_root / "webrtc-audio-processing-2.1"
        extract_tar(archives["abseil"], source / "subprojects")
        extract_overlay(archives["overlay"], source / "subprojects")
        probe = source / "maestro-probe"
        probe.mkdir()
        for name in SOURCES:
            if name != "Build.py":
                shutil.copyfile(here / name, probe / name)
        manifest = source / "meson.build"
        with manifest.open("a", encoding="utf-8") as file:
            file.write("\nsubdir('maestro-probe')\n")
        build = output / "build"
        meson = [sys.executable, "-m", "mesonbuild.mesonmain"]
        setup = meson + ["setup", build, source, "--wrap-mode=nodownload", "--default-library=static",
            "--buildtype=release", "-Dcpp_std=c++20", "-Dabseil-cpp:cpp_std=c++20",
            "--force-fallback-for=absl_base,absl_flags,absl_strings,absl_numeric,absl_synchronization,absl_bad_optional_access"]
        if args.platform == "windows":
            setup += ["-Db_vscrt=mt"]
        if args.platform == "android":
            cross, readelf, nm = cross_file(args.ndk, output)
            record["ndkProperties"] = (args.ndk / "source.properties").read_text()
            setup += ["--cross-file", cross, "-Dgnustl=disabled"]
        run(setup, "configure")
        run(meson + ["compile", "-C", build, "-j", "6"], "compile")
        if args.platform == "android":
            binary = build / "maestro-probe/libmaestro_echo.so"
            run([readelf, "-h", "-l", "-d", binary], "android-elf", 30)
            run([nm, "-D", "--defined-only", binary], "android-exports", 30)
            metadata = (output / "android-elf.log").read_text()
            symbols = (output / "android-exports.log").read_text()
            if "AArch64" not in metadata or "libc++_shared.so" in metadata:
                raise RuntimeError("Unexpected Android architecture or external C++ runtime")
            loads = [line for line in metadata.splitlines() if line.lstrip().startswith("LOAD ")]
            if not loads or any(int(line.split()[-1], 16) < 16384 for line in loads):
                raise RuntimeError("Android load segments lack 16 KiB alignment")
            needed = re.findall(r"\(NEEDED\).*?\[([^]]+)\]", metadata)
            if set(needed) - {"liblog.so", "libm.so", "libdl.so", "libc.so"}:
                raise RuntimeError("Unexpected Android shared-library dependency")
            record["androidNeeded"] = needed
            record["androidLoadAlignment"] = [int(line.split()[-1], 16) for line in loads]
            for name in ("create", "reset", "render", "capture", "destroy"):
                if "maestro_echo_" + name not in symbols:
                    raise RuntimeError("Missing native ABI export")
            record["status"] = "cross-compiled-not-executed"
        else:
            binary = build / "maestro-probe" / ("maestro_echo.dll" if args.platform == "windows" else "libmaestro_echo.so")
            executable = build / "maestro-probe" / ("maestro_echo_probe.exe" if args.platform == "windows" else "maestro_echo_probe")
            run([executable], "probe", 90)
            lines = (output / "probe.log").read_text().splitlines()
            summary = json.loads(lines[-1])
            if summary.get("status") != "passed" or summary.get("audioCases") != 8:
                raise RuntimeError("Incomplete generated-audio result")
            record["probe"] = summary
            # Retain this tougher diagnostic as FAILED if it fails; it is not a
            # passing speech case and cannot authorize native capture integration.
            stress = run([executable, "--noise-stress"], "noise-stress", 90, required=False)
            stress_log = (output / "noise-stress.log").read_text()
            if stress not in (0, 1) or (stress == 1 and "FAILED: near-end missing or amplified" not in stress_log):
                raise RuntimeError("Noise stress crashed or failed for a different reason")
            record["speechWavs"] = []
            for index, wave in enumerate(args.speech_wav):
                wave = wave.resolve(strict=True)
                record["speechWavs"].append({"path": str(wave), "sha256": digest(wave)})
                run([executable, "--speech-wav", wave], "speech-" + str(index), 90)
            record["noiseStress"] = {"status": "passed" if stress == 0 else "failed", "exitCode": stress}
            record["status"] = "bounded-checks-passed-quality-open"
            record["openQualityFailures"] = 1 if stress else 0
        record["binarySha256"] = digest(binary)
        record["binaryBytes"] = binary.stat().st_size
    except Exception as error:
        record["status"] = "failed"
        record["error"] = str(error)
        raise
    finally:
        record["finished"] = time.time()
        save()
    print(str(receipt))

if __name__ == "__main__":
    main()
