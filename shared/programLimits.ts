// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import manifest from './generated/behaviourCatalog.json';
/** Generated native limit shared by validation, module linking and imports. */
export const programResourceLimit=manifest.limits.programResources;
/** One-off actions may address the full saved world; program declarations stay bounded separately. */
export const invocationResourceLimit=manifest.limits.invocationResources;
export const ownershipClaimLimit=manifest.limits.ownershipClaims;

export const programRecordFieldLimit=manifest.limits.programRecordFields;
