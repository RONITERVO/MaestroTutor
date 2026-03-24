// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect } from 'react';
import { ChatMessage } from '../../../core/types';
import { getGlobalProfileDB } from '../services/globalProfile';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';

interface GlobalProfileSummaryProps {
  messages: ChatMessage[];
}

const GlobalProfileSummary: React.FC<GlobalProfileSummaryProps> = ({ messages }) => {
  const { t } = useAppTranslations();
  const [summary, setSummary] = React.useState<string>('');
  useEffect(() => {
    const fetchAndSummarize = async () => {
      try {
        const gp = await getGlobalProfileDB();
        const txt = gp?.text?.trim();
        setSummary(txt && txt.length > 0 ? txt : t('globalProfile.noProfile') || 'No profile yet.');
      } catch {
        setSummary(t('globalProfile.noProfile') || 'No profile yet.');
      }
    };
    fetchAndSummarize();
    const handler = () => { fetchAndSummarize(); };
    try {
      (window as any).addEventListener('globalProfileUpdated', handler);
    } catch {}
    return () => {
      try { (window as any).removeEventListener('globalProfileUpdated', handler); } catch {}
    };
  }, [messages]);
  
  return (
    <div
      className="text-[11px] text-ctrl-muted-text whitespace-nowrap overflow-x-auto overflow-y-hidden flex-1 min-w-0 no-scrollbar"
      style={{ touchAction: 'pan-x', WebkitOverflowScrolling: 'touch' as any, msOverflowStyle: 'none' as any, scrollbarWidth: 'none' as any }}
      title={summary || t('globalProfile.loading') || 'Loading profile...'}
      tabIndex={0}
      aria-label={t('globalProfile.ariaLabel') || 'Global profile summary'}
    >
      {summary || t('globalProfile.loading') || 'Loading profile...'}
    </div>
  );
};

export default GlobalProfileSummary;

