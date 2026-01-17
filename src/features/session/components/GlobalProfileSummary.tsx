import React, { useEffect } from 'react';
import { ChatMessage } from '../types';
import { getGlobalProfileDB } from '../services/globalProfile';

interface GlobalProfileSummaryProps {
  t: (key: string, replacements?: any) => string;
  messages: ChatMessage[];
}

const GlobalProfileSummary: React.FC<GlobalProfileSummaryProps> = ({ t, messages }) => {
  const [summary, setSummary] = React.useState<string>('Loading profile...');
  useEffect(() => {
    const fetchAndSummarize = async () => {
      try {
        const gp = await getGlobalProfileDB();
        const txt = gp?.text?.trim();
        setSummary(txt && txt.length > 0 ? txt : 'No profile yet.');
      } catch {
        setSummary('No profile yet.');
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
      className="text-[11px] text-slate-700 whitespace-nowrap overflow-x-auto overflow-y-hidden flex-1 min-w-0 no-scrollbar"
      style={{ touchAction: 'pan-x', WebkitOverflowScrolling: 'touch' as any, msOverflowStyle: 'none' as any, scrollbarWidth: 'none' as any }}
      title={summary}
      tabIndex={0}
      aria-label="Global profile summary"
    >
      {summary}
    </div>
  );
};

export default GlobalProfileSummary;
