// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Offline API compatibility across a narrowly overridden transitive dependency.
// Security acceptance is the separate production audit, not these filesystem tests.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { dirname, basename, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { existsSync, lstatSync, readFileSync, realpathSync } from 'node:fs';
import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises';

function beneath(parent, candidate) {
  const path = relative(parent, candidate);
  return path !== '' && path !== '..' && !path.startsWith(`..${sep}`) && !isAbsolute(path);
}

function installedPackage(name, requireFromParent, installation) {
  const entry = realpathSync(requireFromParent.resolve(name));
  assert.ok(beneath(installation, entry), `${name} must resolve inside this backend's installation`);
  let directory = dirname(entry);
  while (beneath(installation, directory)) {
    const manifestPath = join(directory, 'package.json');
    if (existsSync(manifestPath)) {
      const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
      if (manifest.name === name) return {
        directory, entry, manifest, require: createRequire(pathToFileURL(entry)),
      };
    }
    directory = dirname(directory);
  }
  assert.fail(`Cannot locate the installed manifest for ${name}`);
}

function firebaseDeletionChain(backend) {
  const installation = realpathSync(join(backend, 'node_modules'));
  assert.ok(beneath(backend, installation), 'Backend dependencies must not resolve through another checkout');
  const ownRequire = createRequire(pathToFileURL(join(backend, 'package.json')));
  const admin = installedPackage('firebase-admin', ownRequire, installation);
  const firestore = installedPackage('@google-cloud/firestore', admin.require, installation);
  let gax = installedPackage('google-gax', firestore.require, installation);
  const chain = [admin, firestore];
  if (!gax.manifest.version.startsWith('5.')) {
    assert.ok(firestore.manifest.dependencies?.['@google-cloud/firestore-api'],
      'Expected the supported Firestore API branch leading to gax 5');
    const api = installedPackage('@google-cloud/firestore-api', firestore.require, installation);
    chain.push(api);
    gax = installedPackage('google-gax', api.require, installation);
  }
  assert.match(gax.manifest.version, /^5\./, 'The gax 5 subtree under repair must exist; never silently skip it');
  assert.ok(gax.manifest.dependencies?.rimraf, 'The reviewed gax branch must declare rimraf');
  const rimraf = installedPackage('rimraf', gax.require, installation);
  assert.match(rimraf.manifest.version, /^5\./, 'This repair preserves rimraf 5');
  const glob = installedPackage('glob', rimraf.require, installation);
  const minimatch = installedPackage('minimatch', glob.require, installation);
  return { chain: [...chain, gax, rimraf, glob, minimatch], gax, rimraf, glob };
}

async function importBranch(pkg) {
  // Load the package's actual advertised ESM entry, not its CJS entry through import().
  const branch = pkg.manifest.exports?.['.']?.import;
  const target = typeof branch === 'string' ? branch : branch?.default;
  assert.ok(typeof target === 'string' && target.startsWith('./'), `${pkg.manifest.name} needs an explicit ESM export`);
  const entry = realpathSync(resolve(pkg.directory, target));
  assert.ok(beneath(pkg.directory, entry), 'Published ESM entry must stay inside its package');
  return import(pathToFileURL(entry).href);
}

function ownedFixture(backend, root) {
  assert.equal(dirname(root), backend);
  assert.match(basename(root), /^\.dependency-compat-[A-Za-z0-9]+$/);
  assert.ok(beneath(backend, root));
  assert.equal(realpathSync(backend), backend);
  assert.equal(realpathSync(root), root);
  const stat = lstatSync(root);
  assert.ok(stat.isDirectory() && !stat.isSymbolicLink(), 'Fixture root must remain an owned real directory');
}

function fixturePath(root, path) {
  const full = resolve(root, path);
  assert.ok(beneath(root, full), 'Test file operations must stay strictly inside the unique fixture');
  return full;
}

async function inventory(root, prefix = '') {
  const files = {};
  for (const entry of await readdir(join(root, prefix), { withFileTypes: true })) {
    assert.ok(!entry.isSymbolicLink(), 'Compatibility fixtures never follow links');
    const path = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) Object.assign(files, await inventory(root, path));
    else {
      assert.ok(entry.isFile(), 'Unexpected fixture entry type');
      files[path] = (await readFile(fixturePath(root, path))).toString('base64');
    }
  }
  return Object.fromEntries(Object.entries(files).sort(([a], [b]) => a.localeCompare(b)));
}

async function withFixture(backend, body) {
  // mkdtemp owns this new directory only; cleanup never enumerates paths for another shell.
  const root = await mkdtemp(join(backend, '.dependency-compat-'));
  try {
    ownedFixture(backend, root);
    await body(root);
  } finally {
    ownedFixture(backend, root);
    await inventory(root); // Refuse unexpected links/types before any recursive cleanup.
    await rm(root, { recursive: true, force: true });
  }
}

async function populate(root, files) {
  for (const [path, content] of Object.entries(files)) {
    const full = fixturePath(root, path);
    await mkdir(dirname(full), { recursive: true });
    await writeFile(full, content);
  }
}

const selectedFiles = {
  'nested dir/first.remove': 'first removable bytes',
  'nested dir/child space/second.remove': 'second removable bytes',
  'top remove.txt': 'third removable bytes',
};
const retainedFiles = {
  'nested dir/first.keep': 'retain sibling bytes',
  'elsewhere/third.remove': 'retain unrelated matching extension',
  'keep space.txt': 'retain root bytes',
};
const patterns = ['nested dir/**/*.remove', 'top remove.txt'];

