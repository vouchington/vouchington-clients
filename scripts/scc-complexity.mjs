#!/usr/bin/env node
import { checkSccComplexity } from 'vouchington-tooling/scc-complexity'
import { buildSharedContext } from 'vouchington-tooling/shared-context'

const context = await buildSharedContext(process.cwd())
const report = await checkSccComplexity(context, {
  excludeDir: '.git,fixtures,__tests__,test-helpers,.github,dev',
  tmpdirPrefix: 'vouchington-clients-scc-complexity-',
})

for (const error of report.errors) console.error(error)
if (report.errors.length > 0) process.exitCode = 1
