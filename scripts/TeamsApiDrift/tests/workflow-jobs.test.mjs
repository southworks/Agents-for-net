import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync, rmSync, existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const require = createRequire(import.meta.url);
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const workflows = ['pr', 'scheduled'].map(kind => ({ kind, text: readFileSync(new URL(`../../../.github/workflows/teams-api-drift-${kind}.yml`, import.meta.url), 'utf8') }));
function job(text, name) {
  const section = text.split(`  ${name}:\n`)[1];
  assert.ok(section, `Missing ${name} job`);
  return section.split(/\n  [a-z_]+:\n/)[0];
}
function step(text, name) {
  const section = text.split(`      - name: ${name}\n`)[1];
  assert.ok(section, `Missing ${name} step`);
  return section.split(/\n      - name:/)[0];
}
function script(text, name) {
  return step(text, name).split('          script: |\n')[1].split('\n').map(line => line.replace(/^ {12}/, '')).join('\n');
}
function jobs(same = 'false', result = {}) {
  return Object.fromEntries(['versions', 'deterministic', 'assessment'].map(name => [name, {
    result: result[name] ?? 'success', outputs: name === 'versions' ? { same, from: '2.1.0', to: '2.1.2' } : { evidence_ready: 'true' },
  }]));
}
function shouldRun(text, name, needs) {
  const section = job(text, name);
  const expression = section.match(/^    if: (.+)$/m)[1];
  // These jobs use explicit comparisons of string outputs and status functions.
  // Apply GitHub's implicit success guard when no status function is present.
  const dependencies = section.match(/^    needs: (.+)$/m)[1].replace(/[\[\] ]/g, '').split(',');
  if (!expression.includes('always()') && dependencies.some(id => needs[id].result !== 'success')) return false;
  return new Function('needs', 'always', `return (${expression});`)(needs, () => true);
}
async function execute(text, name, root, needs, steps = {}) {
  const outputs = {};
  const summaries = [];
  const errors = [];
  const core = {
    setOutput: (name, value) => { outputs[name] = value; }, setFailed: error => errors.push(error),
    summary: { addRaw(value) { summaries.push(value); return this; }, async write() {} },
  };
  await new AsyncFunction('require', 'process', 'context', 'core', script(text, name))(
    require, { env: { ARTIFACTS: root, JOB_RESULTS: JSON.stringify(needs), DOWNLOAD_RESULTS: JSON.stringify(steps), STEP_RESULTS: JSON.stringify(steps) } },
    { repo: { owner: 'southworks', repo: 'Agents-for-net' }, runId: 123 }, core);
  return { outputs, summaries, errors };
}
async function fixture(run) {
  const root = mkdtempSync(join(tmpdir(), 'workflow-jobs-test-'));
  try { await run(root); } finally { rmSync(root, { recursive: true, force: true }); }
}

