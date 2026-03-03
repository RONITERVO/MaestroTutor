// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useEffect } from 'react';
import { debugLogService, LogEntry } from '../services/debugLogService';
import { IconXMark, IconTrash } from '../../../shared/ui/Icons';

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
    <div className="fixed inset-y-0 right-0 w-full sm:w-[480px] bg-debug-panel-bg shadow-2xl z-[100] flex flex-col border-l border-ui-border font-mono text-sm">
      {/* Header */}
      <div
        className="flex items-center justify-between px-4 pb-3 bg-debug-panel-bg/90 border-b border-ui-border"
        style={{ paddingTop: 'calc(0.75rem + env(safe-area-inset-top))' }}
      >
        <h2 className="text-debug-panel-text font-semibold flex items-center gap-2">
          <span className="text-green-400">➜</span> Traffic Log
        </h2>
        <div className="flex items-center gap-2">
          <button
            onClick={handleClear}
            className="p-1.5 text-faded-label hover:text-delete-msg-btn hover:bg-debug-panel-bg/70 rounded transition-colors"
            title="Clear logs"
          >
            <IconTrash className="w-4 h-4" />
          </button>
          <button
            onClick={onClose}
            className="p-1.5 text-faded-label hover:text-debug-panel-text hover:bg-debug-panel-bg/70 rounded transition-colors"
            title="Close"
          >
            <IconXMark className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Log List */}
      <div className="flex-1 overflow-y-auto p-2 space-y-2 bg-debug-panel-bg">
        {logs.length === 0 && (
          <div className="text-faded-label text-center py-10 italic">
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
              className={`rounded border ${isError ? 'border-red-800 bg-red-900/10' : 'border-ui-border bg-debug-panel-bg/70'} overflow-hidden transition-colors`}
            >
              <div
                className="px-3 py-2 cursor-pointer flex items-center justify-between hover:bg-white/5 select-none"
                onClick={() => toggleExpand(log.id)}
              >
                <div className="flex items-center gap-2 overflow-hidden">
                  <span className={`text-xs ${isError ? 'text-red-400' : 'text-faded-label'}`}>{time}</span>
                  <span className={`font-semibold truncate ${isError ? 'text-red-300' : 'text-teal-accent'}`}>{log.type}</span>
                  <span className="text-xs text-faded-label truncate hidden sm:inline-block">- {log.model}</span>
                </div>
                <div className="flex items-center gap-2 text-xs">
                  <span className={`${isError ? 'text-red-400' : 'text-green-400'}`}>{duration}</span>
                  <span className="text-faded-label">{isExpanded ? '▲' : '▼'}</span>
                </div>
              </div>

              {isExpanded && (
                <div className="border-t border-ui-border/50">
                  {/* Request Section */}
                  <div className="bg-black/20 p-2">
                    <div className="text-xs text-faded-label uppercase font-bold mb-1 px-1">Request Payload</div>
                    <pre className="text-xs text-debug-panel-text/70 overflow-x-auto whitespace-pre-wrap break-all bg-black/20 p-2 rounded max-h-60 overflow-y-auto custom-scrollbar">
                      {JSON.stringify(log.request, null, 2)}
                    </pre>
                  </div>

                  {/* Response Section */}
                  {(log.response || log.error) && (
                    <div className="bg-black/10 p-2 border-t border-ui-border/30">
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
