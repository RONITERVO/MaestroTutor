# Quest runtime model import development checkpoint

The solid import tray opens Android's document picker, validates a self-contained
GLB/VRM file, shows a preview and the model's embedded attribution, then adds a
private content-addressed copy to the room after Add. A room record stores the
SHA256 identity, never the external provider URI or original filesystem path.
The original is not changed. Imported objects support existing grip/scale,
paint tint, duplicate/erase/undo and root movement recording. Compatible embedded
glTF clips have manual next/play/stop/loop controls. Files and saved rooms never
start animation playback automatically; pause and grabbing stop playback.

UniVRM/UniGLTF 0.131.2 is pinned to commit
`a4711bbf8c4d10659d3e5568c2e3d7d595005e51`, with VRM 0.x migration and VRM 1.0
loading. Import notices are packaged in Resources/ImportNotices.txt. The shared
watercolor material preserves base textures and alpha cutouts; it approximates
transparent materials as cutouts. Full MToon/translucent material fidelity is
not claimed. VRM springs are disabled. Compatible humanoid GLBs and VRMs can be room objects or
the custom tutor via Use Maestro; see QUEST_CUSTOM_AVATARS.md for retargeting,
posing, persistence and the remaining hardware checks.

## Bounds and unsupported formats

These are development limits, not measured Quest performance guarantees:

- 64 MiB per file; 4 MiB GLB JSON, depth 48; embedded buffer and PNG/JPEG images.
- 512 nodes and skin joints; 128 material parts, 32 materials, 32 images.
- 250,000 counted vertices and 120,000 triangles per model. Shared accessors
  across submeshes are counted conservatively; some otherwise usable models
  need optimization rather than bypassing these limits.
- 128 morph targets per primitive; 4 million morph attribute vertices per model,
  8 million across live imports including the preview.
- 4096 pixels per image side, 32 Mi pixels per model; 64 Mi pixels across live
  imports, 500,000 aggregate counted vertices and six instances including preview
  and the custom tutor.
- 32 clips, increasing key times within one hour, 200,000 sampler output entries
  and 800,000 channel scalar entries. Imported clips contain data, never scripts.
- Four imported room objects, private library at most 32 assets / 256 MiB.
  Library removal/garbage collection UI is still needed; erased models are kept
  so undo, backups and returning room records do not silently lose their assets.

Compressed Draco/Meshopt/KTX content, external/data URIs and sparse accessors are
not supported yet. Failed or missing room models retain a selectable placeholder
and a readable error, so the rest of the room and erase/undo remain usable.
Native copies, content hashes and resource bounds are validated independently.

## Evidence and remaining work

Synthetic, original test fixtures exercise GLB geometry and animation, a VRM 1.0
humanoid, preview/add, file preservation, damaged-copy repair, erase/undo/reload,
pause/stop, and invalid buffer/accessor/hierarchy/resource references. Tests do
not use or distribute the user's avatars. A separate optional read-only audit
can inspect local `.vrm` files and an Editor preview can import one supplied file:

```powershell
./unity/Tools/Verify-Quest.ps1 -Editor 'D:/Tools/Unity/6000.3.24f1/Editor/Unity.exe' -BuildMirror 'D:/Projects/Builds/MaestroQuestVerify' -RenderImports -ModelAuditDirectory 'D:/avatarit' -ModelPreview 'D:/avatarit/vroidmodel3.vrm'
```

The user's directory contains 22 VRM 0.x files with spring-bone settings and no
embedded glTF animation clips. Some have large textures, numerous morph targets,
or duplicated submesh attributes. Compatibility is per file; no claim that all
22 currently import, or that an Editor import establishes Quest performance.
Private audit/render output stays in ignored `.quest-evidence`.

On 2026-09-26, the user's Meshy GLB export with the Mixamo skeleton, Rigged
Character and Current Animation enabled passed preflight, real Unity import,
render and runtime playback. Its SHA256 starts `220A3A4E`; it contains 28 skin
joints, 15,390 triangles, three embedded JPEG textures (base color, normal and
metallic/roughness), and `Running` plus the very short `Running.001` clip.
The file is 9,033,740 bytes. Removing the separate skin/texture controls from
Meshy's GLB export UI did not remove the embedded textures. The shared watercolor
shader visibly retains the base-color texture; it does not reproduce every PBR
texture effect.

