// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/** Shared vocabulary for the embed lifecycle. */

// EmbedBox lives in core/types because it is persisted on ChatMessage; it is
// re-exported here so embed code has one import site for the whole vocabulary.
export type { EmbedBox } from '../../../core/types';

/** What kind of heavy content occupies a reserved box. */
export type EmbedKind = 'mini-game' | 'pdf' | 'office' | 'artifact';

/**
 * placeholder — reserved box only. Zero documents, zero bitmaps. The resting state.
 * live        — the real iframe / rendered pages. Budgeted, see EMBED_BUDGETS.
 * frozen      — a poster bitmap of the last live frame, so scrolling back does
 *               not look like the content vanished. Budgeted separately.
 */
export type EmbedPhase = 'placeholder' | 'live' | 'frozen';
