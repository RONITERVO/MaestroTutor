// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { Capacitor } from '@capacitor/core';
import { Filesystem, Directory, Encoding } from '@capacitor/filesystem';
import { Share } from '@capacitor/share';
import type { PresetTheme } from '../config/presetThemes';
import { migrateLegacyColorMap } from '../config/colorRenameMap';

const THEME_MIME = 'application/json';

/**
 * Validates that a parsed object has the PresetTheme shape.
 * Returns a cleaned PresetTheme or throws on invalid format.
 */
export const validateThemePreset = (obj: unknown): PresetTheme => {
  if (!obj || typeof obj !== 'object') {
    throw new Error('INVALID_THEME_FORMAT');
  }
  const raw = obj as Record<string, unknown>;

  if (typeof raw.name !== 'string' || !raw.name.trim()) {
    throw new Error('INVALID_THEME_FORMAT');
  }
  if (typeof raw.colors !== 'object' || raw.colors === null || Array.isArray(raw.colors)) {
    throw new Error('INVALID_THEME_FORMAT');
  }

  const colors: Record<string, string> = {};
  for (const [key, value] of Object.entries(raw.colors as Record<string, unknown>)) {
    if (typeof value === 'string') {
      colors[key] = value;
    }
  }

  return {
    name: raw.name.trim(),
    description: typeof raw.description === 'string' ? raw.description.trim() : '',
    colors: migrateLegacyColorMap(colors),
  };
};

/**
 * Export a PresetTheme to a .json file.
 * Uses File System Access API on web, Capacitor Filesystem + Share on native.
 */
export const exportThemeToFile = async (preset: PresetTheme): Promise<void> => {
  const json = JSON.stringify(preset, null, 2);
  const safeName = preset.name.replace(/[\\/:*?"<>|]/g, '-').trim() || 'theme';
  const filename = `${safeName}.json`;

  if (Capacitor.isNativePlatform()) {
    let uri: string | undefined;
    try {
      const result = await Filesystem.writeFile({
        path: filename,
        data: json,
        directory: Directory.Cache,
        encoding: Encoding.UTF8,
      });
      uri = result?.uri;
    } catch {
      // Fallback to Documents
      const result = await Filesystem.writeFile({
        path: filename,
        data: json,
        directory: Directory.Documents,
        encoding: Encoding.UTF8,
      });
      uri = result?.uri;
    }
    if (!uri) throw new Error('Failed to write theme file');
    await Share.share({
      title: `Theme: ${preset.name}`,
      url: uri,
      dialogTitle: 'Export Theme',
    });
    return;
  }

  // Web: use File System Access API or fallback to blob download
  const picker = (typeof window !== 'undefined' ? (window as any).showSaveFilePicker : undefined) as
    | undefined
    | ((options?: any) => Promise<any>);

  if (typeof picker === 'function') {
    try {
      const handle = await picker({
        suggestedName: filename,
        types: [{ description: 'Theme Preset', accept: { [THEME_MIME]: ['.json'] } }],
        excludeAcceptAllOption: false,
      });
      const writable = await handle.createWritable();
      await writable.write(json);
      await writable.close();
      return;
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.toLowerCase().includes('cancel') || msg.toLowerCase().includes('abort')) return;
      // Fall through to blob download
    }
  }

  // Fallback: blob download
  const blob = new Blob([json], { type: THEME_MIME });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};

/**
 * Import a theme from a .json file via file picker.
 * Returns the validated PresetTheme.
 */
export const importThemeFromFile = (): Promise<PresetTheme> => {
  return new Promise((resolve, reject) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';

    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) {
        reject(new Error('No file selected'));
        return;
      }
      try {
        const text = await file.text();
        const parsed = JSON.parse(text);
        const preset = validateThemePreset(parsed);
        resolve(preset);
      } catch (err) {
        if (err instanceof SyntaxError) {
          reject(new Error('INVALID_THEME_FORMAT'));
        } else {
          reject(err);
        }
      }
    };

    // Handle cancel (no file selected)
    input.oncancel = () => reject(new Error('CANCELLED'));

    input.click();
  });
};
