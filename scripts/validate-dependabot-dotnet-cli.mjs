import { readFile } from 'node:fs/promises'
export async function runCli(args, api) {
  const [mode, ...modeArgs] = args
  switch (mode) {
    case 'pull-request': {
      const [pullRequestPath, rawDiffPath, defaultBranch, repository, headRef, baseSha, headSha] =
        modeArgs
      if (modeArgs.length !== 7)
        throw new Error(
          'Usage: validate-dependabot-dotnet-repair.mjs pull-request <live-pr.json> <raw-diff> <default-branch> <repository> <head-ref> <base-sha> <head-sha>',
        )
      const [pullRequest, rawDiff] = await Promise.all([
        readFile(pullRequestPath, 'utf8'),
        readFile(rawDiffPath),
      ])
      api.validateDotnetRepairPullRequest(
        JSON.parse(pullRequest),
        rawDiff,
        defaultBranch,
        repository,
        headRef,
        baseSha,
        headSha,
      )
      return
    }
    case 'provenance': {
      const [provenancePath, repository, pullRequest, workflowRun, baseSha, headSha] = modeArgs
      if (modeArgs.length !== 6)
        throw new Error(
          'Usage: validate-dependabot-dotnet-repair.mjs provenance <provenance.json> <repository> <pr> <run> <base-sha> <head-sha>',
        )
      api.validateDotnetRepairProvenance(
        JSON.parse(await readFile(provenancePath, 'utf8')),
        repository,
        pullRequest,
        workflowRun,
        baseSha,
        headSha,
      )
      return
    }
    case 'published-paths':
      if (modeArgs.length !== 1)
        throw new Error(
          'Usage: validate-dependabot-dotnet-repair.mjs published-paths <NUL-delimited-path-file>',
        )
      api.validateDotnetRepairPublishedPaths(await readFile(modeArgs[0]))
      return
    default:
      throw new Error(
        'Usage: validate-dependabot-dotnet-repair.mjs <pull-request|provenance|published-paths> ...',
      )
  }
}
