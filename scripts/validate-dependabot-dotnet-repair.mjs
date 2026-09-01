import { fileURLToPath } from 'node:url'
import { runCli } from './validate-dependabot-dotnet-cli.mjs'
import { validateDotnetRepairPullRequest } from './validate-dependabot-dotnet-repair-pull-request.mjs'
import { validateDotnetRepairRawDiff } from './validate-dependabot-dotnet-repair-raw-diff.mjs'
import {
  validateDotnetRepairProvenance,
  validateDotnetRepairPublishedPaths,
} from './validate-dependabot-dotnet-repair-provenance.mjs'

export { NUGET_LOCK_PATHS } from './validate-dependabot-dotnet-repair-shared.mjs'
export { validateDotnetRepairPullRequest, validateDotnetRepairRawDiff }
export { validateDotnetRepairProvenance, validateDotnetRepairPublishedPaths }

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  runCli(process.argv.slice(2), {
    validateDotnetRepairPullRequest,
    validateDotnetRepairProvenance,
    validateDotnetRepairPublishedPaths,
  }).catch(error => {
    process.stderr.write(`${error.message}\n`)
    process.exitCode = 1
  })
}
