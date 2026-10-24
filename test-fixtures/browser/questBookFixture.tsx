// Development-only presentation fixture. No AI requests or account bypass.
import { createRoot } from 'react-dom/client';
import { QuestBookSurface } from '../../src/platform/quest/QuestBookSurface';
import { ChatInterface } from '../../src/features/chat';
import { useMaestroStore, initialSettings } from '../../src/store';
import '../../src/app/index.css';

if (!import.meta.env.DEV) throw new Error('Quest fixture is development-only.');
// Simulate physical native commands without adding controls to either page.
window.addEventListener('keydown', event => {
  if (!event.altKey) return;
  if (event.key === '1' || event.key === '2') window.maestroBook?.command({ version: 1, type: 'layout.set', layout: event.key === '1' ? 'conversation' : 'practice' });
  else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') window.maestroBook?.command({ version: 1, type: 'history.step', direction: event.key === 'ArrowLeft' ? -1 : 1 });
  else if (event.key === 'b') window.maestroBook?.command({ version: 1, type: 'bookmark.jump' });
  else if (event.key === 'End') window.maestroBook?.command({ version: 1, type: 'history.latest' });
  else return;
  event.preventDefault();
});
const source = `<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"><style>body{margin:0;padding:28px;background:#f8ecd4;color:#342d2b;font:20px Georgia;line-height:1.5}h1{font-size:35px}button{font:inherit;padding:14px 24px;background:#2b8d88;color:white;border:0;border-radius:10px}canvas{width:100%;border-radius:12px}p{margin:20px 0}</style></head><body><p>AT THE CAFÉ / EN EL CAFÉ</p><h1>One small conversation.</h1><canvas id="picture" width="600" height="280"></canvas><p>How would you ask for a cup of coffee?</p><button id="answer">Try the phrase</button><p id="phrase" aria-live="polite"></p><script>const c=document.querySelector('canvas').getContext('2d');c.fillStyle='#e5d5ad';c.fillRect(0,0,600,280);c.fillStyle='#fff0d2';c.beginPath();c.ellipse(300,200,160,30,0,0,Math.PI*2);c.fill();c.fillStyle='#b97957';c.fillRect(215,80,160,120);c.strokeStyle='#342d2b';c.lineWidth=5;c.strokeRect(215,80,160,120);c.beginPath();c.arc(385,133,34,-Math.PI/2,Math.PI/2);c.stroke();document.querySelector('button').onclick=()=>document.querySelector('#phrase').textContent='Un café, por favor.';</script></body></html>`;
const store = useMaestroStore.getState();
useMaestroStore.setState({ settings: { ...initialSettings, selectedLanguagePairId: 'es-en', sendWithSnapshotEnabled: false, imageFocusedModeEnabled: false, historyBookmarkMessageId: 'quest-3' }, isSettingsLoaded: true, needsLanguageSelection: false, isLoadingHistory: false });
store.setMessages(Array.from({ length: 14 }, (_, i) => ({
  id: `quest-${i}`, role: i % 2 ? 'assistant' as const : 'user' as const, timestamp: i + 1,
  text: i % 2 ? 'Let’s try one useful phrase together.' : 'I would like to practise ordering a coffee.',
  ...(i === 13 ? { imageUrl: `data:text/html;base64,${btoa(String.fromCharCode(...new TextEncoder().encode(source)))}`, imageMimeType: 'text/html', attachmentName: 'At the café.html' } : {}),
})));
const noop = () => {};
createRoot(document.getElementById('root')!).render(<QuestBookSurface><div className="flex flex-col min-h-screen"><ChatInterface
  onSendMessage={async () => false} onDeleteMessage={noop} onBookmarkAt={id => store.setSettings(previous => ({ ...previous, historyBookmarkMessageId: id }))}
  onChangeMaxVisibleMessages={noop} bubbleWrapperRefs={{ current: new Map<string, HTMLDivElement>() }}
  onSetAttachedImage={noop} onSttToggle={noop} speakText={noop} stopSpeaking={noop} onToggleSpeakNativeLang={noop} onUserInputActivity={noop}
  onToggleSendWithSnapshot={noop} onToggleUseVisualContextForReengagement={noop} onSuggestionClick={noop} onToggleImageFocusedMode={noop}
  onStartLiveSession={noop} onStopLiveSession={noop} onStopSilentObserver={noop} onToggleSuggestionMode={noop} onCreateSuggestion={async () => {}}
/></div></QuestBookSurface>);
