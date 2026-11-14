# Shared virtual-room snapshots

`roomViewCapture.v1` adds `room.view.capture` to the same native capability catalog
used by the agent, generated book controls and user programs. It captures the
current appearance of virtual creations and Maestro. Structured facts remain the
source for identities, positions, contacts, container quantities and program state.
A picture is useful appearance evidence; it is not a physics sensor or proof that
an action completed.

## Capture contract

The native camera takes one 512 x 384 JPEG from the current viewer position and
orientation, with a 60-degree vertical field of view and neutral background.
Maestro, created/imported objects and programmable user buttons can appear. The
book and chat pages, tool trays, scanned room geometry, physical camera frames and
passthrough are excluded. The frame is narrower than the headset display and does
not establish real-world occlusion, hidden geometry or everything the user sees.

The output is metadata: capture ID, SHA-256, dimensions, timestamp, scene revision,
and room-local camera pose. JPEG bytes have a separate acknowledged bridge channel
and are never copied into recurring room observations or action receipts. Both
Android and the desktop development adapter use the shared validating client.
Session, capture identity, hash, dimensions and pose must match. Changing room
clients clears the cache. Native suspension/workspace holds clear the pending
image. Replaying a durable receipt does not take another photograph.

Limits: one capture per second; at most 1,024 selected renderers; at most 98,304
JPEG bytes; native pixels expire after two minutes. Failed rendering preserves
the previous valid image. Capture does not edit objects, save a room, add Undo or
upload anything by itself. A completed receipt can outlive its pixels; callers
must report an unavailable image, not silently take a replacement.

## User and agent share the same evidence

The optional action catalog shows the captured image. In an authorized room task,
the planner can request this ordinary capability when appearance matters. Only
images captured by that task are supplied to subsequent planning and final
narration, using the original app's existing managed or BYOK Gemini route. A
background program or a manual capture is not automatically attached to a task.
The latest task image is supplied with its capture time and virtual-only scope;
the original conversation attachments and eligible Live-turn media stay separate.

The exact task image is journaled before the next provider call and appears under
Task details in the original chat. Up to six images fit the existing read-query
budget. Task history/backup retains those images with their receipts; deleting
source history follows the existing task-journal removal policy. Previously
exported or provider-held copies are independent. Archive import validates image
bytes, dimensions and hash and never replays actions.

Stop, conversation/access changes and native-session loss prevent a new image
upload. If Stop races a completed capture, its acknowledged receipt remains
available; it does not imply the image reached the provider. The client waits at
most five seconds for the matching image channel and refuses mismatched evidence.
Task persistence failure stops subsequent planning/narration. Inline pixels are
redacted in provider diagnostics.

## Implementation and verification boundary

The synchronous native render temporarily selects owned renderers on a private
layer, refuses conflicts on that layer and restores all layers/render targets in
`finally`. Its camera uses current native skinned poses and materials. There is no
second rendered world and no temporary mutation of saved definitions.

PlayMode coverage renders owned content, excludes the book and unrelated scene
renderers, verifies decoded pixels, restores layers, checks rate/hold/session
behavior and proves receipt replay does not retake an image. The full native app
probe exercises the real catalog/action/receipt and separate pixel channel.
Its untracked desktop viewer starts at the scene origin; the captured view is not
claimed to be a representative headset composition. Chrome replays those exact
native acknowledgements and JPEG bytes through the real book action controls.
Mock-provider tests verify managed/BYOK image parity, diagnostic redaction,
Stop/access fences, receipt identity, journaling and backup validation. They do
not demonstrate live model interpretation.

Quest camera/render-pipeline correctness, useful framing, readback/encoding frame
cost, memory and thermal behavior remain device acceptance gates. Synchronous GPU
readback must be profiled on Quest before release; desktop passing tests cannot
establish headset performance. Physical camera integration remains a separate,
unimplemented permissioned input. No camera permission is added by this feature.
