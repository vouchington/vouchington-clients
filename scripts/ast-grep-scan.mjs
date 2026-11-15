#!/usr/bin/env node
import { scanWithoutExamples } from './ast-grep-without-examples.mjs'

const result = await scanWithoutExamples('ast-grep', process.argv.slice(2))
process.stdout.write(result.stdout ?? '')
process.stderr.write(result.stderr ?? '')
process.exitCode = result.status ?? 1
