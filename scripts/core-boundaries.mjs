// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFileSync, readdirSync } from 'node:fs';
import { resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

const browserGlobals = new Set(['window', 'document', 'navigator', 'localStorage', 'sessionStorage',
  'indexedDB', 'AudioContext', 'webkitAudioContext', 'AudioWorkletNode', 'MediaRecorder', 'FileReader']);
const browserPackage = /^(?:react(?:-dom)?(?:\/|$)|zustand(?:\/|$)|@capacitor(?:-firebase)?\/|firebase(?:\/|$))/;
const browserPath = /^src\/(?:api|features|platform|store|services|headless|app)\//;
const normalize = path => path.replaceAll('\\', '/');
const productionFile = file => /\.[cm]?[jt]sx?$/.test(file) && !/\.(?:test|spec)\./.test(file) && !file.endsWith('.d.ts');

/** Structural guard: inspect runtime syntax, including lazy imports, without reading authored strings as code. */
export function inspectCoreSource(source, file = 'input.ts') {
  const ast = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true);
  const imports = [];
  const violations = [];
  const flag = (node, reason) => violations.push({ file, line: ast.getLineAndCharacterOfPosition(node.getStart(ast)).line + 1, reason });
  const dependency = (node, specifier) => {
    if (!specifier || !ts.isStringLiteralLike(specifier)) { flag(node, 'Computed module loading cannot be checked by the Core boundary guard.'); return; }
    imports.push({ specifier: specifier.text, line: ast.getLineAndCharacterOfPosition(node.getStart(ast)).line + 1 });
  };
  const visit = node => {
    // A DOM-shaped type does not load or access a browser implementation.
    if (ts.isTypeNode(node) || ts.isInterfaceDeclaration(node)) return;
    if (ts.isImportDeclaration(node)) {
      const clause = node.importClause;
      const named = clause?.namedBindings;
      const onlyTypes = clause?.isTypeOnly || (!clause?.name && named && ts.isNamedImports(named)
        && named.elements.length > 0 && named.elements.every(item => item.isTypeOnly));
      if (!onlyTypes) dependency(node, node.moduleSpecifier);
      return;
    }
    if (ts.isExportDeclaration(node)) {
      const onlyTypes = node.isTypeOnly || (node.exportClause && ts.isNamedExports(node.exportClause)
        && node.exportClause.elements.length > 0 && node.exportClause.elements.every(item => item.isTypeOnly));
      if (node.moduleSpecifier && !onlyTypes) dependency(node, node.moduleSpecifier);
      return;
    }
    if (ts.isCallExpression(node) && (node.expression.kind === ts.SyntaxKind.ImportKeyword
      || (ts.isIdentifier(node.expression) && node.expression.text === 'require'))) dependency(node, node.arguments[0]);
    if (ts.isImportEqualsDeclaration(node) && ts.isExternalModuleReference(node.moduleReference) && !node.isTypeOnly) {
      dependency(node, node.moduleReference.expression);
    }
    if (ts.isIdentifier(node) && browserGlobals.has(node.text)) {
      const parent = node.parent;
      const dataName = ((ts.isPropertyAssignment(parent) || ts.isPropertyDeclaration(parent)
        || ts.isMethodDeclaration(parent)) && parent.name === node)
        || (ts.isPropertyAccessExpression(parent) && parent.name === node && parent.expression.getText(ast) !== 'globalThis');
      if (!dataName) flag(node, `Browser runtime reference: ${node.text}`);
    }
    if (ts.isElementAccessExpression(node) && node.expression.getText(ast) === 'globalThis'
      && ts.isStringLiteralLike(node.argumentExpression) && browserGlobals.has(node.argumentExpression.text)) {
      flag(node, `Browser runtime reference: ${node.argumentExpression.text}`);
    }
    ts.forEachChild(node, visit);
  };
  visit(ast);
  return { imports, violations };
}

export function auditCoreBoundaries(root = fileURLToPath(new URL('../', import.meta.url))) {
  const violations = [];
  const visited = new Set();
  const options = { moduleResolution: ts.ModuleResolutionKind.Bundler, baseUrl: root, paths: { '@/*': ['src/*'] }, allowJs: true };
  const visit = (absolute, chain) => {
    if (visited.has(absolute)) return;
    visited.add(absolute);
    const file = normalize(relative(root, absolute));
    const inspected = inspectCoreSource(readFileSync(absolute, 'utf8'), file);
    violations.push(...inspected.violations.map(item => ({ ...item, chain })));
    for (const item of inspected.imports) {
      const flag = reason => violations.push({ file, line: item.line, reason, chain });
      if (browserPackage.test(item.specifier)) { flag(`Browser package: ${item.specifier}`); continue; }
      if (!item.specifier.startsWith('.') && !item.specifier.startsWith('@/')) continue;
      const resolved = ts.resolveModuleName(item.specifier, absolute, options, ts.sys).resolvedModule?.resolvedFileName;
      if (!resolved) { flag(`Unresolved local dependency: ${item.specifier}`); continue; }
      const target = normalize(relative(root, resolved));
      if (browserPath.test(target)) { flag(`Core reaches adapter: ${target}`); continue; }
      if (productionFile(target)) visit(resolve(resolved), [...chain, target]);
    }
  };
  const walk = directory => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const file = resolve(directory, entry.name);
      if (entry.isDirectory()) { if (entry.name !== '__snapshots__') walk(file); }
      else if (productionFile(file)) visit(file, [normalize(relative(root, file))]);
    }
  };
  walk(resolve(root, 'src/core-sdk'));
  return violations;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const violations = auditCoreBoundaries();
  if (violations.length) { console.error(JSON.stringify(violations, null, 2)); process.exitCode = 1; }
  else console.log('Core runtime boundary check passed.');
}
