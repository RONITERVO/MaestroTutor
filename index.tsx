import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './src/app/App';
import './src/app/index.css';
import { QuestBookSurface } from './src/platform/quest/QuestBookSurface';

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error("Could not find root element to mount to");
}

const root = ReactDOM.createRoot(rootElement);
// A presentation selector only; never used as authentication or native authority.
const bookSurface = new URLSearchParams(window.location.search).get('surface') === 'quest-book';
root.render(
  <React.StrictMode>
    {bookSurface ? <QuestBookSurface><App /></QuestBookSurface> : <App />}
  </React.StrictMode>
);
