import { readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// Copilot CLI 1.0.101 emits completed assistant.message snapshots and a final
// result record. Never concatenate deltas, tool results, or draft messages.
export function extractFinalResponse(text) {
  const events = text.replace(/^\uFEFF/, '').split(/\r?\n/).filter(line => line.trim()).map(line => JSON.parse(line));
  if (events.some(event => !event || typeof event !== 'object' || Array.isArray(event)))
    throw new Error('Copilot output must contain JSON event objects.');
  const results = events.filter(event => event.type === 'result');
  if (results.length !== 1 || events.at(-1) !== results[0] || results[0].exitCode !== 0)
    throw new Error('Copilot output requires exactly one successful terminal result.');
  let finalMessage;
  for (const event of events.slice(0, -1)) {
    if (event.parentToolCallId || event.data?.parentToolCallId) continue;
    if (event.type === 'session.error') throw new Error('Copilot session reported an error.');
    if (event.type === 'tool.execution_start') finalMessage = undefined;
    if (event.type !== 'assistant.message') continue;
    const data = event.data;
    finalMessage = data && typeof data.content === 'string' && data.content.trim() &&
      !data.toolRequests?.length && (!data.phase || data.phase === 'final_answer') ? data : undefined;
  }
  if (!finalMessage) throw new Error('Copilot output has no completed final assistant response.');
  return { content: finalMessage.content, observedModel: finalMessage.model ?? null, sessionId: results[0].sessionId ?? null };
}

export function writeMetadata(path, model, version, summaryPath) {
  if (!model || model === 'auto') throw new Error('An explicit COPILOT_MODEL is required.');
  const metadata = { schemaVersion: 1, requestedModel: model, cliVersion: version.split(/\r?\n/)[0].trim() };
  if (!metadata.cliVersion) throw new Error('Copilot CLI version is missing.');
  writeFileSync(path, JSON.stringify(metadata, null, 2) + '\n');
  console.log(`Summary: Review model (requested): ${model}; CLI: ${metadata.cliVersion}`);
  if (summaryPath) appendFileSync(summaryPath, `\n### Implementation review agent\n\n- Requested model: \`${model}\`\n- CLI: \`${metadata.cliVersion}\`\n`);
  return metadata;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const [mode, input, output, extra] = process.argv.slice(2);
    if (mode === 'metadata') {
      writeMetadata(input, output, extra, process.env.GITHUB_STEP_SUMMARY);
    } else if (mode === 'extract') {
      const response = extractFinalResponse(readFileSync(input, 'utf8'));
      writeFileSync(output, response.content);
      writeFileSync(`${output}.metadata.json`, JSON.stringify({ observedModel: response.observedModel, sessionId: response.sessionId }, null, 2) + '\n');
      console.log(`Summary: Captured final assistant response; observed model: ${response.observedModel ?? 'not reported by CLI'}.`);
      if (process.env.GITHUB_STEP_SUMMARY && response.observedModel)
        appendFileSync(process.env.GITHUB_STEP_SUMMARY, `\n- Model reported in final response event: \`${response.observedModel}\`\n`);
    } else throw new Error('Expected metadata or extract command.');
  } catch (error) {
    console.error(`Review agent output: ${error.message}`);
    process.exitCode = 1;
  }
}
