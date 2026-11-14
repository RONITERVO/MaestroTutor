# Typed program lists and records

The book editor, app-owned agent and Unity runtime share bounded structured values in
the existing behaviour program. This lets a program collect creation results, pass
records to a function and apply the same calculation to several items. It adds no
provider tool, second interpreter or separately saved block representation.

## Format and compatibility

The runtime advertises `structuredValues.v1`. A program opts in with `version:3`
and `dataVersion:1`, retaining the existing `state`, `events`, `resources`, `entry`
and `functions` fields. Existing scalar programs need no change. Authoring is gated
on the feature; unsupported saved programs retain source and diagnostics through the
existing per-program isolation. Saving never starts a run or migrates installed data.

Types are `number`, `boolean`, `text`, `{list: itemType}`, and
`{record: {fieldName: fieldType}}`. A record has exactly its declared fields; all
items in a list have the same structural type. Record field order has no meaning.
Functions can receive and return these types. Locals and state infer nonempty
initial values; an optional `type` supplies the shape for empty lists. Literal
expressions also accept `type`. For example:

```json
{"name":"items","type":{"list":{"record":{"id":"text","red":"number"}}},"initial":[]}
```

An explicit local/state/literal `type` requires `dataVersion:1`, including scalar
annotations. Field names are 1-32 ASCII letters, digits or underscores; reserved
prototype names are rejected. Custom event payloads and switch cases remain scalar.

## Expressions and execution

All operations use the existing `{op,args:[expressions]}` representation:

| Operation | Result |
| --- | --- |
| `length(list)` | Item count |
| `at(list,index)` | Item at a zero-based integer index |
| `append(list,item)` | New list with one additional item |
| `replace(list,index,item)` | New list with that item replaced |
| `remove(list,index)` | New list without that item |
| `field(record,{value:"name"})` | Named field |
| `withField(record,{value:"name"},value)` | New record with that field changed |

Record field selectors must be literal names. An index must be an integer in range;
invalid indexing fails before its destination is assigned. Types are checked when
saving and values/bounds are checked at runtime. `eq` and `ne` compare values
structurally, including numeric equality independent of JSON integer/float encoding.

Values are immutable. Assignment, state and function calls may share an immutable
value, but edits produce a new one and API-returned JSON is detached. Mutating a
copy does not change its source. Assign the new value explicitly with `set` or
`setState`. A runtime limit failure does not roll back earlier assignments or effects.

Native event and fact inputs bind scalar fields; use `at` and `field` to project
them from a collection. With `structuredInputs.v1`, action inputs can also bind a
whole fixed-shape list or record. The program must use `version:3,dataVersion:1`.
The type comes from the capability input schema, and the complete computed call
is validated again before claiming resources or starting an effect. Keep a
schema-valid literal placeholder; it grants no authority when replaced.

A value and its descendant fields cannot both be bound. Static selectors, variant
unions, nullable shapes, records with optional fields and array-index paths remain
literal. Whole nested arrays and records must fit the existing value budgets.
Bindings add no new bulk operation; only capabilities that already accept lists
can receive them. An ID
inside a record grants no authority: effects still require a declared existing
resource or the exact ID returned by this run's native creation action. The existing
limit of 16 creations per run remains. Observed IDs alone do not become editable.

## Bounds and observations

- Numbers and arithmetic must be finite, with magnitude at most 9,007,199,254,740,991.
  This carries exact 32-bit native revisions without the former artificial million
  cutoff. Native action schemas still enforce their physical and integer bounds.
- At most 32 items per list, 8 fields per record, nesting depth 4 and 128 nodes per value.
- Each value has a 1,024-character cost limit. JSON punctuation/escaped text count;
  every number costs 30 characters and every boolean 5, giving the same conservative
  serialized-size bound in JavaScript and Unity.
- State, distinct active function scopes and the final return value together may retain
  at most 1,024 nodes and 8,192 characters by that cost model. This is checked before
  execution advances and after assignment/call/return; oversized data fails visibly.
- Data literals, copy operations and comparisons charge the existing activation
  instruction budget. Computing yields do not renew it. Program/source/block/function
  limits, cancellation, event generations and channel ownership remain unchanged.

