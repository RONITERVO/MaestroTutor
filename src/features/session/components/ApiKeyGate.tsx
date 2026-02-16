// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useMemo, useState } from 'react';
import { Clipboard } from '@capacitor/clipboard';
import { Capacitor } from '@capacitor/core';
import { IconCheck, IconChevronLeft, IconChevronRight, IconQuestionMarkCircle, IconKey, IconXMark, IconSparkles } from '../../../shared/ui/Icons';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { openExternalUrl } from '../../../shared/utils/openExternalUrl';
import { isLikelyApiKey, normalizeApiKey } from '../../../core/security/apiKeyStorage';
import { getCostSummary, GOOGLE_BILLING_URL } from '../../../shared/utils/costTracker';

interface ApiKeyGateProps {
  isOpen: boolean;
  isBlocking: boolean;
  hasKey: boolean;
  maskedKey?: string | null;
  isSaving?: boolean;
  error?: string | null;
  keyInvalid?: boolean;
  instructionFocusIndex?: number | null;
  onSave: (value: string) => Promise<boolean>;
  onClear: () => Promise<void>;
  onClose: () => void;
  onValueChange?: (value: string) => void;
}

const AI_STUDIO_URL = 'https://aistudio.google.com/app/apikey';
const PRIVACY_POLICY_URL = 'https://ronitervo.github.io/MaestroTutor/privacy.html';
const INSTRUCTION_IMAGES = Array.from({ length: 12 }, (_, index) => `${import.meta.env.BASE_URL}api-key-instructions/step-${index + 1}.jpg`);
const INSTRUCTION_AUTO_ADVANCE_MS = 3200;
const REGULAR_INSTRUCTIONS_COUNT = 9;

