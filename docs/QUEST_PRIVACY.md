# Quest v1 privacy and audience data map

Owner decision on 2026-10-02: Quest v1 targets adults 18+. This is implementation
and release evidence, not a statement that Meta or Google has approved the app.
`public/privacy.html` is the prepared public disclosure; deployment remains separate.

## Entry and lifecycle

`QuestAudienceGate` is used only by the `surface=quest-book` entry. Its unchecked
checkbox and separate Open button are required before the original App and book
surface mount. Under-18 decline keeps them inaccessible. A new document requires
confirmation again. There is no persisted age flag, date of birth or identity check.
A minimal lifecycle bridge acknowledges native suspend/resume while reading a
policy in the external browser, but refuses all commands, files, identity results,
library state and room actions. Returning does not grant confirmation or start audio.
No separate Gemini client exists in Unity. Native manual editing remains available.

The allowlisted policy URLs are exact HTTPS URLs without query strings or fragments.
Android requires a gesture in the local top-level book before queueing navigation;
Unity permits only the account-approval page, privacy policy and Gemini terms.
The gate is an audience notice and self-confirmation, not authentication or proof
that children cannot access the whole app. Store positioning/distribution and the
provider's audience conditions still require final review.

## Data and retention

| Data | Processing and persistence | Removal / boundary |
| --- | --- | --- |
| Chat, attachments, settings and task journals | Original app's local browser storage. Source context and eligible Live handoff media are in the task journal. | Pruned with source history; chat exports include journals. Previously exported or provider-held copies remain separate. |
| Models, animations, drawings, authored objects and programs | Native headset app storage, with explicit file import/export. | Workspace/library deletion and app-data clearing; external backups are separate. Chat backup is not a native-room backup. |
| Meta scene meshes and continuous head/hand/controller tracking | Local collision, interaction and navigation. | Raw mesh and continuous tracking are not sent. Room status and authored object state/positions can be sent for tasks. Explicit `room.scan` queries can send bounded room/anchor IDs, semantic labels, poses and rectangle/box bounds through the existing AI connection and retain them in task history/backups. They reveal room layout even without images. No automatic layout broadcast. Meta's stored scan is controlled by the system, not chat deletion. |
| Microphone / enabled camera input | Existing speech and visual-context paths to Gemini. Live delegation can resend that turn's audio/frames for planning and final narration. | Permissions and camera controls stay separate. Current Quest passthrough is display-only and the APK has no camera permission. Delegated media is retained with task history, not only in transient Live buffers. |
| Virtual-room snapshots | On-demand native images of authored objects, Maestro and user buttons; excludes book/chat, tool trays, scanned geometry and physical camera. Explicitly capturing tasks send the same pixels through the existing AI route and save them in task details. | Native images expire after two minutes/session change; task copies follow task-history deletion and backup retention. Manual/background captures are not automatically sent to AI. |
| AI task requests/results | Original managed Firebase/Cloud service then Gemini, or direct BYOK Gemini. | Same provider terms, spend and upload cleanup as original app. Relevant scene/program/context data may be included; the policy does not promise provider deletion when local history is removed. |
| Meta entitlement / integrity | SDK checks, short-lived challenge and provider verification, then Firebase App Check. | No optional Meta profile/friends/avatar APIs or retained Meta device ID/token. Challenge hash expires in five minutes, consumed challenges deleted immediately. |
| Quest account pairing | Hashed code/secret, times, state and approved Maestro UID. | Five-minute expiry/TTL; consume/cancel removes UID and secret hash. Account deletion removes outstanding approvals. |
| Rate limits | Hashed ingress IP and, for approval, UID subjects. | Two-minute scheduled deletion; expiry enforced independently of asynchronous TTL. |

Source owners: `src/core-sdk/room`, `docs/QUEST_UNIFIED_AGENT.md`,
`docs/QUEST_ROOM_ENVIRONMENT.md`, `functions/src/questAttestationStore.ts`,
`functions/src/questAccountLinkStore.ts`, `src/platform/quest` and
`unity/NativeBrowser`. Existing managed billing/upload/report retention remains in
the public policy. Do not equate local storage, cloud account deletion and exported
files. OS backups and other installations can retain independent copies.

## Release gates

- Deploy the reviewed privacy policy and verify its public link before distribution.
- Verify the actual packaged audience notice and policy round trip on Quest, including
  hand/controller input, suspension, new-session confirmation and under-18 decline.
- Align adult marketing, Store audience/distribution and truthful IARC responses.
  Meta's teens/adults category includes 13+; it is not an 18+ restriction.
- Verify provider terms and enabled billing/region eligibility for managed and BYOK
  access. An age checkbox does not verify the provider project's billing status.
- Complete truthful privacy/data-use disclosures for the actual production services
  and provide an appropriate private support/deletion contact before launch.
- Exercise real Meta integrity and Google/Firebase linking after owner configuration.
  They remain disabled/undeployed; simulated acceptance is not provider acceptance.

Sources checked 2026-10-02:
[Gemini terms](https://ai.google.dev/gemini-api/terms),
[Meta audience guidance](https://developers.meta.com/vr/resources/age-groups/),
[Meta DUC](https://developers.meta.com/vr/resources/publish-data-use/).


Saved scanned ink layers retain the selected Meta room/anchor identities,
plane-local placement, dimensions and user-authored ink in the native room and
workspace export. Exact identities are used to restore or explicitly rebind the
layer. Local boundary outlines validate placement but are not saved in the layer
or included in layout facts. Explicit agent inspection can include these saved
identities and current availability through the existing Maestro request path.
Deleting a layer removes its current source; ordinary Undo and retained workspace
backups may still contain it under the documented retention controls.
