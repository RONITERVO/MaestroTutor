# NVorbis runtime import decoder

Pinned source version and original file hashes are in UPSTREAM.json. Source is
vendored for Unity Mono/IL2CPP, without NuGet build scripts or native binaries.
XIPH-NOTICE.txt also preserves the BSD attribution for acknowledged libvorbis algorithms; it ships as public/licenses/nvorbis-xiph.txt.
The exact upstream MIT license is in LICENSE.txt and public/licenses/nvorbis.txt
(the latter ships with the book's web assets). These APIs are internal dependencies
of Maestro's bounded audio importer; do not expose unrestricted direct decoding.

Local changes (all enumerated with LF-normalized shipped hashes in UPSTREAM.json):

- Codebook.cs: Bound dimensions/entries and reserve setup work; reject incomplete/invalid/zero-progress ordered books; handle 32-bit code lengths and radix overflow.
- Huffman.cs: Preserve all 32 bits of a 32-bit codeword mask.
- Factory.cs: Share one setup reservation budget across all tables for one decoder.
- Residue0.cs: Check classification multiplication before overflow and reserve decode-map allocation.
- StreamDecoder.cs: Bound metadata strings/counts and channel/rate/block headers before allocations.
- SetupBudget.cs: Maestro-added per-decoder 16 MiB conservative setup reservation counter.

Before updating: review the source diff and parser allocations, regenerate the
original/shipped hashes, run CompressedAudioTests and the native audio workshop
checks, then both offline book/headless journeys and Android IL2CPP compilation.
Synthetic fixtures and their generation provenance are in
Assets/Maestro/Tests/Fixtures/CompressedAudio. No runtime FFmpeg dependency exists.
