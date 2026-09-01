// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import Header from './Header';

vi.mock('../hooks/useStatusAnimations', () => ({
  useStatusAnimations: () => ({
    currentAnimation: null,
    isVisible: false,
    handleAnimationEnded: vi.fn(),
    handleTransitionEnd: vi.fn(),
  }),
}));

afterEach(cleanup);

describe('Header access button', () => {
  it('describes managed access when it is the active access method', () => {
    render(
      <Header
        onOpenApiKey={vi.fn()}
        hasApiKey={false}
        hasManagedAccess
      />,
    );

    expect(screen.getByTitle('Managed access')).toBeTruthy();
    expect(screen.queryByTitle('Manage API Key')).toBeNull();
  });

  it('keeps API-key management as the label when both access methods exist', () => {
    render(
      <Header
        onOpenApiKey={vi.fn()}
        hasApiKey
        hasManagedAccess
      />,
    );

    expect(screen.getByTitle('Manage API Key')).toBeTruthy();
    expect(screen.queryByTitle('Managed access')).toBeNull();
  });

  it('describes the missing-access action when neither method exists', () => {
    render(
      <Header
        onOpenApiKey={vi.fn()}
        hasApiKey={false}
        hasManagedAccess={false}
      />,
    );

    expect(screen.getByTitle('AI access required')).toBeTruthy();
  });
});
