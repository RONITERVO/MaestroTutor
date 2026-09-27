# Shared motion discovery

The original Maestro agent can search the same local animation library as the book
when Unity advertises `motions.v1`. Names and tags find compatible saved motions;
favorites, short-clip and archive filters support larger collections. Each result
page contains at most 12 entries. The existing 1,024-entry library limit remains.
This adds discovery to the original chat/Live handoff, not another provider client.

A request is a standalone room command:

```json
{"action":"motions","target":"maestro","motionQuery":{"query":"wave","offset":0,"includeShort":false,"favouritesOnly":false,"archivedOnly":false}}
```

The native observation's `motions` field contains the target/model identity,
readiness and status, the query, offset/total and entries with stable ID, name, tags,
duration, short-clip/favorite/archive flags and download availability. The field is
null before the first query. When more results exist, request offset + 12. Searches
match a name or tag substring, without case sensitivity; they are not semantic or
cross-language search. An empty query browses the compatible library.

`MotionLibrary.Search` supplies both the manual book and the agent, sharing filters,
sorting and page bounds. Unity checks the currently loaded model's rig, and later
observations recompute compatibility. A missing, loading or replaced model cannot
leave old results presented as compatible. An unavailable/read-only catalogue is
reported as unavailable, rather than an empty successful search. Search never
changes room selection, saved revisions, Undo, visibility or playback.

The agent can use a returned downloaded ID in a library-motion step or program,
through the existing revision-checked `rules` commands. Playback checks the actual
target rig again. Search alone does not assign a motion or establish playback;
receipts and live rule observations distinguish these stages. Current tasks still
allow three command batches, so long searches or edits may require a continuation.
Animation names/tags are data, not instructions or authority to change the room.

This does not add file import, cross-rig retargeting, an agent action for walking or
activity-profile assignments, semantic ranking, or a new asset-picker UI. Those
remaining action-coverage and authoring features stay on the v1 work list.

## Verification

Shared JSON fixtures cover required fields, paging limits, flags, control characters,
unknown arguments and rejection of a query mixed with mutations. Native PlayMode
compares book/agent pages over distinct fixture animations, excludes a different
rig, verifies unchanged room/rule revisions and selection, saves and plays the
returned motion, checks renamed tags/favorites/archive changes, and clears results
on avatar replacement. The fixture uses original synthetic GLB data.

`test-fixtures/browser/motionSearchState.json` is an actual native observation from
that test. Web bridge tests consume it, check native acknowledgement handling and
reject malformed pages. A mocked-provider application journey verifies that the
next planner call receives the discovered native ID before it saves a behavior.
These checks do not establish real-provider or headset acceptance.

The packaged checkpoint and test totals are recorded in QUEST_DEVICE_QA.md.
Local build/test reports and source/APK hashes are retained under ignored
`.quest-evidence/motion-search/`. Device work remains on hold.
