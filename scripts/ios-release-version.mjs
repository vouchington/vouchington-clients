#!/usr/bin/env node
const [tag, runNumber, runAttempt] = process.argv.slice(2)

const match = /^ios-v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.exec(tag ?? '')
if (!match) {
  console.error('Expected release tag ios-vMAJOR.MINOR.PATCH')
  process.exit(1)
}
if (!/^[1-9]\d{0,3}$/.test(runNumber ?? '') || !/^[1-9]\d{0,1}$/.test(runAttempt ?? '')) {
  console.error('GitHub run number must be 1–9999 and attempt must be 1–99')
  process.exit(1)
}

console.log(`version=${match.slice(1).join('.')}`)
console.log(`build=${runNumber}.${runAttempt}.0`)
