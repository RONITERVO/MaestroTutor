# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Run an audited, separate diagnostic APK against synthetic Quest storage.
Never touches the production app, tracking settings, or existing room files.
"""
import argparse, base64, hashlib, io, json, re, subprocess, tarfile, time, uuid
from pathlib import Path

PACKAGE = 'com.maestro.quest.storageprobe'
ACTIVITY = PACKAGE + '/com.unity3d.player.UnityPlayerActivity'
DEVICE_ROOT = '/sdcard/Android/data/' + PACKAGE + '/files/storage-probe'
REPO = Path(__file__).resolve().parents[2]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--adb', required=True)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--audit', type=Path, required=True)
    parser.add_argument('--byte-faults', action='store_true', help='Partial native writes, injected ENOSPC and repeated saves')
    parser.add_argument('--smoke', action='store_true', help='Only the first byte-fault case; not the full matrix')
    args = parser.parse_args()
    if args.smoke and not args.byte_faults: parser.error('--smoke requires --byte-faults')
    audit = json.loads(args.audit.read_text(encoding='utf-8-sig'))
    apk = Path(audit['apk'])
    assert audit['package'] == PACKAGE and audit['debuggable'] and audit['signatureVerified']
    assert audit['noVrCategory'] and audit['noNetworkPermission'] and audit['arm64Only']
    digest = hashlib.sha256(apk.read_bytes()).hexdigest()
    assert digest.lower() == audit['sha256'].lower(), 'APK changed since audit'
    adb = [str(Path(args.adb).resolve()), '-P', '5041', '-s', args.serial]
    flags = getattr(subprocess, 'CREATE_NO_WINDOW', 0)
    evidence = REPO / '.quest-evidence/android-storage' / uuid.uuid4().hex
    evidence.mkdir(parents=True)
    def run(*command, check=True):
        return subprocess.run(adb + list(command), capture_output=True, check=check, timeout=90, creationflags=flags)
    def text(*command, check=True):
        return run(*command, check=check).stdout.decode('utf-8').strip()
    def save(path, value):
        path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
    def pid():
        value=text('shell', 'pidof', PACKAGE, check=False)
        if value: assert re.fullmatch(r'[0-9]+', value), 'Unexpected process inventory'
        return value
    def stop():
        run('shell', 'am', 'force-stop', PACKAGE)
        assert not pid(), 'Diagnostic process did not stop'
    def launch(case=None, mode=None):
        assert not pid(), 'Previous diagnostic process is still live'
        command=['shell', 'am', 'start', '-n', ACTIVITY]
        if case: command+=['--es', 'probe', case, '--es', 'mode', mode]
        run(*command)
    def remote_json(case, name, optional=False):
        result=run('shell', 'cat', DEVICE_ROOT+'/'+case+'/'+name, check=False)
        if result.returncode:
            if optional: return None
            raise RuntimeError('Missing probe evidence: '+name)
        return json.loads(result.stdout)
    def wait(case, name, seconds=60):
        deadline=time.monotonic()+seconds
        while time.monotonic()<deadline:
            failure=remote_json(case,'failure.json',True)
            if failure: raise RuntimeError(json.dumps(failure))
            value=remote_json(case,name,True)
            if value: return value
            time.sleep(.25)
        raise TimeoutError('Probe did not reach '+name+'; no automatic relaunch')
    def put(case, name, value, local):
        save(local/name,value)
        run('push',str(local/name),DEVICE_ROOT+'/'+case+'/'+name)
    def kill(case, marker, mode, local, owner):
        ready=wait(case,marker+'-ready.json')
        assert ready['version']==1 and ready['id']==case
        current=pid();assert current and int(current)==ready['pid']
        expected=remote_json(case,'expected.json')
        if mode=='write':
            r=expected['before' if owner['stage'] in ('before-journal','prepared') else 'after']['room']
            m=expected['before' if owner['stage'] in ('before-journal','prepared','room') else 'after']['memory']
        else: r,m=expected['expected']['room'],expected['expected']['memory']
        assert ready['primaryRoom']==r and ready['primaryMemory']==m, 'Wrong intermediate pair'
        if ready['stage']=='before-journal': assert ready['journal'] is None
        else:
            journal=json.loads(base64.b64decode(ready['journal']))
            assert journal['phase']==('committed' if expected['committed'] else 'prepared')
        # run-as restricts the SIGKILL to the diagnostic UID. Verify identity immediately
        # before sending it; never kill by a stale PID or stop another package.
        command_line=run('shell','run-as',PACKAGE,'cat','/proc/'+current+'/cmdline').stdout
        assert command_line.split(b'\0')[0].decode()==PACKAGE
        stat=text('shell','run-as',PACKAGE,'cat','/proc/'+current+'/stat')
        assert pid()==current
        run('shell','run-as',PACKAGE,'kill','-9',current)
        deadline=time.monotonic()+10
        while pid() and time.monotonic()<deadline: time.sleep(.1)
        assert not pid(), 'Killed process still present; do not relaunch'
        put(case,marker+'-stopped.json',{'version':1,'id':case,'pid':int(current),'forced':True,'signal':9,'statBefore':stat,'absentAfter':True},local)
        stop() # Prevent Android reusing the old Activity/Intent on the next launch.
    def audit_files(case, owner, local):
        data=run('exec-out','tar','-cf','-','-C',DEVICE_ROOT,case).stdout
        (local/'device-case.tar').write_bytes(data)
        with tarfile.open(fileobj=io.BytesIO(data)) as archive:
            names={m.name for m in archive.getmembers()}
            def read(name):
                full=case+'/'+name
                return archive.extractfile(full).read() if full in names else None
            result=json.loads(read('verified.json'))
            expected=json.loads(read('expected.json'))
            assert result['id']==case and result['headset'] and not result['powerLoss']
            assert result['runtime'].endswith('/ Android') and result['backupsMatched'] and result['idempotent']
            assert result['stage']==owner['stage'] and result['firstSave']==owner['firstSave'] and result['recoveryStop']==owner['recoveryStop']
            n=3 if result['committed'] else None if owner['firstSave'] else 2
            backup=None if owner['firstSave'] else 2 if result['committed'] else 1
            for suffix, want, key in [('',n,'expected'),('.backup',backup,'backups')]:
                room=read('workspace/room.v20.json'+suffix);memory=read('workspace/program-memory.v1.json'+suffix)
                for wire, kind in [(room,'room'),(memory,'memory')]:
                    assert (base64.b64encode(wire).decode() if wire is not None else None)==expected[key][kind]
                if want is None: assert room is None and memory is None
                else:
                    r=json.loads(room);m=json.loads(memory)
                    assert next(o for o in r['objects'] if o['id']=='book')['position']['x']==want
                    assert m['programs'][0]['id']=='a'*32 and m['programs'][0]['cells'][0]['value']==want
            assert read('workspace/room-snapshot.v19.json') is None
            save(local/'verified.json',result)
            return result
    def byte_audit(case, owner, local):
        data=run('exec-out','tar','-cf','-','-C',DEVICE_ROOT,case).stdout
        (local/'device-case.tar').write_bytes(data)
        with tarfile.open(fileobj=io.BytesIO(data)) as archive:
            files={m.name.removeprefix(case+'/'):archive.extractfile(m).read() for m in archive.getmembers() if m.isfile()}
        result=json.loads(files['verified.json'])
        assert result['id']==case and result['headset'] and result['ioFault']==owner['ioFault']
        actual={name.removeprefix('workspace/'):base64.b64encode(value).decode() for name,value in files.items() if name.startswith('workspace/')}
        assert actual==result['files'], 'Independent archive differs from application inventory'
        if owner['ioFault']=='stress':
            assert result['saves']==256
            for suffix,n in [('',256),('.backup',255)]:
                room=json.loads(files['workspace/room.v20.json'+suffix])
                memory=json.loads(files['workspace/program-memory.v1.json'+suffix])
                assert next(o for o in room['objects'] if o['id']=='book')['position']['x']==n%20
                assert memory['programs'][0]['cells'][0]['value']==n
            assert len(actual)==5, 'Repeated saves leaked stage or journal files'
        else:
            expected=json.loads(files['expected.json']);hit=json.loads(files['native-hit.json'])
            assert hit['prefixBytes']==7 and hit['requestedBytes']>7
            name=Path(hit['path']).name
            assert re.fullmatch(re.escape(expected['target'])+r'\.snapshot\.[0-9a-f]{32}',name)
            assert hit['path'].startswith('/storage/emulated/0/Android/data/'+PACKAGE+'/files/storage-probe/'+case+'/workspace/')
            if owner['ioFault']=='block':
                assert hit['mode']==1 and result['readOnlyRefusal'] and len(files['workspace/'+name])==7
                stopped=json.loads(files['writer-stopped.json'])
                assert stopped['pid']==hit['pid'] and stopped['signal']==9 and stopped['absentAfter']
                for kind,filename,after_stages in [
                    ('room','room.v20.json',('room','memory')),
                    ('memory','program-memory.v1.json',('memory',))]:
                    want=expected['after' if owner['stage'] in after_stages else 'before'][kind]
                    assert actual[filename]==want
                assert actual['room.v20.json.backup']==expected['backups']['room']
                assert actual['program-memory.v1.json.backup']==expected['backups']['memory']
                assert len([x for x in actual if '.snapshot.' in x])==1
                if owner['stage']=='before-journal': assert 'room-snapshot.v19.json' not in actual
                else: assert json.loads(files['workspace/room-snapshot.v19.json'])['phase']=='prepared'
            else:
                assert hit['mode']==2 and result['recovered'] and result['idempotent']
                failed=json.loads(files['write-failed.json'])
                assert failed['nativeEnospcCount']>0 and failed['type'].endswith('IOException')
                for kind,filename in [('room','room.v20.json'),('memory','program-memory.v1.json')]:
                    assert actual[filename]==expected['expected'][kind]
                    assert actual[filename+'.backup']==expected['backups'][kind]
                assert len(actual)==5, 'I/O recovery left staging or journal files'
        save(local/'verified.json',result)
        return {k:v for k,v in result.items() if k!='files'}
    def byte_cases():
        assert audit.get('nativeFaultHookVerified'), 'Native hook was not audited'
        cases=[('block',s) for s in ('before-journal','prepared','room','memory')]
        cases += [('enospc',s) for s in ('before-journal','prepared','room','memory','committed','room-backup')]
        cases += [('stress','before-journal')]
        if args.smoke: cases=cases[:1]
        for fault,stage in cases:
            case=uuid.uuid4().hex;local=evidence/case;local.mkdir()
            owner={'version':1,'id':case,'package':PACKAGE,'stage':stage,'firstSave':False,'recoveryStop':None,'ioFault':fault}
            run('shell','mkdir',DEVICE_ROOT+'/'+case)
            put(case,'owner.json',owner,local);launch(case,'write')
            if fault=='block':
                hit=wait(case,'native-hit.json')
                current=pid();assert current and int(current)==hit['pid'] and hit['prefixBytes']==7
                command_line=run('shell','run-as',PACKAGE,'cat','/proc/'+current+'/cmdline').stdout
                assert command_line.split(b'\0')[0].decode()==PACKAGE and pid()==current
                # Confirm the write really left a partial staging file before killing.
                assert re.fullmatch('/storage/emulated/0/Android/data/'+re.escape(PACKAGE)+'/files/storage-probe/'+case+r'/workspace/[a-z0-9.-]+\.snapshot\.[0-9a-f]{32}',hit['path'])
                assert int(text('shell','stat','-c','%s',hit['path']))==7
                run('shell','run-as',PACKAGE,'kill','-9',current)
                deadline=time.monotonic()+10
                while pid() and time.monotonic()<deadline: time.sleep(.1)
                assert not pid()
                put(case,'writer-stopped.json',{'version':1,'id':case,'pid':int(current),'signal':9,'absentAfter':True},local)
                stop();launch(case,'verify')
            wait(case,'verified.json',180 if fault=='stress' else 60);stop()
            results.append(byte_audit(case,owner,local))
            print(json.dumps({'passed':len(results),'ioFault':fault,'stage':stage}),flush=True)
        save(evidence/'verified.json',{'version':1,'cases':results,'smokeOnly':args.smoke,'forcedTerminations':sum(c['ioFault']=='block' for c in results),'headset':True,'powerLoss':False,
            'inFlightByteWrite':True,'injectedEnospc':any(c['ioFault']=='enospc' for c in results),'fullDisk':False,'productionApp':False,'apkSha256':digest,
            'independentFileAudit':True,'evidence':str(evidence)})
        print(str(len(results))+' Android native-write/stress cases passed. Evidence: '+str(evidence),flush=True)
    results=[];owned=False
    save(evidence/'package-audit.json',audit)
    try:
        assert text('get-state')=='device'
        installed=text('shell','pm','path',PACKAGE,check=False)
        if not installed:
            run('install',str(apk))
            installed=text('shell','pm','path',PACKAGE)
        path=installed.removeprefix('package:')
        assert re.fullmatch(r'/data/app/[A-Za-z0-9_+/=.~-]+/base\.apk',path)
        assert text('shell','sha256sum',path).split()[0]==digest, 'Another diagnostic APK is installed; inspect before replacing it'
        owned=True
        save(evidence/'installed.json',{'package':PACKAGE,'serial':args.serial,'sha256':digest,'installedHashMatched':True})
        stop();launch()
        deadline=time.monotonic()+60
        while time.monotonic()<deadline:
            if run('shell','test','-d',DEVICE_ROOT,check=False).returncode==0: break
            time.sleep(.25)
        else: raise TimeoutError('Diagnostic startup did not create its private test root')
        stop()
        if args.byte_faults:
            byte_cases()
            return
        cases=[(s,False,None) for s in ('before-journal','prepared','room','memory','committed','room-backup','memory-backup')]
        cases += [(s,True,None) for s in ('prepared','room','memory','committed')]
        cases += [(s,False,'recovered-room') for s in ('room','committed')]
        for stage, first, recovery in cases:
            case=uuid.uuid4().hex;local=evidence/case;local.mkdir()
            owner={'version':1,'id':case,'package':PACKAGE,'stage':stage,'firstSave':first,'recoveryStop':recovery}
            run('shell','mkdir',DEVICE_ROOT+'/'+case)
            put(case,'owner.json',owner,local)
            launch(case,'write');kill(case,'writer','write',local,owner)
            if recovery: launch(case,'recoverStop');kill(case,'recovery','recoverStop',local,owner)
            launch(case,'verify');wait(case,'verified.json');stop()
            results.append(audit_files(case,owner,local))
            print(json.dumps({'passed':len(results),'stage':stage,'firstSave':first,'recoveryStop':recovery}),flush=True)
        save(evidence/'verified.json',{'version':1,'cases':results,'forcedTerminations':15,'headset':True,'powerLoss':False,'inFlightByteWrite':False,'fullDisk':False,'productionApp':False,'apkSha256':digest,'independentFileAudit':True,'evidence':str(evidence)})
        print('All 13 Android process-termination cases passed. Evidence: '+str(evidence),flush=True)
    except BaseException as error:
        save(evidence/'failure.json',{'error':str(error),'completedCases':results})
        raise
    finally:
        # Preserve every test file for inspection. Only stop this diagnostic package.
        if owned: stop()
        (evidence/'exit-info.txt').write_text(text('shell','dumpsys','activity','exit-info',PACKAGE),encoding='utf-8')
        (evidence/'battery.txt').write_text(text('shell','dumpsys','battery'),encoding='utf-8')
        print('Retained evidence: '+str(evidence),flush=True)

if __name__=='__main__': main()
