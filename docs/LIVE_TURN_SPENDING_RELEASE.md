# Live turn and spending protection

The gateway ends one input turn after at most 60 seconds of wall time or decoded
audio, keeps the reply connection open, and ignores further input. It may end
earlier to leave reply time inside the existing session deadline. App, STT, and
headless clients stop forwarding when notified. Manual activity sends only
`activityEnd`; automatic VAD uses `audioStreamEnd`.

The gateway cancels queued pacing immediately on disconnect/deadline. Only
forwarded media enters fallback accounting. No useful output still returns the
customer's reserved credits.

## Owner-selected spending allowance

`MANAGED_DAILY_SPEND_LIMIT_USD` defaults to the owner's selected USD 100. A single
Firestore transaction admits each reservation against both the user's prepaid
balance and the shared UTC-day allowance. All backend instances and users share
`managedSpendBudgets/YYYY-MM-DD`. Invalid configuration or accounting fails
closed. The allowance resets at 00:00 UTC. It does not interrupt admitted replies.

Admission consumes conservative estimated USD permanently for that day, even
after refunds, failures, and settlements. This prevents input-only and failed
requests from recycling the allowance. It can stop normal service well before
USD 100 of actual provider spend; monitor the budget document when diagnosing
the daily-allowance error. Changing the limit is an owner/operator deployment.
Do not delete the current day record to bypass the guard.

Generation now reserves the full reviewed model output ceiling, including the
most expensive output modality and applicable long-context input/output rates.
Unused customer credits are returned at normal settlement. Tiny balances may
therefore be unable to start a request. The provider limit is pinned at its
published maximum, so this does not shorten answers below the model's capacity.
Unknown generation models/config fields and priority/routing overrides are
rejected until reviewed and priced.

Reviewed ceilings: [Flash 3.8](https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash)
and [Flash-Lite 3.5](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite)
65,536 output tokens; [Flash Image 2.5](https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash-image)
32,768 output tokens. Recheck pricing/model limits before changing the allowlist.

This is an application admission guard, not a verified Google billing cap.
Provider pricing changes, Search query estimates, infrastructure costs, and
requests made outside this backend are not a guaranteed USD 100 invoice ceiling.
Google Search still reserves the configured query estimate because the provider
does not expose an enforceable query count limit in this SDK. Configure the
provider project spend cap as an independent backstop. Known partial-generation
failure refunds remain a subsidy, now bounded by the daily admission allowance.

## Validation and rollout

`npm run smoke:managed-live-turn-limit` offers 65 seconds of real paced speech
without a client end signal, requires a handoff around 60 seconds and a completed
audio/text reply, and reconciles one usage row with one charge and zero remaining
reservation. It uses the regular headless managed auth/App Check flow. No
microphone hardware or Android playback is exercised by this test.

The first staging run handed off at 59.968 seconds, forwarded exactly 60 seconds,
returned "I heard you", charged 2 credits (provider estimate USD 0.001622), and
left no reserved credits. Additional release evidence is recorded on the PR.

Deploy Functions before the gateway/client so all new requests enter the shared
allowance. The annotation is compatible with installed gateway clients. Android
candidate is version 2.6.6 (80); build/sync production assets before creating its
signed AAB. Merge and Play upload are separate operator actions.
