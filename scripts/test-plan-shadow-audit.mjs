import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'

const sha256 = text => createHash('sha256').update(text).digest('hex')
export const run = (cmd, args) =>
  spawnSync(cmd, args, { encoding: 'utf8', maxBuffer: 1 << 28, timeout: 60_000 })

// CI runs the pinned devDependency through pnpm; NO_MISTAKES_BIN lets a local run use another binary.
export function nmRun(args) {
  const tail = [
    'tests',
    ...args,
    '--timeout',
    '25',
    '--lock-timeout',
    process.env.SHADOW_LOCK_TIMEOUT ?? '10',
  ]
  return process.env.NO_MISTAKES_BIN
    ? run(process.env.NO_MISTAKES_BIN, tail)
    : run('pnpm', ['exec', 'no-mistakes', ...tail])
}

/** Map each test class to the files declaring it, so results can be tied back to plan entries. */
export function classFiles(framework, scope) {
  const ext = framework === 'dotnet' ? /\.cs$/u : /\.swift$/u
  const decl = /\b(class|struct|actor|extension)\s+([A-Za-z_]\w*)/gu
  const map = new Map()
  for (const file of run('git', ['ls-files', '--', scope])
    .stdout.split('\n')
    .filter(f => ext.test(f))) {
    for (const [, kind, name] of readFileSync(file, 'utf8').matchAll(decl)) {
      const files = map.get(name) ?? []
      files[kind === 'extension' ? 'push' : 'unshift'](file)
      map.set(name, [...new Set(files)])
    }
  }
  return map
}

export function audit(opt, plan, observed) {
  if (observed.length === 0) return { skipped: 'no mapped test results' }
  const digest = sha256(opt.head)
  const provenance = {
    checkout_revision: opt.head,
    source_digest: digest,
    scope_digest: sha256(`${opt.framework}:${opt.scope}`),
  }
  const dir = join(opt['out-dir'], 'audit-inputs')
  mkdirSync(dir, { recursive: true })
  const planPath = join(dir, `${opt.label}.audit-plan.json`)
  const obsPath = join(dir, `${opt.label}.audit-observations.json`)
  writeFileSync(
    planPath,
    JSON.stringify({
      schema_version: 1,
      provenance,
      plan,
      changed_files: plan.changed_files,
      changed_symbols: [],
    }),
  )
  // Per-test-file execution traces are not collected by the existing suites, so every observation
  // is marked incomplete: audit can report selection overlap but cannot prove a changed-code miss.
  const tests = observed.map(f => ({
    test_file: f,
    executed_files: [f],
    executed_symbols: [],
    trace_complete: false,
  }))
  writeFileSync(
    obsPath,
    JSON.stringify({
      schema_version: 1,
      provenance,
      granularity: 'per-test-file',
      suite: 'full',
      complete: true,
      tests,
    }),
  )
  const r = nmRun(['audit', '--plan', planPath, '--observations', obsPath])
  if (r.status !== 0) return { error: (r.stderr || '').slice(-400) }
  const report = JSON.parse(r.stdout)
  return {
    observedTestFiles: report.observed_test_files,
    selectedTestFiles: report.selected_test_files,
    missedObservedTests: report.missed_observed_tests.length,
    selectedWithIncompleteTraces: report.selected_with_incomplete_traces.length,
    selectedWithoutObservedExecution: report.selected_without_observed_execution.length,
  }
}

/**
 * True when the planned command would execute this test class. Selected files are not enough: a
 * target such as `swift test --filter VouchaCoreTests` or `dotnet test <project>` runs a whole
 * target or project. Match the class against the target's `--filter` regex, or accept any class
 * under a planned project that has no filter.
 */
export function runsUnderPlan(plan, file, className) {
  const target = plan.execution_targets.find(
    t =>
      t.test_files.includes(file) ||
      (t.config && (file.startsWith(`${t.config}/`) || file.startsWith(`${dirname(t.config)}/`))),
  )
  if (!target) return false
  const args = target.runner_args ?? []
  const at = args.indexOf('--filter')
  if (at === -1) return true
  try {
    const module = file.split('/Tests/')[1]?.split('/')[0]
    return new RegExp(args[at + 1], 'u').test(module ? `${module}.${className}` : className)
  } catch {
    return true
  }
}
