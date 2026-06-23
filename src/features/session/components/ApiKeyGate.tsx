// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useState } from 'react';
import { Clipboard } from '@capacitor/clipboard';
import { Capacitor } from '@capacitor/core';
import { IconChevronLeft, IconChevronRight, IconQuestionMarkCircle, IconKey, IconSparkles, IconEyeOpen, IconCreditCard, IconShield, IconTrash } from '../../../shared/ui/Icons';
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
const PRIVACY_POLICY_URL = 'https://chatwithmaestro.com/privacy.html';
const GEMINI_API_TERMS_URL = 'https://ai.google.dev/gemini-api/terms';
const GOOGLE_PRIVACY_POLICY_URL = 'https://policies.google.com/privacy';
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
  const [showInstructions, setShowInstructions] = useState(false);
  const [instructionIndex, setInstructionIndex] = useState(0);
  const [isAutoPlaying, setIsAutoPlaying] = useState(true);
  const [costSummary, setCostSummary] = useState({ inputTokens: 0, outputTokens: 0, imageGenCount: 0, totalCostUsd: 0 });

  const canClose = !isBlocking;
  const totalInstructions = INSTRUCTION_IMAGES.length;
  const isBillingHelp = instructionIndex >= REGULAR_INSTRUCTIONS_COUNT;
  const displayValue = value || (hasKey ? maskedKey || '\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022' : '');
  const hasCostEstimate = costSummary.totalCostUsd > 0;
  const showCostButton = hasKey && hasCostEstimate;
  const inputRightPaddingClass = hasKey
    ? showCostButton
      ? 'pr-44'
      : 'pr-24'
    : 'pr-14';
  const savedKeyBorderColor = keyInvalid ? 'hsl(0 60% 60%)' : hasKey ? 'hsl(120 40% 60%)' : undefined;
  const closeCurrentView = () => {
    if (showInstructions) {
      setShowInstructions(false);
    } else {
      onClose();
    }
  };

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
      const start = REGULAR_INSTRUCTIONS_COUNT;
      const count = totalInstructions - start;
      if (count <= 0) return;
      const relativeIndex = instructionIndex - start;
      const nextRelative = (relativeIndex + direction + count) % count;
      setInstructionIndex(start + nextRelative);
    } else {
      const count = REGULAR_INSTRUCTIONS_COUNT;
      setInstructionIndex((prev) => (prev + direction + count) % count);
    }
    setIsAutoPlaying(false);
  };

  const handleInstructionJump = (nextIndex: number) => {
    setInstructionIndex(nextIndex);
    setIsAutoPlaying(false);
  };

  const saveApiKeyFromRawValue = async (rawValue: string) => {
    if (isSaving) return;

    const normalized = normalizeApiKey(rawValue);
    setValue(normalized);
    onValueChange?.(normalized);

    const ok = await onSave(normalized);
    if (ok) {
      setValue('');
      if (canClose) onClose();
    }
  };

  const attemptAutoPasteFromClipboard = async () => {
    if (value.trim() || hasKey) return;

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
    await saveApiKeyFromRawValue(normalized);
  };

  const handleApiKeyPaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    void saveApiKeyFromRawValue(e.clipboardData.getData('text'));
  };

  const handleApiKeyKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    void saveApiKeyFromRawValue(value);
  };

  const handleClearSavedKey = async () => {
    await onClear();
    setValue('');
    setCostSummary(getCostSummary());
  };

  const handleApiKeyInputChange = (next: string) => {
    let nextValue = next;
    if (!value && hasKey && displayValue) {
      if (next === displayValue) return;
      if (next.startsWith(displayValue)) {
        nextValue = next.slice(displayValue.length);
      } else if (next.endsWith(displayValue)) {
        nextValue = next.slice(0, -displayValue.length);
      }
    }
    setValue(nextValue);
    onValueChange?.(nextValue);
  };

  if (!isOpen) return null;

  if (!showInstructions) {
    return (
      <div
        className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
        onClick={closeCurrentView}
      >
        {canClose && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onClose();
            }}
            aria-label={t('apiKeyGate.close')}
            className="absolute inline-flex h-10 w-10 items-center justify-center bg-gate-bg text-gate-muted-text hover:bg-gate-input-bg hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
            style={{
              top: 'calc(1rem + env(safe-area-inset-top))',
              left: 'calc(1rem + env(safe-area-inset-left))',
            }}
          >
            <IconChevronLeft className="h-5 w-5" />
          </button>
        )}

        <div className="relative w-full max-w-sm" onClick={(e) => e.stopPropagation()}>
          <div
            className="msg-tape msg-tape-wrinkled"
            style={{
              top: '-14px',
              left: '50%',
              width: '104px',
              height: '24px',
              transform: 'translateX(-50%) rotate(-3deg)',
            }}
          />
          <div className="relative overflow-visible bg-gate-bg p-4 text-sm text-gate-text msg-depth sketchy-border-thin sketch-shape-2">
            <div className="space-y-3">
              <button
                type="button"
                onClick={() => openExternalUrl(AI_STUDIO_URL)}
                className="w-full bg-gate-btn-bg px-4 py-3 text-left text-sm font-medium text-gate-btn-text hover:bg-gate-btn-bg/80 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
              >
                {t('apiKeyGate.stepOne')}
              </button>

              <div className="relative">
                <input
                  type={value ? 'password' : 'text'}
                  value={displayValue}
                  onChange={(e) => handleApiKeyInputChange(e.target.value)}
                  onClick={attemptAutoPasteFromClipboard}
                  onFocus={(e) => {
                    if (!value && hasKey) e.currentTarget.select();
                  }}
                  onPaste={handleApiKeyPaste}
                  onKeyDown={handleApiKeyKeyDown}
                  placeholder={t('apiKeyGate.placeholder')}
                  aria-label={t('apiKeyGate.keyLabel')}
                  disabled={isSaving}
                  className={`min-h-12 w-full bg-gate-input-bg/75 py-3 pl-4 text-sm text-gate-text placeholder:text-gate-muted-text focus:outline-none focus:ring-2 focus:ring-gate-accent disabled:opacity-70 sketchy-border-thin ${inputRightPaddingClass}`}
                  style={{ borderColor: savedKeyBorderColor }}
                  autoFocus
                />
                <div className="absolute right-2 top-1/2 flex -translate-y-1/2 items-center gap-1">
                  {hasKey && (
                    <button
                      type="button"
                      onClick={handleClearSavedKey}
                      aria-label={t('apiKeyGate.clearSavedKey')}
                      className="inline-flex h-8 w-8 items-center justify-center text-gate-muted-text transition-colors hover:bg-gate-bg hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
                    >
                      <IconTrash className="h-4 w-4" />
                    </button>
                  )}
                  {showCostButton && (
                    <button
                      type="button"
                      onClick={() => openExternalUrl(GOOGLE_BILLING_URL)}
                      aria-label={t('apiKeyGate.costLabel')}
                      title={t('apiKeyGate.costLabel')}
                      className="inline-flex h-8 items-center justify-center gap-1 px-2 text-xs text-gate-muted-text transition-colors hover:bg-gate-bg hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
                    >
                      <IconSparkles className="h-3.5 w-3.5" />
                      <span>~${costSummary.totalCostUsd.toFixed(2)}</span>
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => {
                      setInstructionIndex(0);
                      setShowInstructions(true);
                      setIsAutoPlaying(true);
                    }}
                    aria-label={t('apiKeyGate.viewInstructions')}
                    className="inline-flex h-8 w-8 items-center justify-center bg-gate-bg text-gate-muted-text transition-colors hover:bg-gate-input-bg hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
                  >
                    <IconQuestionMarkCircle className="h-5 w-5" />
                  </button>
                </div>
              </div>

              {error && (
                <div className="text-sm text-gate-error-text">
                  {error === 'apiKeyGate.keyInvalid'
                    ? t('apiKeyGate.keyInvalid', { maskedKey: value.length >= 8 ? `${value.slice(0, 4)}\u2022\u2022\u2022\u2022${value.slice(-4)}` : '' })
                    : error}
                </div>
              )}
            </div>
          </div>
          <p className="mt-3 px-1 text-center text-[10px] leading-snug text-white/45">
            {t('apiKeyGate.disclaimerPrefix')}{' '}
            <a
              href={GEMINI_API_TERMS_URL}
              onClick={(e) => {
                e.preventDefault();
                void openExternalUrl(GEMINI_API_TERMS_URL);
              }}
              className="underline decoration-white/25 underline-offset-2 transition-colors hover:text-white/70"
            >
              {t('apiKeyGate.geminiTerms')}
            </a>
            {' '}{t('apiKeyGate.disclaimerConnector')}{' '}
            <a
              href={GOOGLE_PRIVACY_POLICY_URL}
              onClick={(e) => {
                e.preventDefault();
                void openExternalUrl(GOOGLE_PRIVACY_POLICY_URL);
              }}
              className="underline decoration-white/25 underline-offset-2 transition-colors hover:text-white/70"
            >
              {t('apiKeyGate.googlePrivacyPolicy')}
            </a>
            {t('apiKeyGate.disclaimerSuffix')}
          </p>
        </div>
      </div>
    );
  }

  // =========================================================================
  // INSTRUCTION VIEW
  // =========================================================================
  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4"
      onClick={closeCurrentView}
    >
      <div className="w-full max-w-lg bg-gate-bg shadow-xl sketchy-border sketch-shape-7" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between px-6 pt-6">
          <div className="flex items-start gap-3">
            <div className="flex h-10 w-10 items-center justify-center bg-gate-accent/15 text-gate-accent sketchy-border-thin">
              <IconKey className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gate-text font-sketch">
                {isBillingHelp ? t('apiKeyGate.billingTitle') : t('apiKeyGate.title')}
              </h2>
              <div className="mt-1 space-y-0.5 text-sm text-gate-muted-text">
                <div className="flex items-center gap-1.5">
                  <IconKey className="h-3.5 w-3.5 shrink-0" />
                  <span>{t('apiKeyGate.infoLogin')}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <IconEyeOpen className="h-3.5 w-3.5 shrink-0" />
                  <span>{t('apiKeyGate.infoVisibility')}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <IconCreditCard className="h-3.5 w-3.5 shrink-0" />
                  <span>{t('apiKeyGate.infoBilling')}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <IconSparkles className="h-3.5 w-3.5 shrink-0" />
                  <span>{t('apiKeyGate.infoCost')}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <IconShield className="h-3.5 w-3.5 shrink-0" />
                  <span>{t('apiKeyGate.infoMore')}</span>
                  <button
                    onClick={() => openExternalUrl(PRIVACY_POLICY_URL)}
                    className="text-gate-accent hover:underline"
                  >
                    {t('apiKeyGate.privacyPolicy')}
                  </button>
                </div>
              </div>
            </div>
          </div>
          {showHeaderClose && (
            <button
              onClick={handleHeaderClose}
              className="text-gate-muted-text hover:text-gate-text"
              aria-label={headerCloseLabel}
            >
              <IconChevronLeft className="h-5 w-5" />
            </button>
          )}
        </div>

        <div className="px-6 py-4 space-y-4">
          {(
            <div className="flex flex-col items-center gap-4">
              <div className="relative w-full max-w-[360px] overflow-hidden bg-gate-bg shadow-sm sketchy-border-thin">
                <div className="aspect-[9/16] w-full bg-gate-input-bg/70">
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
                        className="pointer-events-auto bg-gate-bg/90 p-2 text-gate-muted-text shadow hover:bg-gate-bg sketchy-border-thin"
                      >
                        <IconChevronLeft className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleInstructionStep(1)}
                        aria-label={t('apiKeyGate.nextInstruction')}
                        className="pointer-events-auto bg-gate-bg/90 p-2 text-gate-muted-text shadow hover:bg-gate-bg sketchy-border-thin"
                      >
                        <IconChevronRight className="h-5 w-5" />
                      </button>
                    </div>
                    {!isBillingHelp && (
                      <>
                        <div className="absolute bottom-3 left-1/2 -translate-x-1/2 bg-gate-bg/90 px-3 py-1 shadow-sm sketchy-border-thin">
                          <div className="flex items-center gap-1.5">
                            {INSTRUCTION_IMAGES.slice(0, REGULAR_INSTRUCTIONS_COUNT).map((_, index) => (
                              <button
                                key={`instruction-dot-${index}`}
                                type="button"
                                aria-label={t('apiKeyGate.instructionStep', { step: index + 1, total: REGULAR_INSTRUCTIONS_COUNT })}
                                onClick={() => handleInstructionJump(index)}
                                className={`h-2 w-2 sketchy-border-thin ${index === instructionIndex ? 'bg-gate-btn-bg' : 'bg-line-border'}`}
                              />
                            ))}
                          </div>
                        </div>
                        <div className="absolute right-3 top-3 bg-gate-bg/90 px-2 py-1 text-xs text-gate-muted-text shadow-sm sketchy-border-thin">
                          {instructionIndex + 1} / {REGULAR_INSTRUCTIONS_COUNT}
                        </div>
                      </>
                    )}
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default ApiKeyGate;

