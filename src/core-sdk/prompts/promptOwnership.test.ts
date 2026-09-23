// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFileSync, readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
// @ts-expect-error Development-only JavaScript source scanner, also usable as a CLI.
import { auditPromptOwnership, catalogueRuntimeViolations, inlinePromptViolations } from '../../../scripts/prompt-ownership.mjs';
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
    'setSystemInstruction("Please speak clearly and slowly.");',
    'session.setPrompt(`Please speak ${language} first.`);',
    'const hidden = "Text to read: hello";',
    'const hidden = "Instrumental only.";',
    'const hidden = "This is you Gemini.";',
    'const hidden = "Earlier assistant turn artifact omitted.";',
    'const hidden = "Earlier assistant turn used a tool.";',
    'const hidden = "Current conversation context:";',
    'const hidden = "Sketchbook reference:";',
  ])('rejects an inline instruction: %s', source => {
    expect(inlinePromptViolations(source).length).toBeGreaterThan(0);
  });

  it('reports a literal matching both wording and a prompt name only once', () => {
    expect(inlinePromptViolations('const prompt = "You are a helpful assistant.";')).toHaveLength(1);
  });

  it('permits dynamic data, catalogue composition, empty fallbacks and developer comments', () => {
    expect(inlinePromptViolations(`
      // You are a helpful assistant is an example in a developer comment.
      const prompt = buildTranslationPrompt(text, from, to);
      systemInstruction = provided || '';
      const request = { prompt: user.text, systemInstruction: instructions };
      const message = 'No user input is available.';
      const promptId = lookup('No user input is available.');
      setSystemInstruction(instructions);
    `)).toEqual([]);
  });

  it.each([
    'const x = window;',
    'const x = document;',
    'const x = process;',
    'const x = window.navigator;',
    'const x = globalThis.window;',
    'const x = navigator.language;',
    'const x = localStorage;',
    'const x = sessionStorage;',
  ])('rejects runtime-specific catalogue dependencies: %s', source => {
    expect(catalogueRuntimeViolations(source).length).toBeGreaterThan(0);
  });

  it('permits runtime names in authored examples and plain data keys', () => {
    expect(catalogueRuntimeViolations('const example = "window.document"; const data = { window: "example" };')).toEqual([]);
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
      expect(catalogueRuntimeViolations(source, file)).toEqual([]);
    }
  });
});
