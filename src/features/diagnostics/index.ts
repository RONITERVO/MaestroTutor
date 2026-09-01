// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Diagnostics Feature - Public API
 * 
 * This is the single entry point for diagnostics functionality.
 * External code should only import from this file.
 * 
 * Owned Store Slice: diagnosticsSlice
 */

// Components
export { default as DebugLogPanel } from './components/DebugLogPanel';

// Services
export { debugLogService } from '../../core-sdk/diagnostics';
export type { LogEntry } from '../../core-sdk/diagnostics';