const ApiKeyGate: React.FC<ApiKeyGateProps> = ({
  isOpen,
  isBlocking,
  hasKey,
  maskedKey,
  isSaving = false,
  error,
  keyInvalid = false,
  instructionFocusIndex,
  onSave,
  onClear,
  onClose,
  onValueChange,
}) => {
  const { t } = useAppTranslations();
  const [value, setValue] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [showInstructions, setShowInstructions] = useState(false);
  const [instructionIndex, setInstructionIndex] = useState(0);
  const [isAutoPlaying, setIsAutoPlaying] = useState(true);
  const [costSummary, setCostSummary] = useState({ inputTokens: 0, outputTokens: 0, imageGenCount: 0, totalCostUsd: 0 });
  const canClose = !isBlocking;
  const totalInstructions = INSTRUCTION_IMAGES.length;
  const isBillingHelp = instructionIndex >= REGULAR_INSTRUCTIONS_COUNT;

  const canSave = useMemo(() => {
    return value.trim().length >= 20 && !isSaving;
  }, [value, isSaving]);

  useEffect(() => {
    if (!isOpen) {
      setShowInstructions(false);
      setInstructionIndex(0);
      setIsAutoPlaying(true);
    } else {
      setCostSummary(getCostSummary());
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || typeof instructionFocusIndex !== 'number' || totalInstructions <= 0) return;

    const clamped = Math.max(0, Math.min(totalInstructions - 1, instructionFocusIndex));
    setInstructionIndex(clamped);
    setShowInstructions(true);
    setIsAutoPlaying(false);
  }, [instructionFocusIndex, isOpen, totalInstructions]);

  useEffect(() => {
    if (!showInstructions || !isAutoPlaying || totalInstructions <= 1 || isBillingHelp) return;
    const intervalId = window.setInterval(() => {
      setInstructionIndex((prev) => {
        // Prevent advancing into billing instructions on any edge case
        if (prev >= REGULAR_INSTRUCTIONS_COUNT - 1) return 0;
        return prev + 1;
      });
    }, INSTRUCTION_AUTO_ADVANCE_MS);
    return () => window.clearInterval(intervalId);
  }, [showInstructions, isAutoPlaying, totalInstructions, isBillingHelp]);

  const currentInstruction = INSTRUCTION_IMAGES[instructionIndex] || '';
  const showHeaderClose = showInstructions || canClose;
  const headerCloseLabel = showInstructions ? t('apiKeyGate.closeInstructions') : t('apiKeyGate.close');
  const handleHeaderClose = showInstructions ? () => setShowInstructions(false) : onClose;

  const handleInstructionStep = (direction: number) => {
    if (totalInstructions === 0) return;

    if (isBillingHelp) {
       // Loop within the billing set [REGULAR_INSTRUCTIONS_COUNT, totalInstructions - 1]
       const start = REGULAR_INSTRUCTIONS_COUNT;
       const count = totalInstructions - start;
       if (count <= 0) return;
       const relativeIndex = instructionIndex - start;
       const nextRelative = (relativeIndex + direction + count) % count;
       setInstructionIndex(start + nextRelative);
    } else {
       // Loop within regular set [0, REGULAR_INSTRUCTIONS_COUNT - 1]
       const count = REGULAR_INSTRUCTIONS_COUNT;
       setInstructionIndex((prev) => (prev + direction + count) % count);
    }
    setIsAutoPlaying(false);
  };

  const handleInstructionJump = (nextIndex: number) => {
    setInstructionIndex(nextIndex);
    setIsAutoPlaying(false);
  };

  const attemptAutoPasteFromClipboard = async () => {
    if (value.trim()) return;

    let clipboardText = '';

    try {
      if (Capacitor.isNativePlatform()) {
        const { value } = await Clipboard.read();
        clipboardText = value || '';
      } else if (typeof navigator !== 'undefined' && navigator.clipboard?.readText) {
        clipboardText = await navigator.clipboard.readText();
      }
    } catch {
      // Ignore clipboard read failures
    }

    if (!clipboardText) return;

    const normalized = normalizeApiKey(clipboardText);
    if (!normalized || !isLikelyApiKey(normalized) || /\s/.test(normalized)) return;
    setValue(normalized);
    onValueChange?.(normalized);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg bg-card shadow-xl sketchy-border">
        <div className="flex items-start justify-between px-6 pt-6">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center bg-accent/15 text-accent sketchy-border-thin">
              <IconKey className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-foreground font-sketch">
                {isBillingHelp ? t('apiKeyGate.billingTitle') : t('apiKeyGate.title')}
              </h2>
              <p className="text-sm text-muted-foreground">
                {t('apiKeyGate.subtitle')}{' '}
                <button
                  onClick={() => openExternalUrl(PRIVACY_POLICY_URL)}
                  className="text-accent hover:underline inline-flex items-center"
                >
                  {t('apiKeyGate.privacyPolicy')}
                </button>
              </p>
            </div>
          </div>
          {showHeaderClose && (
            <button
              onClick={handleHeaderClose}
              className="text-muted-foreground hover:text-foreground"
              aria-label={headerCloseLabel}
            >
              <IconXMark className="h-5 w-5" />
            </button>
          )}
        </div>

        <div className="px-6 py-4 space-y-4">
          {showInstructions ? (
            <div className="flex flex-col items-center gap-4">
              <div className="relative w-full max-w-[360px] overflow-hidden bg-card shadow-sm sketchy-border-thin">
                <div className="aspect-[9/16] w-full bg-muted">
                  <img
                    src={currentInstruction}
                    alt={t('apiKeyGate.instructionStep', { step: instructionIndex + 1, total: totalInstructions })}
                    className="h-full w-full object-contain"
                    draggable={false}
                  />
                </div>
                {totalInstructions > 1 && (
                  <>
                    <div className="pointer-events-none absolute inset-0 flex items-center justify-between px-2">
                      <button
                        type="button"
                        onClick={() => handleInstructionStep(-1)}
                        aria-label={t('apiKeyGate.previousInstruction')}
                        className="pointer-events-auto bg-card/90 p-2 text-muted-foreground shadow hover:bg-card sketchy-border-thin"
                      >
                        <IconChevronLeft className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleInstructionStep(1)}
                        aria-label={t('apiKeyGate.nextInstruction')}
                        className="pointer-events-auto bg-card/90 p-2 text-muted-foreground shadow hover:bg-card sketchy-border-thin"
                      >
                        <IconChevronRight className="h-5 w-5" />
                      </button>
                    </div>
                    {!isBillingHelp && (
                      <>
                        <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-card/90 px-3 py-1 shadow-sm sketchy-border-thin">
                          <div className="flex items-center gap-1.5">
                            {INSTRUCTION_IMAGES.slice(0, REGULAR_INSTRUCTIONS_COUNT).map((_, index) => (
                              <button
                                key={`instruction-dot-${index}`}
                                type="button"
                                aria-label={t('apiKeyGate.instructionStep', { step: index + 1, total: REGULAR_INSTRUCTIONS_COUNT })}
                                onClick={() => handleInstructionJump(index)}
                                className={`h-2 w-2 sketchy-border-thin ${index === instructionIndex ? 'bg-accent' : 'bg-border'}`}
                              />
                            ))}
                          </div>
                        </div>
                        <div className="absolute right-3 top-3 bg-card/90 px-2 py-1 text-xs text-muted-foreground shadow-sm sketchy-border-thin">
                          {instructionIndex + 1} / {REGULAR_INSTRUCTIONS_COUNT}
                        </div>
                      </>
                    )}
                  </>
                )}
              </div>
            </div>
          ) : (
            <>
              <div className="bg-muted p-4 text-sm text-foreground space-y-2 sketchy-border-thin">
                <div className="font-medium text-foreground font-sketch">{t('apiKeyGate.stepsTitle')}</div>
                <ol className="list-decimal pl-5 space-y-1">
                  <li>{t('apiKeyGate.stepOne')}</li>
                  <li>{t('apiKeyGate.stepTwo')}</li>
                </ol>
                <div className="flex flex-wrap items-center gap-2">
                  <button
                    className="inline-flex items-center gap-2 bg-accent px-3 py-2 text-accent-foreground hover:bg-accent/80 sketchy-border-thin"
                    onClick={() => openExternalUrl(AI_STUDIO_URL)}
                  >
                    {t('apiKeyGate.openAiStudio')}
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setInstructionIndex(0);
                      setShowInstructions(true);
                      setIsAutoPlaying(true);
                    }}
                    aria-label={t('apiKeyGate.viewInstructions')}
                    className="inline-flex h-9 w-9 items-center justify-center bg-card text-muted-foreground hover:bg-muted sketchy-border-thin"
                  >
                    <IconQuestionMarkCircle className="h-5 w-5" />
                  </button>
                </div>
              </div>

              <label className="block text-sm font-medium text-foreground">{t('apiKeyGate.keyLabel')}</label>
              <div className="flex items-center gap-2">
                <input
                  type={showKey ? 'text' : 'password'}
                  value={value}
                  onChange={(e) => {
                    const next = e.target.value;
                    setValue(next);
                    onValueChange?.(next);
                  }}
                  onClick={attemptAutoPasteFromClipboard}
                  placeholder={t('apiKeyGate.placeholder')}
                  className="flex-1 px-3 py-2 text-sm bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-accent sketchy-border-thin"
                  autoFocus
                />
                <button
                  className="px-3 py-2 text-sm text-foreground hover:bg-muted sketchy-border-thin"
                  onClick={() => setShowKey(!showKey)}
                  type="button"
                >
                  {showKey ? t('apiKeyGate.hide') : t('apiKeyGate.show')}
                </button>
              </div>

              {error && <div className="text-sm text-destructive">{error}</div>}

              {hasKey && (
                <div
                  className={`p-3 text-sm flex items-center gap-2 sketchy-border-thin ${
                    keyInvalid
                      ? 'bg-red-50 text-red-800'
                      : 'bg-green-50 text-green-800'
                  }`}
                  style={{ borderColor: keyInvalid ? 'hsl(0 60% 60%)' : 'hsl(120 40% 60%)' }}
                >
                  {keyInvalid ? (
                    <IconXMark className="h-4 w-4 shrink-0" />
                  ) : (
                    <IconCheck className="h-4 w-4 shrink-0" />
                  )}
                  <span className="truncate">
                    {keyInvalid
                      ? t('apiKeyGate.keyInvalid', { maskedKey: maskedKey || '' })
                      : t('apiKeyGate.currentKeySaved', { maskedKey: maskedKey ? `(${maskedKey})` : '' }).trim()}
                  </span>
                  {costSummary.totalCostUsd > 0 && (
                    <button
                      type="button"
                      onClick={() => openExternalUrl(GOOGLE_BILLING_URL)}
                      className="ml-auto shrink-0 flex items-center gap-1 text-xs opacity-70 hover:opacity-100"
                      aria-label={t('apiKeyGate.costLabel')}
                    >
                      <IconSparkles className="h-3 w-3" />
                      <span>~${costSummary.totalCostUsd.toFixed(2)}</span>
                    </button>
                  )}
                </div>
              )}
            </>
          )}
        </div>

        {!showInstructions && (
          <div className="flex items-center justify-between px-6 pb-6">
            <button
              className="text-sm text-muted-foreground hover:text-foreground"
              onClick={onClear}
              disabled={!hasKey}
            >
              {t('apiKeyGate.clearSavedKey')}
            </button>
            <div className="flex items-center gap-2">
              {canClose && (
                <button
                  onClick={onClose}
                  className="px-4 py-2 text-sm text-foreground hover:bg-muted sketchy-border-thin"
                >
                  {t('apiKeyGate.cancel')}
                </button>
              )}
              <button
                onClick={async () => {
                  const ok = await onSave(value);
                  if (ok) {
                    setValue('');
                    if (canClose) onClose();
                  }
                }}
                disabled={!canSave}
                className="bg-accent px-4 py-2 text-sm font-medium text-accent-foreground hover:bg-accent/80 disabled:opacity-50 sketchy-border-thin"
              >
                {isSaving ? t('apiKeyGate.saving') : t('apiKeyGate.saveKey')}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default ApiKeyGate;