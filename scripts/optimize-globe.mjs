import fs from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const root = process.cwd();
const input = path.join(root, 'assets', 'globe-source', 'Flag_map_of_the_world.svg');
const outDir = path.join(root, 'public', 'globe-image-background');

const variants = [
  { width: 1024, webpQuality: 70, avifQuality: 46 },
];

async function ensureInput() {
  try {
    await fs.access(input);
  } catch {
    throw new Error(`Missing input SVG: ${input}`);
  }
}

async function getSize(filePath) {
  const stats = await fs.stat(filePath);
  return (stats.size / 1024 / 1024).toFixed(2);
}

async function buildVariant(variant) {
  const baseName = `Flag_map_of_the_world-${variant.width}`;
  const webpPath = path.join(outDir, `${baseName}.webp`);
  const avifPath = path.join(outDir, `${baseName}.avif`);

  const source = sharp(input, { density: 160 });

  await source
    .clone()
    .resize({ width: variant.width, fit: 'cover' })
    .webp({ quality: variant.webpQuality, effort: 5 })
    .toFile(webpPath);

  await source
    .clone()
    .resize({ width: variant.width, fit: 'cover' })
    .avif({ quality: variant.avifQuality, effort: 6 })
    .toFile(avifPath);

  const webpSize = await getSize(webpPath);
  const avifSize = await getSize(avifPath);
  return { width: variant.width, webpPath, webpSize, avifPath, avifSize };
}

async function main() {
  await ensureInput();

  const sourceSize = await getSize(input);
  console.log(`Source SVG: ${input} (${sourceSize} MB)`);

  const results = [];
  for (const variant of variants) {
    const result = await buildVariant(variant);
    results.push(result);
  }

  console.log('\nGenerated optimized globe assets:');
  for (const r of results) {
    console.log(`- ${path.basename(r.webpPath)} (${r.webpSize} MB)`);
    console.log(`- ${path.basename(r.avifPath)} (${r.avifSize} MB)`);
  }

  console.log('\nUsing 1024 variants only for smallest app footprint.');
}

main().catch((err) => {
  console.error(err.message || err);
  process.exit(1);
});
