// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * The pricing registry lives in `shared/` because the managed backend bills
 * against exactly the same table. Re-exported here so existing app imports keep
 * working and there is still one obvious place to look from inside `src/`.
 */
export * from '../../../shared/pricing/registry';
