# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
import json
import struct
import tempfile
import unittest
from pathlib import Path
from catalog_animations import inspect, catalog


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def fixture(self, name="sample.glb", rest=0, end=1, corrupt=False):
        binary = struct.pack("<8f",0,1,0,0,0,0,end,0)
        doc = {"asset":{"version":"2.0"},"buffers":[{"byteLength":32}],"bufferViews":[{"buffer":0,"byteLength":8},{"buffer":0,"byteOffset":8,"byteLength":24}],"accessors":[{"bufferView":0,"componentType":5126,"type":"SCALAR","count":2},{"bufferView":1,"componentType":5126,"type":"VEC3","count":100 if corrupt else 2}],"nodes":[{"name":"Hips","translation":[0,rest,0]}],"skins":[{"joints":[0]}],"animations":[{"name":"Motion","samplers":[{"input":0,"output":1}],"channels":[{"sampler":0,"target":{"node":0,"path":"translation"}}]}]}
        encoded = json.dumps(doc).encode(); encoded += b" " * (-len(encoded)%4)
        raw = struct.pack("<5I",0x46546c67,2,28+len(encoded)+len(binary),len(encoded),0x4e4f534a)+encoded+struct.pack("<2I",len(binary),0x004e4942)+binary
        path = self.root/name; path.write_bytes(raw); return path

    def test_renames_keep_content_identity_and_original_bytes(self):
        first = self.fixture(); original = first.read_bytes(); second = self.root/"renamed.glb"; second.write_bytes(original)
        a = inspect(first,self.root); b = inspect(second,self.root)
        self.assertEqual(a["fileHash"],b["fileHash"])
        self.assertEqual(a["clips"][0]["fingerprint"],b["clips"][0]["fingerprint"])
        self.assertEqual(original,first.read_bytes())

    def test_same_names_do_not_hide_changed_bind_pose_or_motion(self):
        a=inspect(self.fixture(),self.root)
        b=inspect(self.fixture("changed-rig.glb",rest=.3),self.root)
        c=inspect(self.fixture("changed-motion.glb",end=2),self.root)
        self.assertNotEqual(a["rigHash"],b["rigHash"])
        self.assertEqual(a["rigHash"],c["rigHash"])
        self.assertNotEqual(a["clips"][0]["fingerprint"],c["clips"][0]["fingerprint"])

    def test_bad_accessor_is_reported_without_losing_valid_entries(self):
        self.fixture(); self.fixture("corrupt.glb",corrupt=True)
        report=catalog(self.root)
        self.assertEqual(report["summary"]["files"],1)
        self.assertEqual(report["summary"]["errors"],1)
        self.assertIn("Accessor exceeds",report["errors"][0]["error"])


if __name__ == "__main__": unittest.main()
