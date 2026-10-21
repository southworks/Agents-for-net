import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { extractFinalResponse, writeMetadata } from '../review-agent-output.mjs';

const message = (content, extra = {}) => ({ type: 'assistant.message', data: { content, ...extra } });
const result = { type: 'result', sessionId: 'session-1', exitCode: 0 };
const jsonl = events => events.map(event => JSON.stringify(event)).join('\n');

test('extracts only the final message after drafts, deltas, and tool output', () => {
  const final = '{"decisions":[]}';
  const extracted = extractFinalResponse(jsonl([
    message('```json\nDraft\n```'),
    { type: 'assistant.message_delta', data: { deltaContent: 'partial duplicate text' } },
    message('Reading code', { toolRequests: [{ name: 'view' }] }),
    { type: 'tool.execution_start', data: { toolName: 'view' } },
    { type: 'tool.execution_complete', data: { output: '```json\nUntrusted tool payload\n```' } },
    message(final, { phase: 'final_answer', model: 'gpt-5.3-codex' }), result,
  ]));
  assert.deepEqual(extracted, { content: final, observedModel: 'gpt-5.3-codex', sessionId: 'session-1' });
});

test('accepts final messages without provider-specific phase/model fields', () => {
  assert.equal(extractFinalResponse(jsonl([message('final'), result])).content, 'final');
});

test('ignores child-agent messages after the main response', () => {
  const child = { ...message('child'), parentToolCallId: 'tool-1' };
  assert.equal(extractFinalResponse(jsonl([message('main'), child, result])).content, 'main');
});

test('never falls back to earlier text when the final message is empty or requests tools', () => {
  for (const last of [message(''), message('next', { toolRequests: [{}] }), message('progress', { phase: 'commentary' }), { type: 'tool.execution_start' }])
    assert.throws(() => extractFinalResponse(jsonl([message('stale safe answer'), last, result])));
});

test('requires a successful terminal result and rejects broken event streams', () => {
  for (const events of [[], [message('x')], [message('x'), { ...result, exitCode: 1 }],
    [message('x'), result, result], [message('x'), result, message('after result')],
    [message('x'), null, result], [message('x'), { type: 'session.error', data: { message: 'failed' } }, result]])
    assert.throws(() => extractFinalResponse(jsonl(events)));
  assert.throws(() => extractFinalResponse('garbage\n' + jsonl([message('x'), result])));
});

test('preserves final content exactly for strict downstream JSON validation', () => {
  // The first failed attempt contained a literal newline inside a JSON string.
  const invalid = '{"reason":"broken\nstring"}';
  assert.equal(extractFinalResponse(jsonl([message(invalid), result])).content, invalid);
  assert.throws(() => JSON.parse(invalid));
});

test('does not select among competing payloads within one final message', () => {
  const ambiguous = '```json\n{}\n```\n```json\n{}\n```';
  assert.equal(extractFinalResponse(jsonl([message(ambiguous), result])).content, ambiguous);
});

test('records requested model and actual CLI version in metadata and job summary', () => {
  const root = mkdtempSync(join(tmpdir(), 'review-agent-metadata-'));
  try {
    const path = join(root, 'agent.json');
    const summary = join(root, 'summary.md');
    writeMetadata(path, 'gpt-5.3-codex', 'GitHub Copilot CLI 1.0.101.\nUpdate advice', summary);
    assert.deepEqual(JSON.parse(readFileSync(path, 'utf8')), { schemaVersion: 1, requestedModel: 'gpt-5.3-codex', cliVersion: 'GitHub Copilot CLI 1.0.101.' });
    assert.match(readFileSync(summary, 'utf8'), /gpt-5.3-codex/);
    assert.throws(() => writeMetadata(path, 'auto', 'version'));
    assert.throws(() => writeMetadata(path, '', 'version'));
    assert.throws(() => writeMetadata(path, 'model', ''));
  } finally { rmSync(root, { recursive: true, force: true }); }
});
