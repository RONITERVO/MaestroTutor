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

  it('keeps the native vocabulary runtime dependent only on generated data', () => {
    const source = readFileSync(new URL('../../../shared/behaviourCatalog.ts', import.meta.url), 'utf8');
    const ast = ts.createSourceFile('behaviourCatalog.ts', source, ts.ScriptTarget.Latest, true);
    const imports = ast.statements.filter(ts.isImportDeclaration);
    expect(imports.filter(node=>!node.importClause?.isTypeOnly).map(node=>(node.moduleSpecifier as ts.StringLiteral).text)).toEqual(['./generated/behaviourCatalog.json']);
    expect(imports.filter(node=>node.importClause?.isTypeOnly).map(node=>(node.moduleSpecifier as ts.StringLiteral).text)).toEqual(['./programValues','./capabilities']);
    expect(catalogueRuntimeViolations(source, 'behaviourCatalog.ts')).toEqual([]);
  });

  it('keeps capability validation independent of browsers and native code', () => {
    const source = readFileSync(new URL('../../../shared/capabilities.ts', import.meta.url), 'utf8');
    const ast = ts.createSourceFile('capabilities.ts', source, ts.ScriptTarget.Latest, true);
    const imports = ast.statements.filter(ts.isImportDeclaration).map(node => (node.moduleSpecifier as ts.StringLiteral).text);
    expect(imports).toEqual(['./surfaceGeometry','./roomConnection','./creationPrototype','./creationBatch','./programValues','./collisionRecipe','./programModuleIdentity','./roomRecipe','./behaviourCatalog']);
    for (const name of ['creationBatch', 'creationPrototype', 'programValues', 'roomConnection', 'surfaceGeometry']) {
      const dependency = readFileSync(new URL('../../../shared/' + name + '.ts', import.meta.url), 'utf8');
      const dependencyAst = ts.createSourceFile(name + '.ts', dependency, ts.ScriptTarget.Latest, true);
      expect(dependencyAst.statements.filter(ts.isImportDeclaration).map(node=>(node.moduleSpecifier as ts.StringLiteral).text)).toEqual(name==='creationBatch'?['./roomConnection','./creationPrototype']:name==='creationPrototype'?['./surfaceGeometry','./roomConnection']:[]);
      expect(catalogueRuntimeViolations(dependency, name + '.ts')).toEqual([]);
    }
    const identity = readFileSync(new URL('../../../shared/programModuleIdentity.ts', import.meta.url), 'utf8');
    expect(catalogueRuntimeViolations(identity, 'programModuleIdentity.ts')).toEqual([]);
    const recipe = readFileSync(new URL('../../../shared/roomRecipe.ts', import.meta.url), 'utf8');
    const recipeAst = ts.createSourceFile('roomRecipe.ts', recipe, ts.ScriptTarget.Latest, true);
    expect(recipeAst.statements.filter(ts.isImportDeclaration)).toEqual([]);
    expect(catalogueRuntimeViolations(recipe, 'roomRecipe.ts')).toEqual([]);
    const collision = readFileSync(new URL('../../../shared/collisionRecipe.ts', import.meta.url), 'utf8');
    const collisionAst = ts.createSourceFile('collisionRecipe.ts', collision, ts.ScriptTarget.Latest, true);
    expect(collisionAst.statements.filter(ts.isImportDeclaration).map(node=>(node.moduleSpecifier as ts.StringLiteral).text)).toEqual(['./roomRecipe']);
    expect(catalogueRuntimeViolations(collision, 'collisionRecipe.ts')).toEqual([]);
    expect(catalogueRuntimeViolations(source, 'capabilities.ts')).toEqual([]);
  });

  it('keeps event validation dependent only on the native manifest and pure shared schema helpers', () => {
    const source = readFileSync(new URL('../../../shared/behaviourEvents.ts', import.meta.url), 'utf8');
    const ast = ts.createSourceFile('behaviourEvents.ts', source, ts.ScriptTarget.Latest, true);
    const imports = ast.statements.filter(ts.isImportDeclaration);
    expect(imports.map(node => (node.moduleSpecifier as ts.StringLiteral).text)).toEqual(['./behaviourCatalog','./capabilities']);
    const bindings=imports[1].importClause?.namedBindings;
    expect(bindings&&ts.isNamedImports(bindings)&&bindings.elements.filter(node=>!node.isTypeOnly).map(node=>node.name.text)).toEqual(['validateCapabilityValue','schemaField']);
    expect(catalogueRuntimeViolations(source, 'behaviourEvents.ts')).toEqual([]);
  });

  it('keeps shared room task ceilings free of runtime-specific dependencies', () => {
    const source = readFileSync(new URL('../../../shared/roomTaskBudget.ts', import.meta.url), 'utf8');
    const ast = ts.createSourceFile('roomTaskBudget.ts', source, ts.ScriptTarget.Latest, true);
    expect(ast.statements.filter(node => ts.isImportDeclaration(node) || ts.isExportDeclaration(node))).toEqual([]);
    expect(catalogueRuntimeViolations(source, 'roomTaskBudget.ts')).toEqual([]);
  });

  it('keeps command field vocabulary dependent only on pure control definitions', () => {
    const source = readFileSync(new URL('../../../shared/roomCommandFields.ts', import.meta.url), 'utf8');
    const ast = ts.createSourceFile('roomCommandFields.ts', source, ts.ScriptTarget.Latest, true);
    expect(ast.statements.filter(ts.isImportDeclaration).map(node => (node.moduleSpecifier as ts.StringLiteral).text)).toEqual(['./prompts/roomcontrols']);
    expect(catalogueRuntimeViolations(source, 'roomCommandFields.ts')).toEqual([]);
  });

  it('keeps the catalogue runtime-independent and usable by Functions', () => {
    const rootDirectory = new URL('../../../shared/prompts/', import.meta.url);
    for (const file of readdirSync(rootDirectory).filter(name => name.endsWith('.ts'))) {
      const source = readFileSync(new URL(file, rootDirectory), 'utf8');
      const ast = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true);
      for (const statement of ast.statements) {
        if (ts.isImportDeclaration(statement) || ts.isExportDeclaration(statement)) {
          if (ts.isImportDeclaration(statement) && statement.importClause?.isTypeOnly && (statement.moduleSpecifier as ts.StringLiteral).text === '../roomViewCapture') continue;
          if (statement.moduleSpecifier) expect((statement.moduleSpecifier as ts.StringLiteral).text).toMatch(/^(?:\.\/[a-z]+|\.\.\/(?:behaviourCatalog|behaviourEvents|capabilities|roomTaskBudget|roomCommandFields))$/);
        }
      }
      expect(catalogueRuntimeViolations(source, file)).toEqual([]);
    }
  });
});