`rules.running` exposes `list` and `record` types with compact JSON `value` strings.
The book and agent receive the same bounded state/locals. Those are current-run
observations, not durable values or action receipts. Stop/pause/reload do not restore
or replay a program's state.

## Human editing and remaining scope

Function parameters, returns and locals have recursive type/value controls. Users can
add/remove fields and list items and compose data operations without JSON. Changing
a declaration's type resets its initial literal; existing body calculations must
still validate. Editing an inferred list preserves its type when its last item is
removed, including lists inside records. Invalid drafts retain the previous valid
program. Source and agent
editing use the same validator and canonical definition.

State declarations now use the same recursive controls; see
[the state/signal editor contract](QUEST_EVENT_PROGRAMS.md#visual-state-and-signal-declarations-2026-09-30).
Coordinated record-field renames, shared versioned function libraries and more
convenient block movement remain work. The controls are
optional within the familiar book interface. This increment adds no external flat UI
or automatic background execution.

## Verification and release boundary

Shared contract fixtures exercise both validators, shape/type mismatches, empty lists,
bounds and unsupported features. Native tests cover immutable API copies, numeric
comparison, invalid indexes, retained memory and guessed-ID rejection. A PlayMode
program creates two actual objects, collects their native IDs, changes a copy, passes
the original collection to a typed function and paints the actual objects through
scalar field bindings. Its storage and observations are read back after the normal
pause flush; Stop leaves completed creations intact.

A Chrome walkthrough authors a list of records and an append expression without JSON.
It then displays actual native collection observations and the field binding controls.
Its command acknowledgements and surrounding room are synthetic; Chrome executes no
Unity effects. Screenshots were inspected. PC results are 1,392 app tests, 195 Unity
EditMode and 132 PlayMode tests, with three optional private-asset skips, plus 25
Android bridge tests and successful development ARM64 packaging.

Headset readability, hand/controller editing, sustained performance and real-provider
creation/editing journeys remain unverified. Device work is on hold. Nothing was
installed, deployed or submitted to the store for this increment.

## Shared action inputs and exact revision values (2026-10-03)

The book, agent validator and Unity compiler now derive fixed input types from the
same capability schemas. An independently recorded Chrome edit saves the exact
build/capture/move/reset fixture exercised by native tests; its acknowledgement is
simulated, while the separate native app probe executes the same source through
ordinary room transport. Saving still does not start actions.

Native tests also carry revisions close to the 32-bit maximum through facts,
program locals, action results and reset calls, and reject stale revisions.
Structured argument tests reject undeclared member IDs, empty/duplicate lists,
parent/child binding overlap and static selectors before an effect can start.
The editor only offers structured bindings when the connected runtime advertises
the feature and the program opts into structured data. The 32-bit revision clock
itself is unchanged; this removes the lower shared numeric cutoff, not its eventual
integer-exhaustion boundary.

### Current inputs inside records and lists

Current-input annotations also apply inside present records and literal array
members. The book loads each fact in order and validates all responses before
changing the draft. Missing facts, changed query targets, changed membership or
order, altered guards and session changes cannot silently reuse a reviewed
snapshot. Ordinary preferences and member slot names remain editable.

For a reusable behaviour, the editor creates an ordinary visible Read block and
local per mapped record, followed by the action. Construction capture, for example,
binds `members.0.revision` and `members.1.revision` to two `object.definition`
reads. This requires `indexedInputs.v1`; literal one-off calls remain available
without it. Existing program limits still apply, including 16 locals per function
and 1,024 bytes per value. Sixteen member reads fit an otherwise empty function;
adding them to a function without enough local capacity fails without changing
its draft. No new bulk value or hidden executor bypasses those limits.

Indexed bindings use canonical, existing literal indexes only. They cannot resize
lists, bind a discriminator/static field, overlap parent/child bindings or grant
authority through a replaced literal object ID. Computed arguments undergo the
same final validation and object authorization before execution. A missing read
or stale action stops the run rather than retrying. The UI's sequential reads are
not an atomic room transaction: the native action still checks every revision at
commit, so concurrent edits can cause an explicit stale-edit failure.
