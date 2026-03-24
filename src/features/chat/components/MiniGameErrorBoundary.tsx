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
        <div className="w-full max-w-[560px] mx-auto rounded-2xl border border-red-900/40 bg-red-950/60 p-6 text-center">
          <p className="text-sm text-red-200">{failedText}</p>
          <button
            type="button"
            onClick={() => this.setState({ hasError: false })}
            className="mt-3 rounded-full border border-red-400/30 bg-red-900/40 px-4 py-1.5 text-xs text-red-200 hover:bg-red-900/60"
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
