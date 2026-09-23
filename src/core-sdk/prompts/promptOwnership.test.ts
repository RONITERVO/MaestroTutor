// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFileSync, readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
// @ts-expect-error Development-only JavaScript source scanner, also usable as a CLI.
import { auditPromptOwnership, inlinePromptViolations } from '../../../scripts/prompt-ownership.mjs';
import ts from 'typescript';

describe('prompt ownership', () => {
  it('keeps authored production instructions in the shared catalogue', () => {
    expect(auditPromptOwnership()).toEqual([]);
  });

  it.each([
    'const systemInstruction = "Speak warmly and use short sentences.";',
    'prompt += "Please add a different instruction.";',
    'const x = { systemInstruction: `Always speak ${language} first.` };',
    'const hidden = "You are a helpful assistant.";',
    'const x = { instructionSuffix: "Wait before speaking again." };',
  ])('rejects an inline instruction: %s', source => {
    expect(inlinePromptViolations(source).length).toBeGreaterThan(0);
  });

  it('permits dynamic data, catalogue composition, empty fallbacks and developer comments', () => {
    expect(inlinePromptViolations(`
      // You are a helpful assistant is an example in a developer comment.
      const prompt = buildTranslationPrompt(text, from, to);
      systemInstruction = provided || '';
      const request = { prompt: user.text, systemInstruction: instructions };
      const message = 'No user input is available.';
    `)).toEqual([]);
  });

  it('keeps the catalogue runtime-independent and usable by Functions', () => {
    const rootDirectory = new URL('../../../shared/prompts/', import.meta.url);
    for (const file of readdirSync(rootDirectory).filter(name => name.endsWith('.ts'))) {
      const source = readFileSync(new URL(file, rootDirectory), 'utf8');
      const ast = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true);
      for (const statement of ast.statements) {
        if (ts.isImportDeclaration(statement) || ts.isExportDeclaration(statement)) {
          if (statement.moduleSpecifier) expect((statement.moduleSpecifier as ts.StringLiteral).text).toMatch(/^\.\/[a-z]+$/);
        }
      }
      expect(source).not.toMatch(/\b(?:window|document|process)\./);
    }
  });
});
