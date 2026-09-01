// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import ReactDOM from 'react-dom/client';
import DeleteAccountPage from './DeleteAccountPage';
import '../app/index.css';

const rootElement = document.getElementById('delete-account-root');

if (!rootElement) {
  throw new Error('Could not find delete-account-root element.');
}

ReactDOM.createRoot(rootElement).render(
  <React.StrictMode>
    <DeleteAccountPage />
  </React.StrictMode>
);
