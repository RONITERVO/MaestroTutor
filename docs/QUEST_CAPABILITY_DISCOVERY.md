# Shared action discovery and availability

PC development checkpoint, 2026-09-27. The catalog now serves the book and the
original-app agent through the same native read-only query path. This is not a
completed event runtime or release acceptance.

## Contract

A standalone room command uses `action: "catalog"` and a `catalog` request:

- `{"operation":"search","query":"avatar","offset":0}` returns at most six
  IDs, labels and versions, sorted by ID. Search matches ID, label and prerequisite
  text; the next page uses the returned offset plus pageSize.
- `{"operation":"inspect","capability":"avatar.gesture.play","version":1}`
  returns the registered schema, timing, ownership channels and prerequisites.
  Unknown IDs/versions return a null definition with an explicit status.
- `{"operation":"check","call":{"id":"time.wait","version":1,"arguments":{"seconds":1}}}`
  validates the concrete invocation and reports `valid`, `available`, `occupied`,
  `resources` and a reason.

The runtime advertises `catalog.v1`. Queries do not edit documents, change
selection, reserve objects, interrupt playback or start an action. A query may
succeed while `valid` or `available` is false. The last check is recomputed in
native observations; readiness is not a reservation or a guarantee that later
asset preparation will finish. Existing handlers revalidate at execution.
Current ownership is conservative and covers whole targets and props.

Schemas mark object references using `x-resource: "object"`, sibling constraints
using `x-requires`, and vector constraints using `format: "boundedOffset"` or
`"unitQuaternion"`. The web program validator uses these annotations directly.
A new registered native action no longer needs a numeric RuleStep mapping for
generic program authoring. The old simple controls remain a private adapter;
unsupported projections open the function editor instead of discarding blocks.

Native JSON parsing rejects duplicate fields, trailing data and excessive depth.
Catalog calls additionally bound object depth, field count, scalar size and
wire length. Structured arguments are explicitly hydrated because Unity's
JsonUtility does not preserve arbitrary JSON objects. Returned definitions must
match the manifest bundled with the web client; changed contracts are rejected.

## Book and agent

The optional Action catalog is inside the two book pages. It can be opened from
Objects or Behaviours. Expert argument editing currently uses JSON plus a schema
reference. Add first block to draft inserts the invocation at the entry function's
start, preserving all existing functions and identities and declaring its resources.
Apply is still explicit. Browsing retains drafts, concurrent revisions block stale
saves, and changing arguments clears the previous availability result.

Chat remains the default workflow. The existing agent can progressively search,
inspect and check instead of receiving every action signature in its prompt.
A turn has up to three action batches and six read-only discovery/inspection
batches; reaching either limit returns budget exhaustion. Receipts survive
cancellation and the original app still owns Gemini, managed usage and BYOK.
This is a finite allowance, not arbitrary autonomous task continuation.

Single actions can now run through [one-off execution](QUEST_ONE_OFF_ACTIONS.md),
using the same call contract. Saving and playing behaviours remains the route for
reusable or multi-step programs. Full room-command coverage, richer typed results,
persistent event state, timers, channel blending and durable native run receipts
remain follow-up work. No new provider service or Unity Gemini client is added.

## Evidence and remaining acceptance

- Full app suite: 1,236 passing tests; lint, TypeScript and shared-boundary guards.
- Unity: 97 EditMode and 77 PlayMode passing tests; three optional private asset
  checks deliberately skipped. Live integration checks show ready, occupied,
  removed-target, invalid-argument and paused-runtime cases without query side
  effects. A running animation continues during an occupied check.
- Actual native observations are committed in
  `test-fixtures/browser/catalogStates.json`, accepted by the real web bridge.
  `Verify-Quest.ps1` exports fresh observations under `Logs/catalog-evidence`.
- Browser tests preserve complex functions when adding a catalog block, clear stale
  readiness and block a concurrent stale save. Headless Chrome exercises the
  complete search/inspect/check/draft/save interface. Its responses are simulated;
  native execution and availability are verified separately in Unity.
- Quest readability, text entry, original-provider planning quality and actual
  headset latency still need acceptance. Device work remains on hold.
