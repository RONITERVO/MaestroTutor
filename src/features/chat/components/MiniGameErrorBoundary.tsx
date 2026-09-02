// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';

interface Props {
  children: React.ReactNode;
  failedText?: string;
  retryText?: string;
}

interface State {
  hasError: boolean;
}

class MiniGameErrorBoundary extends React.Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  render() {
    if (this.state.hasError) {
      const failedText = this.props.failedText || 'Mini-game failed to render.';
      const retryText = this.props.retryText || 'Retry';
      return (
        <div className="w-full max-w-[560px] mx-auto rounded-2xl border border-game-error-border bg-game-error-bg p-6 text-center">
          <p className="text-sm text-game-error-text">{failedText}</p>
          <button
            type="button"
            onClick={() => this.setState({ hasError: false })}
            className="mt-3 rounded-full border border-game-error-btn-border bg-game-error-btn-bg px-4 py-1.5 text-xs text-game-error-text hover:bg-game-error-btn-hover"
          >
            {retryText}
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}

export default MiniGameErrorBoundary;
