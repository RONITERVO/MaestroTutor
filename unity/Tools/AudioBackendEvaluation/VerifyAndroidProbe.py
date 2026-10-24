# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Check the built ARM64 payload and native ABI without connecting to a device."""
import hashlib,json,re,struct,sys,zipfile,subprocess
from pathlib import Path

project=Path(sys.argv[1]).resolve(); apk=Path(sys.argv[2]).resolve()
assert apk.parent==project.parent and project.parent.parent.name=='audio-unity'
def elf(data):
    assert data[:6]==b'\x7fELF\x02\x01','Expected little-endian ELF64'
    assert struct.unpack_from('<H',data,18)[0]==183,'Expected AArch64'
    offset=struct.unpack_from('<Q',data,40)[0]
    size,count,names=struct.unpack_from('<HHH',data,58)
    headers=[struct.unpack_from('<IIQQQQIIQQ',data,offset+i*size) for i in range(count)]
    def payload(h):return data[h[4]:h[4]+h[5]]
    def string(data,i):return data[i:data.index(b'\0',i)].decode()
    table=payload(headers[names]); sections={string(table,h[0]):h for h in headers}
    strings=payload(sections['.dynstr']); exports=set()
    symbols=sections['.dynsym']; buffer=payload(symbols)
    for i in range(0,len(buffer),symbols[9]):
        name,info,other,index,value,length=struct.unpack_from('<IBBHQQ',buffer,i)
        if index and info>>4 in (1,2):exports.add(string(strings,name))
    dynamic=payload(sections['.dynamic']); needed=[]
    for i in range(0,len(dynamic),16):
        kind,value=struct.unpack_from('<qQ',dynamic,i)
        if kind==1:needed.append(string(strings,value))
    return exports,needed
with zipfile.ZipFile(apk) as package:
    names=package.namelist()
    libraries=[name for name in names if name.startswith('lib/') and name.endswith('.so')]
    assert libraries and all(name.startswith('lib/arm64-v8a/') for name in libraries),'Unexpected native architecture'
    assert 'lib/arm64-v8a/libil2cpp.so' in names and 'lib/arm64-v8a/libphonon.so' in names
    assert not any(name.lower().endswith('.dll') for name in names),'Managed/Windows DLL in IL2CPP APK'
    phonon=package.read('lib/arm64-v8a/libphonon.so')
    original=project/'Assets/Plugins/SteamAudio/Binaries/Android/arm64/libphonon.so'
    native_input_sha=hashlib.sha256(original.read_bytes()).hexdigest()
    assert native_input_sha==sys.argv[5].lower(),'Native input changed after preparation'
    # Android packaging strips debug/local symbols. Reproduce that exact
    # transformation using this build's NDK; arbitrary changed bytes still fail.
    strip=Path(sys.argv[4]).resolve()
    assert strip.name in {'llvm-strip.exe','llvm-strip'} and strip.is_file()
    reproduced=apk.parent/'verified-stripped-libphonon.so'
    subprocess.run([str(strip),'--strip-unneeded','-o',str(reproduced),str(original)],check=True,capture_output=True)
    assert phonon==reproduced.read_bytes(),'Packaged native library differs from reproducibly stripped SDK'
    exports,needed=elf(phonon)
    required=set()
    for file in (project/'Assets/Evaluation/Runtime').glob('*.cs'):
        required.update(re.findall(r'(?:API|NativeDsp)\.(ipl\w+)\s*\(',file.read_text()))
    assert required<=exports,('Missing runtime exports',sorted(required-exports))
    system={'libc.so','libm.so','libdl.so','liblog.so','libandroid.so','libOpenSLES.so','libz.so'}
    for dependency in needed:
        assert dependency in system or 'lib/arm64-v8a/'+dependency in names,('Unpackaged native dependency',dependency)
    elf(package.read('lib/arm64-v8a/libil2cpp.so'))
cpp=list((project/'Library/Bee').rglob('*.cpp'))
assert cpp,'IL2CPP output required'
wrappers=[]; registration=False; runtime=False; tests=False; lease_types=set(); reference_types=set()
for file in cpp:
    text=file.read_text(encoding='utf-8',errors='replace')
    if file.name.startswith('Maestro.AudioCandidate.Runtime'):
        runtime=True
        for name in ["PcmLeasePlayerProbe", "PcmLeaseAdapter", "PcmOutputLease", "PcmOutputBackend"]:
            if re.search(r"\b"+name+r"_\w+",text):lease_types.add(name)
        for name in ["EchoReferencePlayerProbe", "EchoProbeMute", "EchoReferenceStream", "RoomAudioTap"]:
            if re.search(r"\b"+name+r"_\w+",text):reference_types.add(name)
        wrappers+=re.findall(r'\b(ReversePInvokeWrapper_PreparedGraph_LogMessage_\w+)\s*\(',text)

    if file.name=='Il2CppReversePInvokeWrapperTable.cpp':
        registration='ReversePInvokeWrapper_PreparedGraph_LogMessage_' in text
    if file.name.startswith('Maestro.AudioCandidate.Tests'):tests=True
assert runtime and wrappers and registration,'Native callback wrapper missing after High stripping'
assert not tests,'Test assembly leaked into player'
assert len(reference_types)==4,('Listener reference entry missing after High stripping',sorted(reference_types))
assert len(lease_types)==4,('Application lease entry missing after High stripping',sorted(lease_types))
report={'status':'passed','apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),'nativeSha256':hashlib.sha256(phonon).hexdigest(),
        'originalNativeSha256':native_input_sha,'stripToolSha256':hashlib.sha256(strip.read_bytes()).hexdigest(),'nativeTransformation':'llvm-strip --strip-unneeded; exact output bytes verified','architecture':'ARM64','backend':'IL2CPP','requiredExports':len(required),'verifiedExports':sorted(required),
        'nativeDependencies':needed,'reverseCallback':sorted(set(wrappers)),'highStrippingCallbackRegistered':registration,
        'listenerReferenceTypesCompiled':sorted(reference_types),'applicationLeaseTypesCompiled':sorted(lease_types),'testAssemblyIncluded':tests,'deviceRun':False,'audioDeviceAcceptance':False}
Path(sys.argv[3]).write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='verifiedExports'},indent=2))
