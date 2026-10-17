# NLayer runtime import decoder

Pinned source version and original file hashes are in UPSTREAM.json. Source is
vendored for Unity Mono/IL2CPP, without NuGet build scripts or native binaries.
The exact upstream MIT license is in LICENSE.txt and public/licenses/nlayer.txt
(the latter ships with the book's web assets). These APIs are internal dependencies
of Maestro's bounded audio importer; do not expose unrestricted direct decoding.

Local changes (all enumerated with LF-normalized shipped hashes in UPSTREAM.json):

- Decoder/MpegFrame.cs: Recognize Lavc/Lavf encoder delay/padding in the same Xing layout as LAME (FFmpeg 7.1.1 mp3enc.c).
- MpegFile.cs: Reject invalid frames; remove encoder plus synthesis delay across short MPEG frames without shortening logical duration.
- Decoder/LayerIIIDecoder.cs: Correct the 8 kHz short-block Huffman region.
- FLUENDO-NOTICE.txt: Preserve the embedded MIT attribution in shipped notices.

Before updating: review the source diff and parser allocations, regenerate the
original/shipped hashes, run CompressedAudioTests and the native audio workshop
checks, then both offline book/headless journeys and Android IL2CPP compilation.
Synthetic fixtures and their generation provenance are in
Assets/Maestro/Tests/Fixtures/CompressedAudio. No runtime FFmpeg dependency exists.
