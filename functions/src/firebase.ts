// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { getApps, initializeApp } from 'firebase-admin/app';
import { getAppCheck } from 'firebase-admin/app-check';
import { getAuth } from 'firebase-admin/auth';
import { getFirestore } from 'firebase-admin/firestore';

const adminApp = getApps()[0] || initializeApp();

export const adminAuth = getAuth(adminApp);
export const adminDb = getFirestore(adminApp);
export const adminAppCheck = getAppCheck(adminApp);

adminDb.settings({ ignoreUndefinedProperties: true });