The optional `-ModelPreview` check also supplies that local file to a PlayMode
test. For a skinned model with clips, it checks manual playback, actual baked
vertex movement and restoration of the rest pose on Stop. The Meshy run passed
31 EditMode and 25 PlayMode tests and all Editor processes exited successfully.
Its playback render and reports are retained under ignored
`.quest-evidence/meshy-220A3A4E`. This verifies an animated room-object import in
Unity. A later update implements direct Mixamo GLB selection as Maestro, with
named-bone validation, canonical gesture mapping and joint posing; see
QUEST_CUSTOM_AVATARS.md. The optional `-ModelAsMaestro` verification checks the
supplied file through actual tutor replacement, gesture and visible skin
deformation, including upright facing and usable model bounds. This export has
not yet been tested on the headset.

On 2026-09-25, 10 of the 22 local files passed preflight under the above limits.
The other 12 were rejected for texture, morph, material-image or conservative
vertex budgets. `vroidmodel3.vrm` also completed a real Unity Editor import and
render (three renderers, no clips). This is not headset or custom-Maestro QA.

Still required: hardware picker/stream/label acceptance, representative stress
and recovery tests, import optimization, library management, imported-clip rule
actions, custom Maestro headset acceptance, expressions, detailed
licensing UI, and all overall release gates. Hair/cloth interaction physics is
explicitly excluded by the user's subsequent scope decision.

## User's MR collision examples (2026-09-25)

The user asked about throwable balls, animated avatars releasing balls, curtains
folding against the floor, hair responding to objects/controllers, and their
local VRM collection. These are feasible but distinct feature tracks, not a
single automatic result of model import or installing MRUK.

**Latest scope decision:** skip hair and cloth interaction physics. Implement
realistic rigid-item gravity, user grab/throw interactions and scanned-room
collisions. Items must not begin falling before valid floor/room alignment is
available. Steps 1–2 are now implemented with basic Quest 3 user confirmation
(see QUEST_ROOM_PHYSICS.md); step 3 still needs hand attachment and collision
events. Steps 4–5 are retained only as context for the excluded examples.

1. Rigid items: opt-in Decoration/Throwable presets, appropriate simple/convex
   collision geometry, mass, gravity, bounce/friction, tracked release velocity
   and continuous collision detection where appropriate. Keep existing creation
   placement stable by default. Persist settings and settled poses; recovery and
   animation editing must safely suspend simulation without replaying impulses.
2. MR room: MRUK scene permission/setup, hidden EffectMesh floor/wall/furniture
   colliders, one authoritative alignment with the existing XR origin and clear
   unavailable/stale-room behavior. Physical geometry must not move with B/Y
   content recovery. A scanned room does not detect every moving real object.
3. Visual actions: attach an object to a compatible hand, play an animation,
   release at an authored point with velocity/impulse, then hand control to
   physics. Add bounded collision events to the same rule catalog. Animating a
   transform directly does not automatically make a hand/body collision-aware.
4. Hair: retain compatible imported VRM spring chains; add controller/hand/object
   collision proxies to their collision groups, with limits and stable pause,
   posing, locomotion and avatar replacement ownership. This is spring-bone
   motion, not strand-level hair simulation.
5. Curtains: explicit deformable mesh/cloth setup, pinned edges, material settings
   and floor collision handling. Unity's built-in Cloth only supports specified
   sphere/capsule colliders and does not collide directly with arbitrary MRUK
   MeshColliders. A bounded cloth solver/proxy approach or prepared curtain asset
   needs its own prototype and device performance evidence. Arbitrary imported
   meshes cannot be promised automatic physically correct folding.

Primary sources checked:
- https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-manage-scene-data/
- https://docs.unity3d.com/6000.3/Documentation/Manual/class-Cloth.html
- https://vrm.dev/en/univrm1/vrm1_tutorial/springbone/
- https://github.com/vrm-c/UniVRM/releases/tag/v0.131.2
- https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html
