// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useSyncExternalStore } from 'react';
import { sessionActivity } from './sessionActivity';
export const useSessionActive = () => useSyncExternalStore(sessionActivity.subscribe, sessionActivity.isActive, sessionActivity.isActive);
