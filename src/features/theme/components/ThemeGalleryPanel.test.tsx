// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { THEME_GALLERY_ITEMS } from '../config/themeCatalogue';
import { getThemePreset } from '../config/themePresets';
import ThemeGalleryPanel from './ThemeGalleryPanel';

vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => ({
    t: (key: string) => ({
      'themeGallery.title': 'Theme Gallery',
      'themeGallery.close': 'Close',
      'themeGallery.included': 'Free',
      'themeGallery.includedDescription': 'Included color theme',
      'themeGallery.apply': 'Apply',
      'themeGallery.footerNote': 'Every color theme is included and will remain free.',
    })[key] || key,
  }),
}));

afterEach(cleanup);

describe('ThemeGalleryPanel', () => {
  it('offers every gallery theme locally without purchase controls', () => {
    render(<ThemeGalleryPanel onApplyTheme={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getAllByText('Free')).toHaveLength(THEME_GALLERY_ITEMS.length);
    expect(screen.getAllByText('Included color theme')).toHaveLength(THEME_GALLERY_ITEMS.length);
    expect(screen.getAllByRole('button', { name: 'Apply' })).toHaveLength(THEME_GALLERY_ITEMS.length);
    expect(screen.queryByText(/buy|price|restore|google play/i)).toBeNull();
  });

  it('applies the same preset exposed by the shared theme catalogue', () => {
    const onApplyTheme = vi.fn();
    render(<ThemeGalleryPanel onApplyTheme={onApplyTheme} onClose={vi.fn()} />);

    fireEvent.click(screen.getAllByRole('button', { name: 'Apply' })[0]);
    expect(onApplyTheme).toHaveBeenCalledWith(getThemePreset(THEME_GALLERY_ITEMS[0].themeId));
  });

  it('behaves as a modal dialog and restores focus after Escape closes it', async () => {
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();
    const onClose = vi.fn();
    const { unmount } = render(<ThemeGalleryPanel onApplyTheme={vi.fn()} onClose={onClose} />);
    const dialog = screen.getByRole('dialog', { name: 'Theme Gallery' });

    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(document.activeElement).toBe(dialog);
    fireEvent.keyDown(dialog, { key: 'Tab' });
    expect(document.activeElement).toBe(dialog);
    fireEvent.keyDown(dialog, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();

    unmount();
    await new Promise<void>(resolve => {
      requestAnimationFrame(() => {
        expect(document.activeElement).toBe(trigger);
        trigger.remove();
        resolve();
      });
    });
  });
});
