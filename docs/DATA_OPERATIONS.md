# Data operations and release gates

## Verified evidence (2026-09-05)

- Production PITR clone of snapshot `2026-09-04T21:28:00Z` completed successfully.
  All eight financial/account collection comparisons matched, including nonempty
  account and ledger data. The isolated drill database was deleted afterward.
  [Counts and hashes](evidence/restore-drill-20260905.json).
- [Staging cost baseline](evidence/staging-turn-cost-20260905.json) measured
  74 completed Live turns, USD 0.601906 in Live provider estimates, 222,131 default
  database document reads, and 2,932.716 gateway billable instance seconds in the
  specified window. These are workload-wide observations, not production prices.
- Recovery starvation and tied-timestamp ledger pagination passed real Firestore
  emulator tests. Root tests, Functions units, gateway tests, lint/build and
  release configuration checks passed before staging verification.
- Updated staging backend passed a real no-output Live canary with zero charge
  and zero stranded reservation. A completed headless Live turn returned `Play`,
  passed realtime pacing/playback/handoff checks, billed one credit (provider
  estimate USD 0.000486), and left zero reserved credits.
- The owner confirmed the browser camera permission fix and physical STT trailing
  words/single-send, speaker playback, and repeated-turn checks passed on staging.
- Runtime candidate `38c44bc` was promoted to production Functions, gateway and
  both hosting surfaces. `chatwithmaestro.com` uses GitHub Pages (`gh-pages`);
  `chatwithmaestro.web.app` uses Firebase Hosting. Both were verified against
  their corresponding build asset hashes. The gateway revision
  `maestrotutor-live-gateway-release-38c44bc` serves 100% of traffic.
- A production browser request returned `OK!`, played speech, and completed
  suggestions. Its existing chat rebuilt media context; total test spend was
  49 credits over 15 operations. Account spend, usage charges and billing charges
  matched exactly, with zero reserved credits afterward.
  [Reconciliation evidence](evidence/production-billing-20260905.json).
- An operator-token headless production canary was unavailable because the
  operator lacks IAM `signBlob`. No new permissions or App Check bypasses were
  added; the existing authenticated browser flow supplied production evidence.
- [Production baseline report](evidence/production-turn-cost-20260905.json)
  provides the same measured units as the staging report. These windows predate
  the query fix and must not be presented as its post-release performance.

## Protection policy

Production `chatwithmaestro/(default)` has deletion protection and PITR enabled.
Daily backups retain 14 days; Sunday backups retain 14 weeks. Staging
`chatwithmaestro-staging/(default)` has deletion protection, PITR, and daily
backups retained for 7 days. These controls were applied on 2026-09-05 (Helsinki).
PITR history grows from enablement; enabling it does not create seven days of past history.

Check actual state before each production promotion:

```powershell
gcloud firestore databases describe --database '(default)' --project chatwithmaestro
gcloud firestore backups schedules list --database '(default)' --project chatwithmaestro
gcloud firestore backups list --project chatwithmaestro
```

Do not equate a configured schedule with a successfully created backup. Inspect
backup state and age. Alert on failed backups or a newest daily backup older than
48 hours. Check this as part of the operating routine until automated alerting is
installed. A restoration rehearsal must compare data, not just database creation.

Restore into a new isolated `restore-drill-...` database, never over production.
For PITR, use Firestore `projects.databases.clone` with the source `(default)`
and a minute-aligned snapshot time within its available retention window. For
scheduled backups, use `gcloud firestore databases restore` with the verified
backup resource. Wait for the operation to finish before reading the database.
Then run:

```powershell
$env:GOOGLE_OAUTH_ACCESS_TOKEN = gcloud auth print-access-token
node functions/scripts/verify-firestore-restore.mjs chatwithmaestro restore-drill-ID SNAPSHOT_ISO
Remove-Item Env:GOOGLE_OAUTH_ACCESS_TOKEN
```

The verifier compares financial collections against the exact source snapshot,
including balances, purchase claims, usage and billing ledgers, and reservations.
It emits counts/hashes only. Also verify indexes and deploy the versioned rules
and TTL policies before using a restored database as an application database.
Backups do not include TTL policies. Firebase Authentication and provider files
are separate services and are not restored by Firestore recovery.

