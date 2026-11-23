import { appendFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { runChangedFileRoute } from './changed-file-route.mjs'

// A change is docs-only when it touches at least one path and every path is Markdown or under docs/.
// No native job reads either (verified by grep over Swift, .NET, scripts and actions), so such a
// change cannot alter a native build, test, or smoke result.
export function isDocsOnly(paths) {
  return paths.length > 0 && paths.every(path => path.endsWith('.md') || path.startsWith('docs/'))
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  const repositoryRoot = resolve(import.meta.dirname, '..')
  try {
    runChangedFileRoute({
      repositoryRoot,
      select: isDocsOnly,
      logLabel: 'Docs-only changed-file route',
      outputKey: 'docs-only',
      mergeGroupFallback: false,
    })
  } catch (error) {
    // Fail safe: an uncomputable diff (shallow fetch, missing base) runs the full suite.
    console.log(
      `Docs-only changed-file route: docs-only=false (diff unavailable: ${error.message})`,
    )
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, 'docs-only=false\n')
  }
}