for (const { kind, text } of workflows) {
  test(`${kind}: matching versions skip all later jobs`, () => {
    const needs = jobs('true');
    needs.deterministic.result = needs.assessment.result = 'skipped';
    for (const name of ['deterministic', 'assessment', 'publish']) assert.equal(shouldRun(text, name, needs), false, name);
  });
  test(`${kind}: changed versions run all jobs`, () => {
    for (const name of ['deterministic', 'assessment', 'publish']) assert.equal(shouldRun(text, name, jobs()), true, name);
  });
  test(`${kind}: version resolution failure still reaches artifact publication`, () => {
    const needs = jobs('', { versions: 'failure', deterministic: 'skipped', assessment: 'skipped' });
    assert.equal(shouldRun(text, 'deterministic', needs), false);
    assert.equal(shouldRun(text, 'assessment', needs), false);
    assert.equal(shouldRun(text, 'publish', needs), true);
    assert.match(step(text, 'Publish complete comparison and review artifacts'), /if: always\(\)/);
  });
  test(`${kind}: assessment can explain failed checks only with transferable evidence`, () => {
    const needs = jobs('false', { deterministic: 'failure' });
    assert.equal(shouldRun(text, 'assessment', needs), true);
    needs.deterministic.outputs.evidence_ready = 'false';
    assert.equal(shouldRun(text, 'assessment', needs), false);
    needs.assessment.result = 'skipped';
    assert.equal(shouldRun(text, 'publish', needs), true);
  });
  test(`${kind}: final publication runs after assessment failure`, () => {
    assert.equal(shouldRun(text, 'publish', jobs('false', { assessment: 'failure' })), true);
  });
  test(`${kind}: missing evidence produces a useful fallback report and pipeline artifact`, async () => fixture(async root => {
    const result = await execute(text, 'Prepare final report and pipeline diagnostics', root, jobs('', { versions: 'failure', deterministic: 'skipped', assessment: 'skipped' }));
    assert.ok(existsSync(join(root, 'pipeline-status.json')));
    assert.match(readFileSync(join(root, 'impact-report.md'), 'utf8'), /versions: failure/);
    assert.match(result.summaries[0], /did not produce a validated implementation report/);
    assert.equal(result.outputs.publish_issue, 'true');
  }));
  test(`${kind}: existing report and failed check outcomes survive publication`, async () => fixture(async root => {
    writeFileSync(join(root, 'impact-report.md'), '# Review\nNeeds adaptation.\n');
    writeFileSync(join(root, 'test-summary.json'), JSON.stringify({ checks: { build: 'failure' } }));
    const result = await execute(text, 'Prepare final report and pipeline diagnostics', root, jobs('false', { deterministic: 'failure', assessment: 'failure' }));
    assert.match(result.summaries[0], /Needs adaptation/);
    assert.match(result.summaries[0], /build: failure/);
  }));
  test(`${kind}: malformed upstream artifacts do not prevent diagnostic publication`, async () => fixture(async root => {
    writeFileSync(join(root, 'versions.json'), '{broken');
    const result = await execute(text, 'Prepare final report and pipeline diagnostics', root, jobs('false', { deterministic: 'failure' }));
    assert.match(result.summaries[0], /Invalid JSON artifact: versions.json/);
    assert.ok(existsSync(join(root, 'pipeline-status.json')));
  }));
  test(`${kind}: no API changes suppress a scheduled issue only when jobs succeeded`, async () => fixture(async root => {
    writeFileSync(join(root, 'raw-api-diff.json'), JSON.stringify({ changes: [] }));
    const success = await execute(text, 'Prepare final report and pipeline diagnostics', root, jobs());
    assert.equal(success.outputs.publish_issue, 'false');
    const failed = await execute(text, 'Prepare final report and pipeline diagnostics', root, jobs('false', { deterministic: 'failure' }));
    assert.equal(failed.outputs.publish_issue, 'true');
  }));
  test(`${kind}: safe review cannot clear failed jobs or failed evidence downloads`, async () => fixture(async root => {
    writeFileSync(join(root, 'impact-review-validation.json'), JSON.stringify({ valid: true, canProceed: true }));
    const failedJobs = await execute(text, 'Enforce final workflow result', root, jobs('false', { deterministic: 'failure' }));
    assert.match(failedJobs.errors[0], /deterministic/);
    const failedDownload = await execute(text, 'Enforce final workflow result', root, jobs(), { download_assessment: { outcome: 'failure' } });
    assert.match(failedDownload.errors[0], /download_assessment/);
    const failedComment = await execute(text, 'Enforce final workflow result', root, jobs(), { publish_comment: { outcome: 'failure' } });
    assert.match(failedComment.errors[0], /publish_comment/);
    const success = await execute(text, 'Enforce final workflow result', root, jobs());
    assert.deepEqual(success.errors, []);
  }));
  test(`${kind}: incomplete and non-safe reviews remain blocking at the final gate`, async () => fixture(async root => {
    assert.match((await execute(text, 'Enforce final workflow result', root, jobs())).errors[0], /missing-or-invalid-review/);
    writeFileSync(join(root, 'impact-review-validation.json'), JSON.stringify({ valid: true, canProceed: false }));
    assert.match((await execute(text, 'Enforce final workflow result', root, jobs())).errors[0], /impact-policy/);
    writeFileSync(join(root, 'impact-review-validation.json'), JSON.stringify({ valid: 'true', canProceed: 'false' }));
    assert.match((await execute(text, 'Enforce final workflow result', root, jobs())).errors[0], /impact-policy/);
  }));
}
