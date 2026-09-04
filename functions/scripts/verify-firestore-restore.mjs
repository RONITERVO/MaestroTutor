// Compare restored financial records with the exact source PITR snapshot.
// Data stays in memory: output contains only collection names/counts/hashes.
import { createHash } from 'node:crypto';
const [project, restoredDatabase, snapshotTime] = process.argv.slice(2);
const token = process.env.GOOGLE_OAUTH_ACCESS_TOKEN;
if (!project || !restoredDatabase?.startsWith('restore-drill-') || !Number.isFinite(Date.parse(snapshotTime)) || !token) {
  throw new Error('Usage: verify-firestore-restore.mjs PROJECT restore-drill-ID SNAPSHOT_ISO; set GOOGLE_OAUTH_ACCESS_TOKEN');
}
const stable = value => Array.isArray(value) ? value.map(stable) : value && typeof value === 'object'
  ? Object.fromEntries(Object.keys(value).sort().map(key => [key, stable(value[key])])) : value;
const read = async (database, collection, readTime) => {
  const prefix = `projects/${project}/databases/${database}/documents/`;
  const documents = [];
  let cursor;
  do {
    const response = await fetch(`https://firestore.googleapis.com/v1/${prefix.slice(0, -1)}:runQuery`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'x-goog-user-project': project, 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ...(readTime ? { readTime } : {}),
        structuredQuery: {
          from: [{ collectionId: collection, allDescendants: true }],
          orderBy: [{ field: { fieldPath: '__name__' }, direction: 'ASCENDING' }],
          limit: 500,
          ...(cursor ? { startAt: { values: [{ referenceValue: cursor }], before: false } } : {}),
        },
      }),
    });
    if (!response.ok) throw new Error(`Restore verification query failed: ${response.status} ${await response.text()}`);
    const rows = (await response.json()).filter(row => row.document).map(row => row.document);
    documents.push(...rows.map(doc => ({ path: doc.name.slice(prefix.length), fields: doc.fields })));
    cursor = rows.length === 500 ? rows.at(-1).name : null;
  } while (cursor);
  return { count: documents.length, sha256: createHash('sha256').update(JSON.stringify(stable(documents))).digest('hex') };
};
const results = [];
for (const collection of ['users', 'managedAccounts', 'entitlements', 'billingEvents', 'usageEvents', 'reservations', 'purchaseClaims', 'accountDeletionClaims']) {
  const source = await read('(default)', collection, snapshotTime);
  const restored = await read(restoredDatabase, collection);
  results.push({ collection, source, restored, matches: source.sha256 === restored.sha256 });
}
const passed = results.every(result => result.matches) && results.some(result => result.source.count > 0);
console.log(JSON.stringify({ project, restoredDatabase, snapshotTime, passed, results }, null, 2));
if (!passed) process.exitCode = 1;
