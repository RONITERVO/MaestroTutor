// Development-only visual fixture. Reads synthetic Unity test evidence; no AI or model service.
import './questBookFixture';
import { parseLibraryState, type LibraryState } from '../../src/platform/quest/libraryBookBridge';
if (!import.meta.env.DEV) throw new Error('Library fixture is development-only.');
const parameters = new URLSearchParams(window.location.search);
const evidence = parameters.get('removed') === '1' ? 'removed-book-state.json' : parameters.get('maintenance') === '1' ? 'maintenance-book-state.json' : parameters.get('protected') === '1' ? 'protected-book-state.json' : parameters.get('activity') === '1' ? 'activity-book-state.json' : 'library-book-state.json';
const response = await fetch('/.quest-evidence/art/' + evidence);
const initial = parseLibraryState(await response.json());
if (!initial) throw new Error('Run Verify-Quest.ps1 -RenderImports to generate native library test evidence.');
let current: LibraryState = { ...initial, revision: 1, ack: 0, offset: 0, status: 'Choose an animation to preview or assign.' };
let entries = current.entries;
function applyFilters() {
  const matches = entries.filter(entry => (!!entry.archived === !!current.archivedOnly) && (!current.favouritesOnly || entry.favourite) && (!current.compatibleOnly || entry.compatible) && (!entry.shortClip || current.includeShort) && (entry.name + ' ' + entry.tags.join(' ')).toLowerCase().includes(current.query.toLowerCase()));
  current.total = matches.length; current.offset = 0; current.entries = matches;
}
// This fixture exercises presentation only. Native search/mutation/ownership are
// covered by LibraryRuleAndWalkTests and the web request tests.
const timer = window.setInterval(() => {
  const bridge = window.maestroBook; if (!bridge) return;
  const request = bridge.snapshot().libraryRequest;
  if (request?.session === current.session && request.sequence > current.ack) {
    current = { ...current, revision: current.revision + 1, ack: request.sequence };
    const selected = entries.find(entry => entry.id === request.motionId);
    if (request.action === 'close') current.visible = false;
    else if (request.action === 'query') {
      current.query = request.query ?? ''; current.compatibleOnly = !!request.compatibleOnly; current.favouritesOnly = !!request.favouritesOnly; current.includeShort = !!request.includeShort; current.archivedOnly = !!request.archivedOnly;
      applyFilters();
    } else if (selected && request.action === 'select') current.selected = selected;
    else if (selected && request.action === 'save') {
      current.selected = { ...selected, name: request.name!, tags: request.tags!, favourite: !!request.favourite };
      entries = entries.map(entry => entry.id === selected.id ? current.selected! : entry); applyFilters();
      current.status = 'Motion details saved (visual fixture).';
    } else current.status = 'Visual fixture only. Playback and assignment are verified in Unity.';
  }
  bridge.libraryState(current);
}, 100);
window.addEventListener('pagehide', () => window.clearInterval(timer), { once: true });
