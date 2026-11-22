#!/usr/bin/env node
// Shadow-mode test planning for pull requests. Advisory only: it never fails, never selects which
// tests run, and always exits 0. Every run is a full suite; this only reports what the plan would
// have skipped. See docs/development/native-ci-test-placement.md.
import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { parseArgs } from 'node:util'

import { audit, classFiles, nmRun, runsUnderPlan } from './test-plan-shadow-audit.mjs'
import { readResults } from './test-plan-shadow-results.mjs'

const { values: opt } = parseArgs({
  options: {
    framework: { type: 'string' },
    label: { type: 'string' },
    scope: { type: 'string' },
    base: { type: 'string' },
    head: { type: 'string' },
    'results-dir': { type: 'string', default: '' },
    'out-dir': { type: 'string' },
  },
})

function nm(args) {
  const r = nmRun(args)
  if (r.status !== 0)
    throw new Error(
      `no-mistakes tests ${args[0]} failed: ${(r.stderr || r.error || '').toString().slice(-400)}`,
    )
  return JSON.parse(r.stdout)
}
function analyze() {
  const plan = nm([
    'plan',
    opt.framework,
    '--json',
    '--base',
    opt.base,
    '--head',
    opt.head,
    '--include-glob',
    `${opt.scope}/**`,
  ])
  const planned = new Set(plan.selected_tests.map(t => t.test_file))
  const out = {
    label: opt.label,
    framework: opt.framework,
    scope: opt.scope,
    base: opt.base,
    head: opt.head,
    changedFiles: plan.changed_files.length,
    plannedTestFiles: planned.size,
    fallback: plan.fallback_triggered,
    fallbackReason: plan.fallback_reason ?? null,
    warnings: plan.warnings,
  }
  const dir = opt['results-dir']
  if (!dir || !existsSync(dir)) return { ...out, results: 'none collected' }
  const files = classFiles(opt.framework, opt.scope)
  const observed = new Set()
  const failedMissed = []
  let unmapped = 0,
    failedClasses = 0
  for (const [name, { failed }] of readResults(dir)) {
    const owners = files.get(name)
    if (!owners) {
      unmapped += 1
      continue
    }
    observed.add(owners[0])
    if (failed) failedClasses += 1
    if (failed && !owners.some(f => runsUnderPlan(plan, f, name))) failedMissed.push(name)
  }
  // File-selection statistic only: the planned command may still run unselected files.
  const skipped = [...observed].filter(f => !planned.has(f)).length
  return {
    ...out,
    observedTestFiles: observed.size,
    unselectedTestFiles: skipped,
    unmappedClasses: unmapped,
    failedClasses,
    failedMissedByPlan: failedMissed,
    audit: audit(opt, plan, [...observed]),
  }
}

function summarize(r) {
  const rows = Object.entries(r).filter(([, v]) => typeof v !== 'object' || v === null)
  const lines = [
    `### Shadow test plan: ${opt.label}`,
    '',
    'Advisory only; the full suite still ran.',
    '',
    '| Metric | Value |',
    '| --- | --- |',
    ...rows.map(([k, v]) => `| ${k} | ${v} |`),
  ]
  if (r.failedMissedByPlan)
    lines.push(
      `| failedMissedByPlan | ${r.failedMissedByPlan.length}${r.failedMissedByPlan.length ? ` (${r.failedMissedByPlan.join(', ')})` : ''} |`,
    )
  if (r.audit) lines.push(`| audit | \`${JSON.stringify(r.audit)}\` |`)
  return `${lines.join('\n')}\n`
}

mkdirSync(opt['out-dir'], { recursive: true })
let result
try {
  result = analyze()
} catch (error) {
  result = { label: opt.label, error: String(error.message ?? error).slice(0, 500) }
}
writeFileSync(
  join(opt['out-dir'], `test-plan-shadow-${opt.label}.json`),
  `${JSON.stringify(result, null, 2)}\n`,
)
const summary = summarize(result)
console.log(summary)
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary)
process.exit(0)
