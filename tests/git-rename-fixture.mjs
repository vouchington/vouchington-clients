import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

export function renamedFilePaths(source, destination) {
  const repository = mkdtempSync(join(tmpdir(), 'changed-file-rename-'))
  const git = (...args) => execFileSync('git', args, { cwd: repository, encoding: 'utf8' })
  try {
    git('init', '-q')
    git('config', 'user.email', 'test@example.invalid')
    git('config', 'user.name', 'Routing test')
    mkdirSync(dirname(join(repository, source)), { recursive: true })
    writeFileSync(join(repository, source), 'before')
    git('add', '.')
    git('commit', '-qm', 'baseline')
    git('mv', source, destination)
    git('commit', '-qm', 'move source out')
    const [base, revision] = git('rev-list', '--max-count=2', 'HEAD').trim().split('\n').reverse()
    return git('diff', '--no-renames', '--name-only', '-z', base, revision)
      .split('\0')
      .filter(Boolean)
  } finally {
    rmSync(repository, { recursive: true, force: true })
  }
}
