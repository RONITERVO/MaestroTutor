# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Offline platform-effect lifecycle checks; no device or provider is accessed."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time

from Build import windows_env

SOURCES = ("BuildEffects.py", "Build.py", "CaptureInput.h", "CaptureInput.cpp", "DeviceAudioClock.h",
    "CaptureEffects.h", "CaptureEffects.cpp", "EffectsProbe.cpp", "AndroidCaptureDevice.cpp",
    "AndroidCaptureEffects.cpp", "java/com/maestro/quest/audio/SessionEffects.java",
    "java/com/maestro/quest/audio/PlatformCaptureEffects.java",
    "java/com/maestro/quest/audio/SessionEffectsProbe.java")

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--jdk", required=True, type=Path)
    parser.add_argument("--android-jar", type=Path)
    parser.add_argument("--ndk", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2] / ".quest-evidence/echo-effects"
    output = args.output.resolve()
    if output == root or not output.is_relative_to(root) or output.exists():
        parser.error("Use a fresh child directory of this checkout's .quest-evidence/echo-effects")
    if bool(args.android_jar) != bool(args.ndk):
        parser.error("Provide both --android-jar and --ndk for Android cross-compilation")
    extension = ".exe" if os.name == "nt" else ""
    javac = (args.jdk / "bin" / ("javac" + extension)).resolve(strict=True)
    java = (args.jdk / "bin" / ("java" + extension)).resolve(strict=True)
    output.mkdir(parents=True)
    receipt = output / "result.json"
    record = dict(version=1, status="running", started=time.time(),
        sourceHashes={name: digest(here/name) for name in SOURCES},
        deviceOpened=False, physicalAudio=False, providerUsed=False,
        unityIntegration=False, questAcceptance=False, commands=[])
    def save():
        receipt.write_text(json.dumps(record, indent=2)+"\n", encoding="utf-8")
    env = os.environ.copy()
    def run(argv, name, timeout=90):
        argv = list(map(str, argv))
        command = dict(argv=argv, log=name+".log")
        record["commands"].append(command); save()
        with (output/(name+".log")).open("w", encoding="utf-8") as log:
            result = subprocess.run(argv, stdout=log, stderr=subprocess.STDOUT, env=env,
                cwd=output, timeout=timeout, creationflags=subprocess.CREATE_NO_WINDOW if os.name=="nt" else 0)
        command["exitCode"] = result.returncode; save()
        if result.returncode != 0:
            raise RuntimeError(name+" failed; see "+str(output/(name+".log")))
        return (output/(name+".log")).read_text(encoding="utf-8", errors="replace")
    try:
        cpp = [here/name for name in ("EffectsProbe.cpp","CaptureEffects.cpp","CaptureInput.cpp","AndroidCaptureDevice.cpp")]
        binary = output/("effects_probe"+extension)
        if os.name=="nt":
            record["hostTools"] = windows_env(env)
            compiler = Path(record["hostTools"]["compiler"])/"bin/Hostx64/x64/cl.exe"
            run([compiler,"/nologo","/std:c++20","/EHsc","/W4","/WX","/O2","/MT",*cpp,"/Fe:"+str(binary)],"native-compile")
        else:
            run(["c++","-std=c++20","-Wall","-Wextra","-Werror","-O2",*cpp,"-o",binary],"native-compile")
        native = json.loads(run([binary],"native-probe").splitlines()[-1])
        if native.get("status")!="passed" or native.get("effectsCases")!=17 or native.get("deviceOpened") is not False:
            raise RuntimeError("Incomplete native effect ownership evidence")
        record["nativeProbe"] = native
        package = here/"java/com/maestro/quest/audio"
        classes = output/"classes"; classes.mkdir()
        run([javac,"--release","17","-Xlint:all","-Werror","-d",classes,
            package/"SessionEffects.java",package/"SessionEffectsProbe.java"],"java-compile")
        jvm = json.loads(run([java,"-cp",classes,"com.maestro.quest.audio.SessionEffectsProbe"],"java-probe").splitlines()[-1])
        if jvm.get("status")!="passed" or jvm.get("sessionEffectsCases")!=20 or jvm.get("deviceOpened") is not False:
            raise RuntimeError("Incomplete Java effect ownership evidence")
        record["javaProbe"] = jvm
        if args.ndk:
            ndk = args.ndk.resolve(strict=True)
            jar = args.android_jar.resolve(strict=True)
            host = "windows-x86_64" if os.name=="nt" else "linux-x86_64"
            tools = ndk/"toolchains/llvm/prebuilt"/host/"bin"
            android = output/"effects_probe_arm64"
            run([tools/("clang++"+extension),"--target=aarch64-linux-android32","-std=c++20",
                "-Wall","-Wextra","-Werror","-O2","-fPIC","-static-libstdc++",
                "-Wl,--exclude-libs,ALL","-Wl,-z,max-page-size=16384",*cpp,here/"AndroidCaptureEffects.cpp",
                "-laaudio","-o",android],"android-native-compile")
            metadata=run([tools/("llvm-readelf"+extension),"-h","-l","-d","-s",android],"android-elf")
            needed=re.findall(r"Shared library: \[(.*?)\]",metadata)
            loads=[line for line in metadata.splitlines() if line.strip().startswith("LOAD")]
            alignment=[int(line.split()[-1],16) for line in loads]
            if ("AArch64" not in metadata or not loads or min(alignment)<16384 or
                set(needed)-{"libaaudio.so","libm.so","libdl.so","libc.so"} or
                "libaaudio.so" not in needed or "AAudioStream_getSessionId" not in metadata or
                "MakeAndroidCaptureEffects" not in metadata):
                raise RuntimeError("Unexpected Android JNI/AAudio linkage")
            android_classes=output/"android-classes";android_classes.mkdir()
            run([javac,"--release","17","-Xlint:all","-Werror","-cp",jar,"-d",android_classes,
                package/"SessionEffects.java",package/"PlatformCaptureEffects.java"],"android-java-compile")
            record["android"] = dict(status="cross-compiled-not-executed",binarySha256=digest(android),
                androidJarSha256=digest(jar),needed=needed,loadAlignment=alignment,
                classes={str(f.relative_to(android_classes)):digest(f) for f in android_classes.rglob("*.class")})
        record["binarySha256"] = digest(binary)
        record["status"] = "ownership-checks-passed-device-unverified"
    except Exception as error:
        record["status"]="failed";record["error"]=str(error);raise
    finally:
        record["finished"]=time.time();save()
    print(str(receipt))

if __name__=="__main__":
    main()
