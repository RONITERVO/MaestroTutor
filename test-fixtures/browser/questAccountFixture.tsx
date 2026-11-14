// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Presentation-only synthetic accounts; no backend/authentication requests.
import { createRoot } from 'react-dom/client';
import { useState } from 'react';
import QuestLinkPage from '../../src/quest-link/QuestLinkPage';
import ManagedAccountModal from '../../src/features/session/components/ManagedAccountModal';
import type { QuestPairingView } from '../../src/services/auth/questPairingClient';
import '../../src/app/index.css';
if (!import.meta.env.DEV) throw new Error('Quest fixture is development-only.');
const user = { firebaseIdToken: 'synthetic', refreshToken: null, expiresAt: null, user: { id: 'fixture', email: 'learner@example.test', displayName: 'Learner', photoUrl: null } };
const params = new URLSearchParams(location.search);
const noop = () => {};
function Book() {
  const [state, setState] = useState<QuestPairingView>({ phase: 'waiting', code: 'ABCDE-FGHJK', verificationUrl: 'https://chatwithmaestro.com/quest-link.html', expiresAt: Date.now() + 300000 });
  return <ManagedAccountModal isOpen session={null} questPairing={state} onCancelQuestPairing={() => setState({ phase: 'idle' })}
    statusMessage={null} errorMessage={null} isSigningIn={state.phase !== 'idle'} isRefreshing={false} isPurchasing={false} isDeletingAccount={false}
    isDeleteConfirmOpen={false} purchasingAvailable={false} deleteConfirmationText="" onClose={() => setState({ phase: 'idle' })}
    onSignIn={noop} onSignOut={noop} onRefresh={noop} onPurchase={noop} onOpenActivity={noop} onOpenDeleteConfirm={noop} onCancelDelete={noop}
    onDeleteConfirmationTextChange={noop} onDeleteAccount={noop} />;
}
createRoot(document.getElementById('root')!).render(params.has('book') ? <Book /> : <QuestLinkPage adapter={{ available: () => !params.has('unconfigured'),
  identity: async () => params.has('signedout') ? null : user, signIn: async () => user, signOut: async () => {},
  approve: async (code, expected) => { if (code !== 'ABCDEFGHJK' || expected !== user.user.id) throw new Error('Invalid fixture approval'); } }} />);