export async function testBackendDependencyCompatibility(t, backendPath) {
  const backend = realpathSync(backendPath);
  const resolved = firebaseDeletionChain(backend);
  t.diagnostic('Actual installed chain: ' + resolved.chain.map(pkg =>
    `${pkg.manifest.name}@${pkg.manifest.version} (${relative(backend, pkg.directory).split(sep).join('/')})`).join(' -> '));
  const modules = [
    { name: 'CommonJS', rimraf: resolved.gax.require('rimraf'), glob: resolved.rimraf.require('glob') },
    { name: 'ESM', rimraf: await importBranch(resolved.rimraf), glob: await importBranch(resolved.glob) },
  ];
  await t.test('the actual Firebase dependency branch exposes both module APIs', () => {
    for (const module of modules) {
      for (const name of ['rimraf', 'rimrafSync']) assert.equal(typeof module.rimraf[name], 'function');
      for (const name of ['glob', 'globSync']) assert.equal(typeof module.glob[name], 'function');
    }
  });

  for (const module of modules) {
    await t.test(`${module.name}: glob/globSync return exact absolute file names without changing content`, async () => {
      await withFixture(backend, async root => {
        await populate(root, { ...selectedFiles, ...retainedFiles });
        const before = await inventory(root);
        const options = { cwd: root, absolute: true, withFileTypes: false };
        const expected = Object.keys(selectedFiles).map(path => fixturePath(root, path)).sort();
        for (const found of [await module.glob.glob(patterns, options), module.glob.globSync(patterns, options)]) {
          assert.ok(found.every(value => typeof value === 'string' && isAbsolute(value)));
          const actual = found.map(path => {
            const full = resolve(path); assert.ok(beneath(root, full)); return full;
          }).sort();
          assert.deepEqual(actual, expected);
        }
        assert.deepEqual(await inventory(root), before);
      });
    });

    for (const sync of [false, true]) {
      await t.test(`${module.name}: ${sync ? 'sync' : 'async'} literal deletion preserves glob-like siblings`, async () => {
        await withFixture(backend, async root => {
          const retained = { 'literal x/sentinel.txt': 'do not delete', 'outside stay.txt': 'untouched' };
          await populate(root, { 'literal [x]/nested space/remove.txt': 'remove me', ...retained });
          const expected = Object.fromEntries(Object.entries(await inventory(root)).filter(([path]) => !path.startsWith('literal [x]/')));
          const target = fixturePath(root, 'literal [x]');
          ownedFixture(backend, root);
          const result = sync ? module.rimraf.rimrafSync(target, { glob: false }) : await module.rimraf.rimraf(target, { glob: false });
          assert.equal(result, true);
          assert.equal(existsSync(target), false);
          assert.deepEqual(await inventory(root), expected);
        });
      });

      await t.test(`${module.name}: ${sync ? 'sync' : 'async'} glob deletion preserves every nonmatch`, async () => {
        await withFixture(backend, async root => {
          await populate(root, { ...selectedFiles, ...retainedFiles });
          const expected = Object.fromEntries(Object.entries(await inventory(root)).filter(([path]) => Object.hasOwn(retainedFiles, path)));
          ownedFixture(backend, root);
          const options = { glob: { cwd: root } };
          const result = sync ? module.rimraf.rimrafSync(patterns, options) : await module.rimraf.rimraf(patterns, options);
          assert.equal(result, true);
          assert.deepEqual(await inventory(root), expected);
        });
      });
    }

    await t.test(`${module.name}: a pre-aborted request rejects without deleting any file`, async () => {
      await withFixture(backend, async root => {
        await populate(root, { ...selectedFiles, ...retainedFiles });
        const before = await inventory(root), controller = new AbortController();
        const reason = new Error('Deliberate compatibility-test cancellation');
        controller.abort(reason);
        ownedFixture(backend, root);
        await assert.rejects(async () => module.glob.glob(patterns, { cwd: root, signal: controller.signal }), error => error === reason);
        await assert.rejects(async () => module.rimraf.rimraf(patterns, { glob: { cwd: root }, signal: controller.signal }), error => error === reason);
        await assert.rejects(async () => module.rimraf.rimraf(fixturePath(root, 'nested dir'), { signal: controller.signal }), error => error === reason);
        assert.deepEqual(await inventory(root), before);
      });
    });

    await t.test(`${module.name}: cancellation after matching rejects and preserves vetoed files`, async () => {
      await withFixture(backend, async root => {
        await populate(root, { ...selectedFiles, ...retainedFiles });
        const before = await inventory(root), controller = new AbortController();
        const reason = new Error('Cancel after the matched delete begins');
        let filtered = 0;
        ownedFixture(backend, root);
        await assert.rejects(async () => module.rimraf.rimraf(patterns, {
          glob: { cwd: root }, signal: controller.signal,
          filter: path => {
            assert.ok(beneath(root, resolve(path)));
            filtered++; controller.abort(reason); return false;
          },
        }), error => error === reason);
        assert.ok(filtered > 0, 'Cancellation must occur after glob discovery, not before dispatch');
        assert.deepEqual(await inventory(root), before);
      });
    });
  }
}
