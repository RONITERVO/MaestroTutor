// Read-only operational report. Pass an OAuth token through the environment,
// never a command-line argument. No account IDs or chat content are emitted.
const [project, start, end] = process.argv.slice(2);
const token = process.env.GOOGLE_OAUTH_ACCESS_TOKEN;
if (!project || !token || !Number.isFinite(Date.parse(start)) || !Number.isFinite(Date.parse(end)) || Date.parse(end) <= Date.parse(start)) {
  throw new Error('Usage: GOOGLE_OAUTH_ACCESS_TOKEN=<token> node measure-turn-cost.mjs PROJECT START_ISO END_ISO');
}
const headers = { Authorization: `Bearer ${token}`, 'x-goog-user-project': project, 'Content-Type': 'application/json' };
const request = async (url, body) => {
  const response = await fetch(url, { headers, ...(body ? { method: 'POST', body: JSON.stringify(body) } : {}) });
  if (!response.ok) throw new Error(`Cloud API ${response.status}: ${await response.text()}`);
  return response.json();
};
const value = field => Number(field?.integerValue ?? field?.int64Value ?? field?.doubleValue ?? 0);
const model = { operations: 0, completedLiveTurns: 0, providerUsd: 0, liveProviderUsd: 0, incompleteLiveOperations: 0 };
let cursor;
do {
  const rows = await request(`https://firestore.googleapis.com/v1/projects/${project}/databases/(default)/documents:runQuery`, {
    structuredQuery: {
      from: [{ collectionId: 'usageEvents', allDescendants: true }],
      select: { fields: ['createdAt', 'operation', 'billedUsd', 'metadata.providerTurnCompleteCount'].map(fieldPath => ({ fieldPath })) },
      where: { compositeFilter: { op: 'AND', filters: [
        { fieldFilter: { field: { fieldPath: 'createdAt' }, op: 'GREATER_THAN_OR_EQUAL', value: { integerValue: String(Date.parse(start)) } } },
        { fieldFilter: { field: { fieldPath: 'createdAt' }, op: 'LESS_THAN', value: { integerValue: String(Date.parse(end)) } } },
      ] } },
      orderBy: [{ field: { fieldPath: 'createdAt' }, direction: 'ASCENDING' }, { field: { fieldPath: '__name__' }, direction: 'ASCENDING' }],
      limit: 500,
      ...(cursor ? { startAt: { values: cursor, before: false } } : {}),
    },
  });
  const docs = rows.filter(row => row.document).map(row => row.document);
  for (const doc of docs) {
    const fields = doc.fields;
    model.operations++;
    model.providerUsd += value(fields.billedUsd);
    if (fields.operation?.stringValue === 'liveGateway') {
      const turns = value(fields.metadata?.mapValue?.fields?.providerTurnCompleteCount);
      model.completedLiveTurns += turns;
      model.liveProviderUsd += value(fields.billedUsd);
      if (!turns) model.incompleteLiveOperations++;
    }
  }
  const last = docs.at(-1);
  cursor = docs.length === 500 ? [last.fields.createdAt, { referenceValue: last.name }] : null;
} while (cursor);

const metrics = {
  firestoreDocumentReads: 'firestore.googleapis.com/document/read_ops_count',
  firestoreDocumentWrites: 'firestore.googleapis.com/document/write_ops_count',
  firestoreDocumentDeletes: 'firestore.googleapis.com/document/delete_ops_count',
  gatewayBillableInstanceSeconds: 'run.googleapis.com/container/billable_instance_time',
  gatewayCpuSeconds: 'run.googleapis.com/container/cpu/allocation_time',
  gatewayMemoryGiBSeconds: 'run.googleapis.com/container/memory/allocation_time',
  gatewayReceivedBytes: 'run.googleapis.com/container/network/received_bytes_count',
  gatewaySentBytes: 'run.googleapis.com/container/network/sent_bytes_count',
};
const infrastructure = {};
for (const [name, metric] of Object.entries(metrics)) {
  let nextPageToken;
  let total = 0;
  let samples = 0;
  do {
    const filter = `metric.type="${metric}"` + (name.startsWith('gateway')
      ? ' AND resource.labels.service_name="maestrotutor-live-gateway"'
      : ' AND resource.labels.database_id="(default)"');
    const query = new URLSearchParams({ filter, 'interval.startTime': start, 'interval.endTime': end, view: 'FULL', pageSize: '1000' });
    if (nextPageToken) query.set('pageToken', nextPageToken);
    const response = await request(`https://monitoring.googleapis.com/v3/projects/${project}/timeSeries?${query}`);
    for (const series of response.timeSeries || []) {
      for (const point of series.points || []) { total += value(point.value); samples++; }
    }
    nextPageToken = response.nextPageToken;
  } while (nextPageToken);
  infrastructure[name] = {
    total: samples ? total : null,
    samples,
    allocatedPerCompletedLiveTurn: samples && model.completedLiveTurns ? total / model.completedLiveTurns : null,
  };
}
console.log(JSON.stringify({
  project, start, end, model,
  liveProviderUsdPerCompletedTurn: model.completedLiveTurns ? model.liveProviderUsd / model.completedLiveTurns : null,
  infrastructure,
  interpretation: [
    'Provider USD is the application pricing estimate, not an invoice reconciliation.',
    'Infrastructure allocation includes idle, failed and other work in the window. Firestore covers the entire default database.',
    'Null means no available samples or no completed turns; it never means free.',
    'Network bytes are measured traffic, not billable egress. Apply region/destination SKUs using Cloud Billing export.',
    'Run after monitoring ingestion has settled. This read-only report itself consumes Firestore reads.',
  ],
}, null, 2));
