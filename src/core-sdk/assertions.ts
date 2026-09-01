// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface CoreAssertion {
  name: string;
  passed: boolean;
  message: string;
  actual?: unknown;
}

export interface CoreAssertionReport {
  passed: boolean;
  assertions: CoreAssertion[];
}

export const createAssertionReport = (assertions: CoreAssertion[]): CoreAssertionReport => ({
  passed: assertions.every(assertion => assertion.passed),
  assertions,
});

export const assertTutorTurnInvariants = (result: {
  rawResponse: string;
  translations: Array<{ target: string; native: string }>;
}): CoreAssertionReport => createAssertionReport([
  {
    name: 'response.nonEmpty',
    passed: result.rawResponse.trim().length > 0,
    message: 'The tutor returns non-empty model output.',
    actual: result.rawResponse.length,
  },
  {
    name: 'response.bilingualPairs',
    passed: result.translations.length > 0,
    message: 'The tutor output contains at least one target/native translation pair.',
    actual: result.translations.length,
  },
  {
    name: 'response.completePairs',
    passed: result.translations.every(pair => Boolean(pair.target.trim() && pair.native.trim())),
    message: 'Every parsed translation pair has both target and native text.',
  },
]);
