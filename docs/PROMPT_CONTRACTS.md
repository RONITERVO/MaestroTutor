# Maestro prompt contracts

Maestro's authored model instructions live in **`shared/prompts/`**. The familiar
`src/core/config/prompts.ts` is the app-facing entry point. Functions imports the
same runtime-independent definitions directly. There are no browser, provider,
environment, database or filesystem dependencies in the catalogue.

| File | Owns |
| --- | --- |
| `tutor.ts` | Main tutor instructions, voice/persona addition, system composer |
| `suggestions.ts` | Reply suggestions, live-transcript exception, JSON response schema |
| `images.ts` | Image generation, retry wording, camera/composition reference, avatar identity overlay, request nonce formatting |
| `art.ts` | Sketchbook reference prose and SVG/HTML/canvas examples |
| `speech.ts` | STT with context, triggered TTS, audio notes, diagnostic Live fallback |
| `music.ts` | One music suffix builder shared by BYOK and managed Functions |
| `translation.ts` | Translation template |
| `context.ts` | History/profile labels, compact-history markers, omissions, re-engagement trigger |

The SDK and feature layers still select history, trim data, attach files and send
requests. They import authored wording from the catalogue instead of creating
another system prompt or appending prose. User messages, model-generated tool
arguments, summaries and profiles are runtime data, not catalogue entries. Test
fixtures, diagnostic scripts and the standalone theme-model brief remain separate.
The recorded `Play` sound remains a media asset; its meaning lives in `speech.ts`.

## Preserve the existing behavior

The baseline is the shipped `ab2923d` implementation. Golden snapshots were
recorded and committed **before** moving prompt definitions (`a115850`). The
centralization then passed those same snapshots without updating them. Exact
wording, whitespace, JSON schema, role order and media placement all matter.
Local `AbortSignal` cancellation metadata is serialized by its public aborted
state; private Node-version-specific symbols are excluded because they are not
sent to the model. No prompt text is normalized or removed from the snapshots.
Do not clean up spelling, change language-tag handling or strengthen instructions
as part of an unrelated refactor.

Existing mode differences are deliberate compatibility constraints for this
release, even where they may deserve a later product change:

| Path | Voice/persona addition | Sketchbook reference |
| --- | --- | --- |
| Browser text chat | Included | Included as first-user-turn reference material |
| Browser Live / silent observer | Omitted | Omitted |
| Headless text chat | Included | Omitted |
| Headless Live / observer | Included | Omitted |

Live suggestions append an exception permitting new artifacts/tools without
fenced blocks. Ordinary suggestions extract them from the tutor's output.
Triggered TTS and audio-note generation use distinct prompts. The low-level
synthetic Live fallback differs from normal STT. Moving definitions does not
authorize merging these modes or turning soft user-turn references into system
policy. Changing one of these differences requires an explicit behavior change
with before/after evidence.

## Fast developer checks

```sh
npm run test:prompts
npm run verify:prompt-ownership
```

The prompt suite is also included in `npm test`, which the required Release gate
runs on pull requests. These checks require no credentials or provider charges.

The tests cover:

- Full central wording and art references.
- Final text and suggestion requests captured at the Gemini SDK boundary,
  including language substitution, roles, history, files, avatar, search and schema.
- Browser Live context and actual headless chat, re-engagement, STT, Live and
  observer requests. Browser text cases exercise the real history serializer and
  provider adapter; they do not simulate every React user interaction.
- Empty and populated suggestion context, multilingual/quoted text, compact
  artifact/tool history and truncation, image history pruning and retries.
- Browser SVG serialization in actual suggestion requests and Live context,
  captured before moving DOM animation repair into its browser adapter. These
  fixtures also preserve raw headless SVG and do not update the earlier baselines.
- Actual audio-note connections, browser triggered-TTS configuration, music
  weighted prompts/configuration, and the text actually drawn on avatar pixels.
- Managed client forwarding, Functions prompt/schema preservation and the
  gateway's provider connection, so transport refactors cannot quietly drop fields.
- Ownership guard examples and a source scan for inline prompt assignments,
  appended prose and known instruction wording outside the catalogue.

The ownership scan is a guardrail, not a semantic proof for arbitrary strings.
New request paths need a representative provider-boundary test. UI rendering,
arbitrary user data and external model-version changes are not fully characterized
by snapshots. Exact input preservation reduces accidental behavior changes; it
does not make stochastic provider responses deterministic.

## When changing code

1. Run the prompt tests before editing. For a new scenario, capture a synthetic,
   non-private fixture on the current implementation first.
2. Make the implementation change and rerun without `--update`. A refactor should
   pass the existing snapshots unchanged. Fix an unintended delta in the code.
3. If product behavior is intentionally changing, review the actual request diff
   (including instruction role, whitespace, history order, schema and media), then
   update only the affected named test with Vitest's `--update -t` options.
4. Explain the intentional before/after behavior and snapshot changes in the PR.
   Never bulk-refresh baselines merely to make a failing build green. Do not put
   real user history, credentials or signed URLs into fixtures.
5. Run the full release checks and provider staging journey before deployment.
   Prompt tests complement those checks; they do not replace live verification.

For a new prompt, add a named constant or pure builder to the appropriate catalogue
file, export it through `index.ts`, and import it at the existing role/transport
boundary. Shared backend wording must have a single owner, not a copied string.
