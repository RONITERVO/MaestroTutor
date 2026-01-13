
import React, { useState, useEffect, useRef } from 'react';
import { debugLogService, LogEntry } from '../services/debugLogService';
import { IconXMark, IconTrash, IconCheck } from '../../constants';

interface DebugLogPanelProps {
  onClose: () => void;
}

const DebugLogPanel: React.FC<DebugLogPanelProps> = ({ onClose }) => {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  useEffect(() => {
    return debugLogService.subscribe((updatedLogs) => {
      setLogs(updatedLogs);
    });
  }, []);

  const handleClear = () => {
    debugLogService.clear();
  };

  const toggleExpand = (id: string) => {
    setExpandedId(expandedId === id ? null : id);
  };

  return (
    <div className="fixed inset-y-0 right-0 w-full sm:w-[480px] bg-slate-900 shadow-2xl z-[100] flex flex-col border-l border-slate-700 font-mono text-sm">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 bg-slate-800 border-b border-slate-700">
        <h2 className="text-slate-200 font-semibold flex items-center gap-2">
          <span className="text-green-400">➜</span> Traffic Log
        </h2>
        <div className="flex items-center gap-2">
          <button
            onClick={handleClear}
            className="p-1.5 text-slate-400 hover:text-red-400 hover:bg-slate-700 rounded transition-colors"
            title="Clear logs"
          >
            <IconTrash className="w-4 h-4" />
          </button>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-700 rounded transition-colors"
            title="Close"
          >
            <IconXMark className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Log List */}
      <div className="flex-1 overflow-y-auto p-2 space-y-2 bg-slate-900">
        {logs.length === 0 && (
          <div className="text-slate-500 text-center py-10 italic">
            No traffic recorded yet.
          </div>
        )}
        {logs.map((log) => {
          const isExpanded = expandedId === log.id;
          const isError = !!log.error;
          const duration = log.duration ? `${log.duration}ms` : 'pending...';
          const time = new Date(log.timestamp).toLocaleTimeString([], { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' });

          return (
            <div
              key={log.id}
              className={`rounded border ${isError ? 'border-red-800 bg-red-900/10' : 'border-slate-700 bg-slate-800/50'} overflow-hidden transition-colors`}
            >
              <div
                className="px-3 py-2 cursor-pointer flex items-center justify-between hover:bg-white/5 select-none"
                onClick={() => toggleExpand(log.id)}
              >
                <div className="flex items-center gap-2 overflow-hidden">
                  <span className={`text-xs ${isError ? 'text-red-400' : 'text-slate-500'}`}>{time}</span>
                  <span className={`font-semibold truncate ${isError ? 'text-red-300' : 'text-blue-300'}`}>{log.type}</span>
                  <span className="text-xs text-slate-500 truncate hidden sm:inline-block">- {log.model}</span>
                </div>
                <div className="flex items-center gap-2 text-xs">
                  <span className={`${isError ? 'text-red-400' : 'text-green-400'}`}>{duration}</span>
                  <span className="text-slate-600">{isExpanded ? '▲' : '▼'}</span>
                </div>
              </div>

              {isExpanded && (
                <div className="border-t border-slate-700/50">
                  {/* Request Section */}
                  <div className="bg-slate-950/50 p-2">
                    <div className="text-xs text-slate-400 uppercase font-bold mb-1 px-1">Request Payload</div>
                    <pre className="text-xs text-slate-300 overflow-x-auto whitespace-pre-wrap break-all bg-black/20 p-2 rounded max-h-60 overflow-y-auto custom-scrollbar">
                      {JSON.stringify(log.request, null, 2)}
                    </pre>
                  </div>

                  {/* Response Section */}
                  {(log.response || log.error) && (
                    <div className="bg-slate-950/30 p-2 border-t border-slate-800/50">
                      <div className={`text-xs uppercase font-bold mb-1 px-1 ${isError ? 'text-red-400' : 'text-green-400'}`}>
                        {isError ? 'Error' : 'Response'}
                      </div>
                      <pre className={`text-xs overflow-x-auto whitespace-pre-wrap break-all bg-black/20 p-2 rounded max-h-60 overflow-y-auto custom-scrollbar ${isError ? 'text-red-300' : 'text-green-300'}`}>
                        {JSON.stringify(log.error || log.response, null, 2)}
                      </pre>
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default DebugLogPanel;
