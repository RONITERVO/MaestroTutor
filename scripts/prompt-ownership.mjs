// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFileSync, readdirSync } from 'node:fs';
import { resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

// This is a guardrail, not a semantic proof. Provider payload snapshots remain
// the authority for wording, roles, history ordering and generation settings.
const instructionLanguage = /\b(?:you are (?:a |an |Maestro)|smart parrot|return only (?:the |a |valid )|text to read:|instrumental only|this is you Gemini|earlier assistant turn (?:artifact|used)|current conversation context|sketchbook reference)/i;
const promptName = /(?:prompt|instruction|instructionSuffix|systemInstructionForGemini|systemInstructionText|augmentedSystemInstruction)$/i;
const naturalText = value => /\p{L}+[\s,:]+\p{L}+/u.test(value) || value === '...';

export function inlinePromptViolations(source, fileName = 'input.ts') {
  const ast = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true);
  const violations = [];
  const flag = (node, reason) => violations.push({
    file: fileName, line: ast.getLineAndCharacterOfPosition(node.getStart(ast)).line + 1, reason,
  });
  const visit = node => {
    const literal = ts.isStringLiteralLike(node) || ts.isTemplateHead(node) || ts.isTemplateMiddle(node) || ts.isTemplateTail(node);
    if (literal && instructionLanguage.test(node.text)) flag(node, 'Authored instruction belongs in shared/prompts.');
    if (literal && naturalText(node.text)) {
      let parent = node.parent;
      // Walk through expressions, but do not confuse a call's data/lookup keys,
      // an error message or a function body with a prompt assignment.
      while (parent && !ts.isStatement(parent) && !ts.isCallExpression(parent) && !ts.isFunctionLike(parent)) {
        const name = ts.isVariableDeclaration(parent) || ts.isPropertyAssignment(parent) ? parent.name.getText(ast)
          : ts.isBinaryExpression(parent) ? parent.left.getText(ast).split('.').at(-1) : '';
        if (promptName.test(name.replace(/^['"]|['"]$/g, ''))) {
          flag(node, 'Inline prompt text/suffix must use the shared catalogue.');
          break;
        }
        parent = parent.parent;
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(ast);
  return violations;
}

export function auditPromptOwnership(root = fileURLToPath(new URL('../', import.meta.url))) {
  const violations = [];
  const walk = directory => {
    for (const entry of readdirSync(resolve(root, directory), { withFileTypes: true })) {
      const file = `${directory}/${entry.name}`;
      if (entry.isDirectory()) { if (!['node_modules', '__snapshots__', 'i18n'].includes(entry.name)) walk(file); continue; }
      if (!/\.tsx?$/.test(file) || /\.(test|spec)\./.test(file)) continue;
      if (file.startsWith('shared/prompts/')) continue;
      // Deliberately synthetic test instructions; never used as app defaults.
      if (file === 'src/headless/firstLessonJourney.ts') continue;
      violations.push(...inlinePromptViolations(readFileSync(resolve(root, file), 'utf8'), file));
    }
  };
  for (const directory of ['src', 'shared', 'functions/src', 'live-gateway/src']) walk(directory);
  return violations;
}

if (process.argv[1] && relative(fileURLToPath(new URL('.', import.meta.url)), resolve(process.argv[1])) === 'prompt-ownership.mjs') {
  const violations = auditPromptOwnership();
  if (violations.length) { console.error(JSON.stringify(violations, null, 2)); process.exitCode = 1; }
  else console.log('Prompt ownership check passed.');
}
