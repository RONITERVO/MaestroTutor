// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState } from 'react';
import type { QuestPairingView } from '../../../services/auth/questPairingClient';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
export default function QuestAccountPairing({ state, onCancel }: { state: QuestPairingView; onCancel(): void }) {
  const { t } = useAppTranslations();
  const [now, setNow] = useState(Date.now);
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []);
  if (state.phase === 'idle') return null;
  const remaining = Math.max(0, Math.ceil(((state.expiresAt || now) - now) / 1000));
  const waiting = state.phase === 'waiting' || state.phase === 'paused';
  return <section className="space-y-3 border border-gate-accent/40 p-3" aria-label={t('questLink.title')}>
    <h3 className="font-semibold">{t('questLink.title')}</h3>
    <p role="status">{t(`questLink.${state.phase}`)}</p>
    {state.code && waiting && <>
      <p>{t('questLink.instructions')}</p>
      <p className="select-text break-all text-center font-mono text-2xl font-bold tracking-wide" aria-label={t('questLink.codeLabel')}>{state.code}</p>
      <a href={state.verificationUrl} rel="noreferrer" className="block break-all text-gate-accent underline">{state.verificationUrl}</a>
      <p className="text-xs text-gate-muted-text">{t('questLink.expires').replace('{time}', `${Math.floor(remaining / 60)}:${String(remaining % 60).padStart(2, '0')}`)}</p>
      <p className="text-xs text-gate-muted-text">{t('questLink.return')}</p>
    </>}
    <button type="button" onClick={onCancel} disabled={state.phase === 'cancelling'} className="px-3 py-2 text-gate-text hover:bg-gate-input-bg disabled:opacity-60 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin">{t('questLink.cancel')}</button>
  </section>;
}
