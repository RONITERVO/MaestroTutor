# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Read-only GLB inventory. Produces review data, not a runtime motion pack.

python catalog_animations.py SOURCE_DIRECTORY OUTPUT_JSON
Original exports are never changed. Sparse/compressed/external accessors are
reported as unsupported rather than guessed. Rig equality includes rest poses,
ancestry and inverse bind matrices; this is still not animation acceptance.
"""
import argparse
import hashlib
import json
import math
import struct
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


def digest(value):
    if not isinstance(value, bytes):
        value = json.dumps(value, sort_keys=True, separators=(",", ":")).encode()
    return hashlib.sha256(value).hexdigest()


def inspect(path, source):
    before = path.stat()
    if before.st_size > 256 * 1024 * 1024:
        raise ValueError("Export exceeds inventory limit of 256 MiB")
    raw = path.read_bytes()
    after = path.stat()
    if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
        raise ValueError("Export changed during inspection; retry when download finishes")
    if len(raw) < 20 or struct.unpack_from("<III", raw) != (0x46546C67, 2, len(raw)):
        raise ValueError("Invalid GLB 2 header")
    chunks = {}; offset = 12
    while offset < len(raw):
        length, kind = struct.unpack_from("<II", raw, offset); offset += 8
        if length % 4 or offset + length > len(raw) or kind in chunks:
            raise ValueError("Invalid GLB chunk")
        chunks[kind] = raw[offset:offset+length]; offset += length
    document = json.loads(chunks[0x4E4F534A]); binary = chunks.get(0x004E4942, b"")
    if any(x.get("uri") for x in document.get("buffers", [])):
        raise ValueError("External buffers are not inventoried")
    views = document.get("bufferViews", []); accessors = document.get("accessors", [])

    def view(index):
        v = views[index]; start = v.get("byteOffset", 0); end = start + v["byteLength"]
        if v.get("buffer", 0) != 0 or start < 0 or end > len(binary):
            raise ValueError("Invalid buffer view")
        return binary[start:end]

    def accessor(index):
        a = accessors[index]
        if "sparse" in a or "bufferView" not in a:
            raise ValueError("Sparse or compressed accessor needs a full importer")
        size = {5120:1,5121:1,5122:2,5123:2,5125:4,5126:4}[a["componentType"]]
        components = {"SCALAR":1,"VEC2":2,"VEC3":3,"VEC4":4,"MAT4":16}[a["type"]]
        width = size * components; v = views[a["bufferView"]]; data = view(a["bufferView"])
        start = a.get("byteOffset", 0); stride = v.get("byteStride", width)
        if a["count"] < 0 or start < 0 or stride < width or (a["count"] and start+(a["count"]-1)*stride+width > len(data)):
            raise ValueError("Accessor exceeds its buffer")
        packed = b"".join(data[start+i*stride:start+i*stride+width] for i in range(a["count"]))
        return {"type":a["type"],"componentType":a["componentType"],"count":a["count"],"normalized":a.get("normalized",False),"hash":digest(packed)}, packed

    nodes = document.get("nodes", []); parents = {}
    for i, node in enumerate(nodes):
        for child in node.get("children", []):
            if child in parents: raise ValueError("Multiple node parents")
            parents[child] = i

    def transform_values(values):
        # Unity consumes these node transforms as float32. Normalize only JSON
        # rest-transform precision; animation payloads retain exact byte hashes.
        if values is not None and any(not math.isfinite(float(v)) for v in values): raise ValueError("Nonfinite rest transform")
        return [struct.unpack("<f",struct.pack("<f",float(v)))[0] for v in values] if values is not None else None

    def ancestry(index):
        result = []; seen = set()
        while index is not None:
            if index in seen: raise ValueError("Cyclic node hierarchy")
            seen.add(index); n = nodes[index]
            result.append({"name":n.get("name", ""),"translation":transform_values(n.get("translation",[0,0,0])),"rotation":transform_values(n.get("rotation",[0,0,0,1])),"scale":transform_values(n.get("scale",[1,1,1])),"matrix":transform_values(n.get("matrix"))})
            index = parents.get(index)
        return list(reversed(result))

    geometry = []
    for mesh in document.get("meshes", []):
        primitives = []
        for primitive in mesh["primitives"]:
            if primitive.get("extensions"): raise ValueError("Compressed geometry requires a full importer")
            primitives.append({"mode":primitive.get("mode",4),"attributes":{k:accessor(v)[0] for k,v in primitive["attributes"].items()},"indices":accessor(primitive["indices"])[0] if "indices" in primitive else None,"targets":[{k:accessor(v)[0] for k,v in target.items()} for target in primitive.get("targets",[])]})
        geometry.append(primitives)
    rigs = [{"joints":[ancestry(j) for j in skin["joints"]],"inverseBindMatrices":accessor(skin["inverseBindMatrices"])[0] if "inverseBindMatrices" in skin else None} for skin in document.get("skins",[])]
    textures = []
    for image in document.get("images", []):
        if "bufferView" not in image: raise ValueError("External/data URI images need a full importer")
        data = view(image["bufferView"]); textures.append({"hash":digest(data),"bytes":len(data)})
    clips = []; motion_payloads = {}
    for animation in document.get("animations", []):
        tracks = []; start = float("inf"); end = 0
        for channel in animation["channels"]:
            sampler = animation["samplers"][channel["sampler"]]
            time_meta, times = accessor(sampler["input"]); values_meta, values = accessor(sampler["output"])
            if time_meta["type"] != "SCALAR" or time_meta["componentType"] != 5126: raise ValueError("Invalid animation time accessor")
            keys = struct.unpack("<"+"f"*time_meta["count"], times)
            if any(not math.isfinite(t) or t < 0 for t in keys) or any(a >= b for a,b in zip(keys,keys[1:])): raise ValueError("Invalid animation times")
            if keys: start = min(start,min(keys)); end = max(end,max(keys))
            for metadata, data in [(time_meta,times),(values_meta,values)]: motion_payloads[metadata["hash"]] = len(data)
            tracks.append({"target":ancestry(channel["target"]["node"]),"path":channel["target"]["path"],"interpolation":sampler.get("interpolation","LINEAR"),"times":time_meta,"values":values_meta})
        duration = end-start if start != float("inf") else 0
        clips.append({"name":animation.get("name",""),"fingerprint":digest(tracks),"durationSeconds":duration,"channels":len(tracks),"reviewShortClip":duration < .1})
    return {"file":path.relative_to(source).as_posix(),"category":path.parent.name,"fileHash":digest(raw),"bytes":len(raw),"geometryHash":digest(geometry),"rigHash":digest(rigs),"joints":sum(len(s["joints"]) for s in document.get("skins",[])),"textures":textures,"motionPayloads":motion_payloads,"clips":clips}


def catalog(source):
    files = []; errors = []
    for path in sorted(source.rglob("*.glb")):
        if path.is_symlink() or not path.resolve().is_relative_to(source): continue
        try: files.append(inspect(path, source))
        except (ValueError, KeyError, IndexError, OSError, OverflowError, struct.error) as error:
            errors.append({"file":path.relative_to(source).as_posix(),"error":str(error)})
    textures = {t["hash"]:t["bytes"] for f in files for t in f["textures"]}
    motions = {h:b for f in files for h,b in f["motionPayloads"].items()}
    return {"schemaVersion":1,"generatedUtc":datetime.now(timezone.utc).isoformat(),"note":"Inventory only; no runtime compatibility or contact/root-motion acceptance implied.","summary":{"files":len(files),"errors":len(errors),"sourceBytes":sum(f["bytes"] for f in files),"categories":dict(Counter(f["category"] for f in files)),"geometryVariants":len({f["geometryHash"] for f in files}),"rigVariants":len({f["rigHash"] for f in files}),"clips":sum(len(f["clips"]) for f in files),"uniqueClipFingerprints":len({c["fingerprint"] for f in files for c in f["clips"]}),"shortClipsForReview":sum(c["reviewShortClip"] for f in files for c in f["clips"]),"uniqueTextureBytes":sum(textures.values()),"uniqueMotionAccessorBytes":sum(motions.values())},"files":files,"errors":errors}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("source",type=Path); parser.add_argument("output",type=Path); args = parser.parse_args()
    source = args.source.resolve(); output = args.output.resolve()
    if not source.is_dir(): parser.error("Source must be a directory")
    if output.is_relative_to(source): parser.error("Write the report outside the source exports")
    result = catalog(source); output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(result,indent=2,allow_nan=False),encoding="utf-8")
    print(json.dumps(result["summary"],indent=2)); print("Report:",output)
    raise SystemExit(1 if result["errors"] else 0)
