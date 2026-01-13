
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface LogEntry {
  id: string;
  timestamp: number;
  type: string;
  model: string;
  request: any;
  response?: any;
  error?: any;
  duration?: number;
}

type LogListener = (logs: LogEntry[]) => void;

class DebugLogService {
  private logs: LogEntry[] = [];
  private listeners: Set<LogListener> = new Set();

  public subscribe(listener: LogListener): () => void {
    this.listeners.add(listener);
    listener([...this.logs]); // Initial emit
    return () => this.listeners.delete(listener);
  }

  private notify() {
    const logsCopy = [...this.logs];
    this.listeners.forEach(l => l(logsCopy));
  }

  public logRequest(type: string, model: string, requestPayload: any) {
    const id = Math.random().toString(36).substring(7);
    const timestamp = Date.now();
    
    // Create a deep copy of the request to prevent mutation issues if the SDK modifies it
    let safeRequest = requestPayload;
    try {
        safeRequest = JSON.parse(JSON.stringify(requestPayload));
    } catch (e) {
        // Fallback for circular structures or non-serializable data
        safeRequest = "[Unserializable Payload]";
    }

    const entry: LogEntry = {
      id,
      timestamp,
      type,
      model,
      request: safeRequest,
    };

    this.logs = [entry, ...this.logs].slice(0, 50); // Keep last 50 logs
    this.notify();

    return {
      complete: (responsePayload: any) => {
        const duration = Date.now() - timestamp;
        this.updateEntry(id, { response: responsePayload, duration });
      },
      error: (errorPayload: any) => {
        const duration = Date.now() - timestamp;
        const safeError = errorPayload instanceof Error ? { message: errorPayload.message, stack: errorPayload.stack, ...errorPayload } : errorPayload;
        this.updateEntry(id, { error: safeError, duration });
      }
    };
  }

  private updateEntry(id: string, updates: Partial<LogEntry>) {
    this.logs = this.logs.map(log => log.id === id ? { ...log, ...updates } : log);
    this.notify();
  }

  public clear() {
    this.logs = [];
    this.notify();
  }
}

export const debugLogService = new DebugLogService();
