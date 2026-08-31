// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useState } from 'react';
import type { ManagedAccessSession } from '../core/contracts/backend';
import { googleAuthService } from '../services/auth/googleAuthService';
import { maestroBackendService } from '../services/backend/maestroBackendService';
import { maestroFirebaseService } from '../services/firebase/maestroFirebaseService';
import { SmallSpinner } from '../shared/ui/SmallSpinner';

const DeleteAccountPage: React.FC = () => {
  const [session, setSession] = useState<ManagedAccessSession | null>(null);
  const [isBooting, setIsBooting] = useState(true);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [confirmationText, setConfirmationText] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const isConfigured = maestroBackendService.isConfigured() && maestroFirebaseService.isConfigured();

  useEffect(() => {
    let isMounted = true;

    const restoreSession = async () => {
      if (!isConfigured) {
        if (isMounted) {
          setErrorMessage('Managed account deletion is not configured on this deployment.');
          setIsBooting(false);
        }
        return;
      }

      try {
        const restored = await googleAuthService.restoreManagedSession();
        if (isMounted) {
          setSession(restored);
        }
      } catch (error) {
        if (isMounted) {
          setErrorMessage(error instanceof Error ? error.message : 'Failed to restore your managed session.');
        }
      } finally {
        if (isMounted) {
          setIsBooting(false);
        }
      }
    };

    void restoreSession();
    return () => {
      isMounted = false;
    };
  }, [isConfigured]);

  const handleSignIn = useCallback(async () => {
    setIsSigningIn(true);
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      const nextSession = await googleAuthService.beginSignIn();
      setSession(nextSession);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Google sign-in failed.');
    } finally {
      setIsSigningIn(false);
    }
  }, []);

  const handleSignOut = useCallback(async () => {
    setErrorMessage(null);
    setSuccessMessage(null);
    await googleAuthService.signOutManagedSession();
    setSession(null);
  }, []);

  const handleDeleteAccount = useCallback(async () => {
    if (!session?.firebaseIdToken) {
      setErrorMessage('Sign in first.');
      return;
    }
    if (confirmationText.trim().toUpperCase() !== 'DELETE') {
      setErrorMessage('Type DELETE to confirm account deletion.');
      return;
    }

    setIsDeleting(true);
    setErrorMessage(null);
    setSuccessMessage(null);
    try {
      await maestroBackendService.deleteManagedAccount();
      await googleAuthService.signOutManagedSession();
      setSession(null);
      setConfirmationText('');
      setSuccessMessage('Your managed Maestro account has been deleted.');
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Failed to delete your managed account.');
    } finally {
      setIsDeleting(false);
    }
  }, [confirmationText, session?.firebaseIdToken]);

  return (
    <div className="min-h-screen bg-page-bg paper-texture px-4 py-10 text-page-text">
      <div className="mx-auto w-full max-w-2xl space-y-4 bg-paper-surface p-6 shadow-xl sketchy-border-thin">
        <div className="space-y-2">
          <div className="text-2xl font-sketch font-semibold">Delete Managed Account</div>
          <p className="text-sm text-page-text/80">
            This page deletes your managed Maestro account for this app. It is intended for Google Play
            account-deletion compliance and uses the same backend deletion flow as the in-app managed access panel.
          </p>
        </div>

        <div className="rounded-md border border-line-border bg-page-bg px-4 py-3 text-sm text-page-text/85">
          <div className="font-medium">What this deletes</div>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            <li>Your Firebase-authenticated managed account for this app.</li>
            <li>Your managed billing summary, entitlements, and managed usage/billing ledgers stored in Firestore.</li>
            <li>Your managed upload records and current managed session cache.</li>
          </ul>
          <div className="mt-3 font-medium">What may remain in anonymized form</div>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            <li>Processed Google Play purchase token records needed to prevent fraud and duplicate credit grants.</li>
            <li>Generated-content safety reports if you previously submitted one.</li>
          </ul>
          <div className="mt-3 font-medium">What this does not delete</div>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            <li>BYOK API keys, local chats, or local usage history stored on another device.</li>
            <li>Google Play payment records held by Google.</li>
          </ul>
        </div>

        <div className="text-sm">
          <a href="/privacy.html" className="text-blue-700 hover:underline">
            View privacy policy
          </a>
        </div>

        {isBooting && (
          <div className="flex items-center gap-3 rounded-md border border-line-border bg-page-bg px-4 py-3 text-sm">
            <SmallSpinner className="h-5 w-5 text-loading-spinner" />
            <span>Checking your managed session...</span>
          </div>
        )}

        {successMessage && (
          <div className="rounded-md border border-emerald-300 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
            {successMessage}
          </div>
        )}

        {errorMessage && (
          <div className="rounded-md border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
            {errorMessage}
          </div>
        )}

        {!isBooting && !session?.firebaseIdToken && (
          <div className="space-y-3">
            <p className="text-sm text-page-text/80">
              Sign in with the Google account you used for managed Maestro access, then confirm deletion.
            </p>
            <button
              type="button"
              onClick={() => void handleSignIn()}
              disabled={!isConfigured || isSigningIn}
              className="bg-gate-btn-bg px-4 py-2 text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 sketchy-border-thin"
            >
              {isSigningIn ? 'Signing in...' : 'Sign in with Google'}
            </button>
          </div>
        )}

        {!isBooting && session?.firebaseIdToken && (
          <div className="space-y-3">
            <div className="rounded-md border border-line-border bg-page-bg px-4 py-3 text-sm">
              <div className="font-medium">Signed in account</div>
              <div className="mt-1 text-page-text/80">{session.user.email || session.user.id}</div>
            </div>

            <div className="space-y-2">
              <label className="block text-sm font-medium" htmlFor="delete-account-confirm">
                Type <code>DELETE</code> to confirm
              </label>
              <input
                id="delete-account-confirm"
                type="text"
                value={confirmationText}
                onChange={(event) => setConfirmationText(event.target.value)}
                placeholder="DELETE"
                className="w-full border border-line-border bg-page-bg px-3 py-2 text-sm outline-none"
                disabled={isDeleting}
              />
            </div>

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => void handleSignOut()}
                disabled={isDeleting}
                className="px-4 py-2 text-page-text hover:bg-page-bg sketchy-border-thin"
              >
                Sign out
              </button>
              <button
                type="button"
                onClick={() => void handleDeleteAccount()}
                disabled={isDeleting || confirmationText.trim().toUpperCase() !== 'DELETE'}
                className="bg-red-700 px-4 py-2 text-white hover:bg-red-800 disabled:opacity-60 sketchy-border-thin"
              >
                {isDeleting ? 'Deleting account...' : 'Delete managed account'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default DeleteAccountPage;