Delete the isolated drill database after evidence is saved, explicitly targeting
that database and disabling its inherited deletion protection first. Never disable
protection on `(default)` as part of the rehearsal. Rehearse quarterly and after
material schema or billing changes.

## Regional decision

Verified production Firestore is `us-central1`; API and gateway are
`europe-west1`. Staging is already colocated in `europe-west1`.
Production Auth inventory on 2026-09-05 contained one user, the owner; staging
contained two users. Inventory is time-sensitive and must be checked again
immediately before a reset. No data reset is needed for query/pagination changes.

The target for future production data placement is `europe-west1`, matching the
current compute location and staging. This release does not silently replace the
production database: preserving a working billing system takes precedence over
an unmeasured latency optimization. Measure database-dependent endpoint latency
and traffic first. A region change requires a separate cutover with admission
paused, reservations drained, Auth inventory rechecked, and either a verified
export/import or the owner's still-applicable test-only reset authorization.
Deploy indexes/rules/TTL/protection in the destination, validate billing and
webhooks there, switch configuration, then verify clients and scheduled jobs.
Retain a rollback source until verification is complete. Firestore location
cannot be changed in place. Merely changing a Functions region is not a data move.

## Usage and cost reporting

```powershell
$env:GOOGLE_OAUTH_ACCESS_TOKEN = gcloud auth print-access-token
node functions/scripts/measure-turn-cost.mjs chatwithmaestro-staging START_ISO END_ISO
Remove-Item Env:GOOGLE_OAUTH_ACCESS_TOKEN
```

Use complete UTC windows after Monitoring ingestion has settled. The report
paginates usage records and Monitoring results, reports provider USD estimates,
completed managed Live turns, Firestore document operations, gateway billable
instance time, CPU/memory allocation, and sent/received bytes. Infrastructure
per-turn values allocate window totals across completed managed Live turns;
they include idle/failure overhead, and database operations include other app
features. They are not causal per-request measurements or invoice amounts.
No samples or no completed turns produce null, not a zero-cost claim.

Use Cloud Billing export for actual SKU charges, free allowances, regional egress,
storage/backups, and costs from other services. Reconcile that with provider
estimates before changing credit pricing. Never debit users for an inferred
infrastructure allocation. Report incomplete Live operations separately.

Ledger APIs accept `after=<previous nextCursor>` and return `nextCursor` or null.
The headless `account.ledgers` command accepts independent `usageAfter` and
`billingAfter` cursors, using the corresponding previous response's nextCursor.
Cursors are scoped to the authenticated user's collection. Deleted/invalid
cursors return 400; refresh from page one. Document ID breaks timestamp ties.
Do not add offsets or load an entire growing ledger into the UI.

Recovery queries filter eligible statuses before limiting by deadline. Finished
records retained for audit cannot consume the recovery batch. Deploy and wait
for the new indexes before promoting Functions. Query-free ticket config and
session checkpoint maps are exempt from indexing.

## Promotion gates

1. Root regression tests, Functions unit/emulator tests, gateway tests, lint,
   builds, and release configuration checks pass for the candidate commit.
2. Deploy indexes and wait for readiness. Deploy matching Functions, gateway,
   and staging frontend. Verify frontend asset hashes and backend health.
3. Run real managed headless billing/no-output and completed-turn checks.
4. Check browser microphone permission, STT trailing words and single-send,
   camera-off, camera permission, complete speaker playback and repeated turns.
   Simulated audio tests do not prove physical speaker/microphone behavior.
5. Record the tested commit and evidence. Promote that exact commit to production
   Functions/gateway, Firebase Hosting, and GitHub Pages (`npm run deploy`). Wait
   for the Pages build and verify the custom domain separately. Repeat
   health/asset and billing smoke checks, and inspect error/recovery logs.
6. Only then mark the PR merge-ready. Changes after testing restart affected gates.

References: [backups](https://firebase.google.com/docs/firestore/backups),
[PITR](https://firebase.google.com/docs/firestore/use-pitr),
[best practices](https://firebase.google.com/docs/firestore/best-practices),
[billing](https://firebase.google.com/docs/firestore/pricing).
