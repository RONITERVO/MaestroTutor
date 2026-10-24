# Live room action integration

**Superseded v1 direction (2026-09-26):** room tasks are launched by the original
Maestro chat/suggestion tool coordinator. See [QUEST_UNIFIED_AGENT.md](QUEST_UNIFIED_AGENT.md).
Direct Live function calling remains disabled experimental infrastructure, not a
required Quest v1 path. The remaining steps below apply only if that optional
transport is revisited; do not enable it as part of the current unified-task work.

Status (2026-09-26): transport foundation implemented and locally tested. **Live
voice cannot perform room actions in the shipped app yet.** The new managed path
is disabled by default (`MANAGED_LIVE_ROOM_TOOLS_ENABLED=false`). No backend
service was deployed, no provider request was made and no Quest was accessed for
this checkpoint. The last packaged APK remains 7EC419D6; this work changes server
and shared TypeScript only.

## Shared contract implemented

`shared/prompts/room.ts` exports two synchronous function declarations:

- `observeMaestroRoomV1`: request an observation and a host-issued token.
- `actInMaestroRoomV1`: provide that token and a batch using the existing
  `ROOM_AGENT_SCHEMA`. The native executor remains the authority for command
  validation, revision checks, permission and receipts.

Only these exact declarations are admitted by the opted-in gateway issuer. Custom
functions, changed descriptions/schemas, asynchronous behaviour, search and tool
configuration overrides remain rejected. Legacy ephemeral tokens cannot enable
room tools. The socket cannot replace the configuration pinned to its one-use
ticket. The gateway also checks the declarations before opening the provider.

The names carry the proposed wire version. Before release, freeze the v1 catalogue;
a later schema/description change must preserve v1 or add an explicitly supported
version. The catalogue is still pre-release and currently derives its command
schema from the shared typed-request definition.

`shared/roomLiveProtocol.ts` owns pending IDs for one connection. It bounds calls,
requires matching names/IDs, suppresses identical provider retransmissions,
rejects changed arguments under a reused ID, and consumes results once. It checks
the whole response batch before consuming any ID. This is protocol validation,
not an alternative implementation of room behaviours.

Cancellation runs on provider callback arrival, before queued audio or responses.
Calls are checked again after checkpoint persistence before being offered to a
client. Late results for cancelled calls are discarded. Interruption, transport
failure, close and turn completion seal pending actions. A cancelled operation
already executed by a native client cannot be rolled back by this transport;
the client must retain an honest receipt and inspect before any retry.

Bounds shared by the protocol and reservation:

| Bound | Value |
| --- | --- |
| Unique function calls per connection | 6 |
| Tool call envelope | 32 KiB UTF-8 |
| Tool result envelope | 72 KiB UTF-8 |
| All tool results per connection | 144 KiB UTF-8 |
| User/model exchanges | Existing single-turn limit |
| Socket, audio and video limits | Existing gateway limits |

A tool call ends queued microphone tail input, as a spoken model answer already
does; it does not close the socket needed to return the result and hear the answer.
No browser-side action dispatcher is installed by this checkpoint.

## Accounting and activation gate

A valid tool call is useful provider output even if the later spoken answer fails.
The gateway persists that evidence before delivery. Tool call/result UTF-8 byte
counters survive checkpoint merging and recovery without duplication. They contain
no room object names, geometry, audio, or raw tool arguments. Provider usage remains
the settlement source; transport fallback estimates tool JSON as text at four
bytes per token and does not claim exact tokenization or unobserved context reuse.
Missing modality detail never labels tool text as audio.

Tool-enabled reservations include additional retained context and bounded JSON
payloads; the operator spend admission also covers tool continuations. The initial
reservation deliberately uses one token per payload byte and can be much larger
than actual usage. Unused reservation credit is released by existing settlement.
Ordinary Live session reservations and legacy-token policy remain unchanged. Before
activation, measure typical room observations and narrow unnecessary payloads so
ordinary conversation does not require this larger reserve.

**Provider accounting acceptance remains open.** The current usage accumulator
retains the maximum snapshot within one provider turn. The public Live reference
lists prompt, response and tool-use counts but does not establish, by itself, how
the configured model reports all intermediate tool continuations. Capture a real
multi-tool trace, compare it with provider usage, and adjust aggregation if needed
before enabling customer billing. No provider smoke key is present in the current
process; no credentials were fetched from browser storage or project secrets.

## Remaining implementation and acceptance

1. Add the Core SDK room tool session: acquire a live native lease, mint observation
   tokens tied to an exact observed state, parse commands, and call the same
   `RoomAgentLease.execute`. Keep per-object/rule revisions and bounded receipts.
   Never let the model choose or manufacture an observation token.
2. Add request-owned cancellation to the native bridge lease. Cancelling a pending
   Live request may invalidate that request; it must not cancel an unrelated manual
   edit. Completed effects need durable user-visible receipts even if audio fails.
3. Wire the Live controller through an injected runtime port. Supply tools only
   with an active room capability and reviewed speech intent policy. Interruptions,
   Stop, suspension and transport errors must abort pending actions immediately,
   before asynchronous audio flushing. Tool execution must not block cancellation
   behind the provider message queue.
4. Test the same client adapter through BYOK and the managed gateway. Exercise real
   provider function declaration acceptance, observe/action/result roundtrips,
   cancellation, multiple continuations, disconnect and usage reporting. Preserve
   the existing tutor speech/translation format. No automatic action replay after
   ambiguous failure, and no gateway-to-legacy-token fallback.
5. Ship the gateway first, then the matching issuer/client capability negotiation.
   Keep the environment switch false until provider/accounting acceptance passes.
   Verify ordinary Live without tools, then Quest microphone, native receipts,
   permissions, interruption and latency. Only then enable the feature for users.

## Verification

- 128 shared protocol, billing, pricing, client and gateway checks pass. They cover malformed,
  oversized, repeated, cancelled and late messages, and recovery/usage behaviour.
- The gateway and Functions compile with Node 22.23.3; all 25 Functions unit checks
  pass, including existing CORS, pricing, managed-policy and prompt contracts.
- All 65 prompt checks, root TypeScript, core boundaries and prompt ownership pass.
- This is simulated provider/transport coverage, not a live model test or native
  headset acceptance. No new APK or device evidence is implied.

Primary protocol references checked 2026-09-26:
[Live tool use](https://ai.google.dev/gemini-api/docs/live-api/tools) and
[Live WebSocket messages and usage metadata](https://ai.google.dev/api/live).
The implemented function response shape includes the original call ID/name;
async function behaviour is deliberately outside this first contract.
