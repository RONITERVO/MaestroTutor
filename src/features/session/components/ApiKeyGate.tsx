// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useState, useRef } from 'react';
import { Clipboard } from '@capacitor/clipboard';
import { Capacitor } from '@capacitor/core';
import { IconChevronLeft, IconChevronRight, IconQuestionMarkCircle, IconKey, IconXMark, IconSparkles, IconEyeOpen, IconCreditCard, IconShield, IconTrash } from '../../../shared/ui/Icons';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { openExternalUrl } from '../../../shared/utils/openExternalUrl';
import { isLikelyApiKey, normalizeApiKey } from '../../../core/security/apiKeyStorage';
import { getCostSummary, GOOGLE_BILLING_URL } from '../../../shared/utils/costTracker';

// Hardcoded developer password to bypass the tester form. 
// Password is: thedev
const DEV_PASSWORD = 'thedev';

// REPLACE THIS with your deployed Google Apps Script Web App URL
const GOOGLE_APPS_SCRIPT_URL = 'https://script.google.com/macros/s/AKfycbyYCcKruNccmRqB7YmVFPBLUpC4gvTAMtfOjYJ1oJS-Dp44Am1HJoKJAp-RRtf02eIx/exec';

const STORAGE_KEY_EMAIL = 'maestro_tester_email';
const STORAGE_KEY_SUBMISSION_COUNT = 'maestro_tester_submission_count';
const MAX_SUBMISSIONS = 100;
const MAX_EMAIL_LENGTH = 100;

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

  // --- TESTER FORM STATE ---
  const [showTesterForm, setShowTesterForm] = useState(() => !Capacitor.isNativePlatform() && !hasKey);
  const [testerEmail, setTesterEmail] = useState('');
  const [testerStatus, setTesterStatus] = useState<'idle' | 'submitting' | 'error'>('idle');
  const [emailErrorMsg, setEmailErrorMsg] = useState('');

  // Memory & Approval State
  const [lastSubmittedEmail, setLastSubmittedEmail] = useState(() => localStorage.getItem(STORAGE_KEY_EMAIL) || '');
  const [isCheckingApproval, setIsCheckingApproval] = useState(false);
  const [isApproved, setIsApproved] = useState(false);
  const skipNextCheckRef = useRef(false); // Prevents checking immediately after a POST race condition

  // Alternate Form View Mode
  const [viewMode, setViewMode] = useState<'submit' | 'check'>('submit');
  const [checkEmailInput, setCheckEmailInput] = useState('');
  const [notFoundError, setNotFoundError] = useState(false);

  const [submissionCount, setSubmissionCount] = useState(() => {
    const stored = localStorage.getItem(STORAGE_KEY_SUBMISSION_COUNT);
    return stored ? parseInt(stored, 10) : 0;
  });

  const [showDevPassword, setShowDevPassword] = useState(false);
  const [devPasswordInput, setDevPasswordInput] = useState('');
  // -------------------------

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

      // Keep memory of submission, but reset UI state if reopened
      setTesterStatus('idle');
      setTesterEmail('');
      setEmailErrorMsg('');
      setShowDevPassword(false);
      setDevPasswordInput('');
    } else {
      setCostSummary(getCostSummary());
    }
  }, [isOpen]);

  // Check approval status on load if we have a saved email
  useEffect(() => {
    const checkApprovalStatus = async () => {
      if (!lastSubmittedEmail || !showTesterForm) return;

      // If we just submitted the form, skip the fetch so we don't hit a sheet sync race condition
      if (skipNextCheckRef.current) {
        skipNextCheckRef.current = false;
        return;
      }

      setIsCheckingApproval(true);
      try {
        const response = await fetch(`${GOOGLE_APPS_SCRIPT_URL}?email=${encodeURIComponent(lastSubmittedEmail)}`, {
          redirect: "follow"
        });
        const data = await response.json();

        if (data.found) {
          setIsApproved(!!data.approved);
          setNotFoundError(false);
          // Save to local storage just in case it was a manual check
          localStorage.setItem(STORAGE_KEY_EMAIL, lastSubmittedEmail);
        } else {
          // They checked an email that isn't in the sheet at all
          setLastSubmittedEmail('');
          localStorage.removeItem(STORAGE_KEY_EMAIL);
          setNotFoundError(true);
          setViewMode('submit'); // Send them back to the main form to apply
        }
      } catch (e) {
        console.error("Failed to check approval status", e);
        // Fallback: don't delete their email if network fails, just show pending
        setIsApproved(false);
      } finally {
        setIsCheckingApproval(false);
      }
    };

    checkApprovalStatus();
  }, [lastSubmittedEmail, showTesterForm]);

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

  // =========================================================================
  // TESTER FORM VIEW (Web Only) - GRANDMA PROOF VERSION
  // =========================================================================
  if (showTesterForm) {
    return (
      <div
        className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4"
        onClick={() => {
          if (canClose) onClose();
        }}
      >
        <div className="w-full max-w-sm bg-gate-bg shadow-xl sketchy-border sketch-shape-3 p-8 relative" onClick={(e) => e.stopPropagation()}>

          {canClose && (
            <button
              onClick={onClose}
              className="absolute right-4 top-4 text-gate-muted-text hover:text-gate-text"
            >
              <IconXMark className="h-5 w-5" />
            </button>
          )}

          <div className="flex flex-col items-center text-center space-y-3">
            <div className="flex h-12 w-12 items-center justify-center bg-gate-accent/15 text-gate-accent sketchy-border-thin rounded-full mb-2">
              <IconSparkles className="h-6 w-6" />
            </div>

            {/* GRANDMA-PROOF STATE 1: CHECKING STATUS */}
            {isCheckingApproval && (
              <div className="py-8 space-y-4 w-full flex flex-col items-center">
                <div className="h-8 w-8 animate-spin rounded-full border-b-2 border-gate-accent"></div>
                <p className="text-sm text-gate-muted-text">{t('apiKeyGate.testerFormChecking')}</p>
              </div>
            )}

            {/* GRANDMA-PROOF STATE 2: APPROVED! SHOW DOWNLOAD LINK */}
            {!isCheckingApproval && isApproved && (
              <div className="w-full space-y-4 animate-in fade-in slide-in-from-bottom-2">
                <h2 className="text-2xl font-semibold text-pencil font-sketch">
                  {t('apiKeyGate.testerFormApprovedTitle')}
                </h2>
                <p className="text-sm text-gate-text">
                  {t('apiKeyGate.testerFormApprovedDesc')}
                </p>
                <button
                  onClick={() => openExternalUrl("https://play.google.com/store/apps/details?id=com.ronitervo.maestrotutor")}
                  className="w-full bg-gate-btn-bg px-4 py-4 mt-2 text-base font-bold text-gate-btn-text hover:bg-gate-btn-bg/85 sketchy-border-thin transition-colors rounded-md shadow-md"
                >
                  {t('apiKeyGate.testerFormDownloadBtn')}
                </button>
                <button
                  onClick={() => {
                    setLastSubmittedEmail('');
                    localStorage.removeItem(STORAGE_KEY_EMAIL);
                    setIsApproved(false);
                    setViewMode('submit');
                  }}
                  className="mt-4 text-xs text-gate-muted-text hover:text-gate-text underline transition-colors"
                >
                  {t('apiKeyGate.submitAnotherEmail')}
                </button>
              </div>
            )}

            {/* GRANDMA-PROOF STATE 3: PENDING (Submitted, but not ticked in sheet yet) */}
            {!isCheckingApproval && !isApproved && lastSubmittedEmail && (
              <div className="w-full space-y-4 animate-in fade-in slide-in-from-bottom-2">
                <h2 className="text-2xl font-semibold text-gate-text font-sketch">
                  {t('apiKeyGate.testerFormPendingTitle')}
                </h2>
                <div className="p-4 bg-amber-50 text-amber-900 sketchy-border-thin text-sm space-y-2">
                  <p>{t('apiKeyGate.testerFormPendingDesc')}</p>
                  <p className="font-medium opacity-80">{lastSubmittedEmail}</p>
                </div>
                <button
                  onClick={() => {
                    setLastSubmittedEmail('');
                    localStorage.removeItem(STORAGE_KEY_EMAIL);
                    setViewMode('submit');
                  }}
                  className="mt-4 text-xs text-gate-muted-text hover:text-gate-text underline transition-colors"
                >
                  {t('apiKeyGate.submitAnotherEmail')}
                </button>
              </div>
            )}

            {/* GRANDMA-PROOF STATE 4A: FIRST TIME INPUT FORM */}
            {!isCheckingApproval && !isApproved && !lastSubmittedEmail && viewMode === 'submit' && (
              <div className="w-full space-y-2 animate-in fade-in">
                <h2 className="text-2xl font-semibold text-gate-text font-sketch">
                  {t('apiKeyGate.testerFormTitle')}
                </h2>
                <p className="text-sm text-gate-muted-text pb-4">
                  {t('apiKeyGate.testerFormDescription')}
                </p>

                {notFoundError && (
                  <div className="p-3 mb-4 text-sm bg-red-50 text-red-800 sketchy-border-thin">
                    {t('apiKeyGate.testerFormNotFound')}
                  </div>
                )}

                <form
                  className="w-full space-y-4"
                  onSubmit={async (e) => {
                    e.preventDefault();
                    const cleanEmail = testerEmail.trim().toLowerCase();

                    if (!cleanEmail) return;

                    if (submissionCount >= MAX_SUBMISSIONS) {
                      setEmailErrorMsg(t('apiKeyGate.testerFormCapReached'));
                      return;
                    }
                    if (!cleanEmail.endsWith('@gmail.com') && !cleanEmail.endsWith('@googlemail.com')) {
                      setEmailErrorMsg(t('apiKeyGate.testerFormMustBeGmail'));
                      return;
                    }
                    if (cleanEmail.length < 11 || cleanEmail.length > MAX_EMAIL_LENGTH) {
                      setEmailErrorMsg(t('apiKeyGate.testerFormError'));
                      return;
                    }

                    setTesterStatus('submitting');

                    try {
                      const formData = new URLSearchParams();
                      formData.append('email', cleanEmail);
                      formData.append('source', 'web_tester_form');

                      const response = await fetch(GOOGLE_APPS_SCRIPT_URL, {
                        method: 'POST',
                        body: formData,
                      });

                      if (response.ok) {
                        const newCount = submissionCount + 1;
                        localStorage.setItem(STORAGE_KEY_EMAIL, cleanEmail);
                        localStorage.setItem(STORAGE_KEY_SUBMISSION_COUNT, String(newCount));

                        skipNextCheckRef.current = true; // Prevent race condition
                        setNotFoundError(false);
                        setIsApproved(false);
                        setSubmissionCount(newCount);
                        setTesterStatus('idle');
                        setLastSubmittedEmail(cleanEmail);
                      } else {
                        setTesterStatus('error');
                      }
                    } catch (error) {
                      console.error('Form submission failed', error);
                      setTesterStatus('error');
                    }
                  }}
                >
                  <div>
                    <input
                      type="email"
                      required
                      value={testerEmail}
                      onChange={(e) => {
                        setTesterEmail(e.target.value);
                        if (testerStatus === 'error') setTesterStatus('idle');
                        if (emailErrorMsg) setEmailErrorMsg('');
                      }}
                      placeholder="@gmail.com"
                      className="w-full px-3 py-3 text-sm bg-gate-bg text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin sketch-shape-1"
                      maxLength={MAX_EMAIL_LENGTH}
                      autoFocus
                    />
                    {testerStatus === 'error' && (
                      <p className="text-xs text-gate-error-text text-left mt-1">
                        {t('apiKeyGate.testerFormError')}
                      </p>
                    )}
                    {emailErrorMsg && (
                      <p className="text-xs text-amber-600 text-left mt-1">
                        {emailErrorMsg}
                      </p>
                    )}
                  </div>

                  <button
                    type="submit"
                    disabled={testerStatus === 'submitting' || submissionCount >= MAX_SUBMISSIONS}
                    className="w-full bg-gate-btn-bg px-4 py-3 text-sm font-medium text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-50 sketchy-border-thin sketch-shape-5 transition-opacity"
                  >
                    {submissionCount >= MAX_SUBMISSIONS
                      ? t('apiKeyGate.testerFormCapReached')
                      : testerStatus === 'submitting'
                        ? t('apiKeyGate.testerFormSubmitting')
                        : t('apiKeyGate.testerFormSubmit')}
                  </button>
                </form>

                <div className="w-full pt-4 mt-2 border-t border-line-border/40 text-center">
                  <button
                    onClick={() => {
                      setViewMode('check');
                      setNotFoundError(false);
                    }}
                    className="text-xs text-gate-muted-text hover:text-gate-accent transition-colors underline decoration-gate-muted-text/30 underline-offset-2"
                  >
                    {t('apiKeyGate.checkStatusModeBtn')}
                  </button>
                </div>
              </div>
            )}

            {/* GRANDMA-PROOF STATE 4B: CHECK EXISTING STATUS FORM */}
            {!isCheckingApproval && !isApproved && !lastSubmittedEmail && viewMode === 'check' && (
              <div className="w-full space-y-2 animate-in fade-in">
                <h2 className="text-2xl font-semibold text-gate-text font-sketch">
                  {t('apiKeyGate.checkStatusTitle')}
                </h2>
                <p className="text-sm text-gate-muted-text pb-4">
                  {t('apiKeyGate.checkStatusDesc')}
                </p>

                <form
                  className="w-full space-y-4"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const cleanEmail = checkEmailInput.trim().toLowerCase();
                    if (!cleanEmail) return;

                    skipNextCheckRef.current = false; // Make sure it fetches from network
                    setNotFoundError(false);
                    setLastSubmittedEmail(cleanEmail); // Triggers useEffect
                  }}
                >
                  <input
                    type="email"
                    required
                    value={checkEmailInput}
                    onChange={(e) => setCheckEmailInput(e.target.value)}
                    placeholder="@gmail.com"
                    className="w-full px-3 py-3 text-sm bg-gate-bg text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin sketch-shape-9"
                    autoFocus
                  />
                  <button
                    type="submit"
                    className="w-full bg-gate-btn-bg px-4 py-3 text-sm font-medium text-gate-btn-text hover:bg-gate-btn-bg/80 sketchy-border-thin sketch-shape-11 transition-opacity"
                  >
                    {t('apiKeyGate.checkStatusSubmit')}
                  </button>
                </form>

                <div className="w-full pt-4 mt-2 border-t border-line-border/40 text-center">
                  <button
                    onClick={() => {
                      setViewMode('submit');
                      setNotFoundError(false);
                    }}
                    className="text-xs text-gate-muted-text hover:text-gate-accent transition-colors underline decoration-gate-muted-text/30 underline-offset-2"
                  >
                    {t('apiKeyGate.submitModeBtn')}
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Developer / API Key Escape Hatch */}
          <div className="absolute bottom-2 right-2 flex flex-col items-end gap-1">
            {!showDevPassword ? (
              <button
                type="button"
                onClick={() => setShowDevPassword(true)}
                className="text-[10px] text-gate-muted-text/30 hover:text-gate-muted-text transition-colors px-2 py-1"
              >
                {t('apiKeyGate.developerLogin')}
              </button>
            ) : (
              <input
                type="password"
                placeholder="password"
                value={devPasswordInput}
                onChange={(e) => {
                  const val = e.target.value;
                  setDevPasswordInput(val);
                  if (val === DEV_PASSWORD) {
                    setShowTesterForm(false);
                    setShowDevPassword(false);
                    setDevPasswordInput('');
                  }
                }}
                onBlur={() => {
                  if (!devPasswordInput) setShowDevPassword(false);
                }}
                className="w-24 px-2 py-1 text-[10px] bg-gate-bg text-gate-text sketchy-border-thin focus:outline-none"
                autoFocus
              />
            )}
          </div>
        </div>
      </div>
    );
  }

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

