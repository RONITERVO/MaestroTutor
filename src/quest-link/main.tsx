// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import QuestLinkPage from './QuestLinkPage';
import '../app/index.css';
const root = document.getElementById('quest-link-root');
if (!root) throw new Error('Missing account-link page root.');
createRoot(root).render(<StrictMode><QuestLinkPage /></StrictMode>);
