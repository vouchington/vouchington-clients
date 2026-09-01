import assert from 'node:assert/strict'
import { access, mkdir } from 'node:fs/promises'
import { spawnSync } from 'node:child_process'
import { mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = fileURLToPath(new URL('..', import.meta.url))
const astGrep = fileURLToPath(new URL('../node_modules/@ast-grep/cli/ast-grep', import.meta.url))
const rulesDirectory = join(repoRoot, 'ast-grep-rules')
const configPath = join(repoRoot, 'sgconfig.yml')

function runAstGrep(...args) {
  const result = runAstGrepResult(args, repoRoot)
  assert.equal(result.error, undefined, result.error?.message)
  assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`)
}

function runAstGrepResult(args, cwd) {
  return spawnSync(astGrep, args, { cwd, encoding: 'utf8' })
}

function parseExamples(rulePath, source) {
  const lines = source.split('\n')
  const examplesIndex = lines.findIndex(line => line === 'examples:')
  assert.notEqual(examplesIndex, -1, `${rulePath}: missing examples`)
  const examples = []
  for (let index = examplesIndex + 1; index < lines.length; index += 1) {
    if (!lines[index]) continue
    const literal = lines[index].match(/^  - code: ("(?:[^"\\]|\\.)*")\s*$/)
    if (!/^  - code: \|\s*$/.test(lines[index]) && !literal) {
      throw new Error(`${rulePath}:${index + 1}: expected an inline code example`)
    }
    let code
    if (literal) {
      code = JSON.parse(literal[1])
      index += 1
    } else {
      const codeLines = []
      for (
        index += 1;
        index < lines.length && !lines[index].startsWith('    isValid: ');
        index += 1
      ) {
        const line = lines[index]
        if (line)
          assert.match(line, /^      /, `${rulePath}:${index + 1}: invalid example indentation`)
        codeLines.push(line.slice(6))
      }
      code = codeLines.join('\n')
    }
    const isValid = lines[index]?.match(/^    isValid: (true|false)\s*$/)
    assert.ok(isValid, `${rulePath}:${index + 1}: missing isValid`)
    const file = lines[index + 1]?.match(/^    file: ((?:"(?:[^"\\]|\\.)*")|'(?:[^'\\]|\\.)*')\s*$/)
    assert.ok(file, `${rulePath}:${index + 2}: missing quoted example file`)
    const fileValue = file[1].startsWith("'")
      ? file[1].slice(1, -1).replace(/\\'/g, "'")
      : JSON.parse(file[1])
    examples.push({ code, isValid: isValid[1] === 'true', file: fileValue })
    index += 1
  }
  assert.ok(examples.length, `${rulePath}: no examples discovered`)
  assert.ok(
    examples.some(example => example.isValid),
    `${rulePath}: missing isValid:true example`,
  )
  assert.ok(
    examples.some(example => !example.isValid),
    `${rulePath}: missing isValid:false example`,
  )
  return examples
}

async function loadExamples() {
  const ruleFiles = (await readdir(rulesDirectory))
    .filter(file => file.endsWith('.yml') || file.endsWith('.yaml'))
    .toSorted()
  assert.ok(ruleFiles.length, `${rulesDirectory}: no rules discovered`)
  const rules = []
  for (const ruleFile of ruleFiles) {
    const rulePath = join(rulesDirectory, ruleFile)
    const examples = parseExamples(rulePath, await readFile(rulePath, 'utf8'))
    rules.push({ examples, rulePath })
  }
  const exampleCount = rules.reduce((total, rule) => total + rule.examples.length, 0)
  assert.ok(exampleCount, `${rulesDirectory}: no examples discovered`)
  return rules
}

async function assertExample(rulePath, example, testDirectory) {
  const examplePath = resolve(testDirectory, example.file)
  assert.equal(
    relative(testDirectory, examplePath).startsWith('..'),
    false,
    `${rulePath}: example escapes test root`,
  )
  await mkdir(dirname(examplePath), { recursive: true })
  await writeFile(examplePath, example.code)
  const result = runAstGrepResult(
    [
      'scan',
      '--rule',
      rulePath,
      '--config',
      configPath,
      '--json=compact',
      '--no-ignore',
      'hidden',
      '--',
      example.file,
    ],
    testDirectory,
  )
  assert.equal(result.error, undefined, result.error?.message)
  assert.ok(result.status === 0 || result.status === 1, `${result.stdout}\n${result.stderr}`)
  const findings = result.stdout.trim() ? JSON.parse(result.stdout) : []
  assert.equal(
    findings.length > 0,
    !example.isValid,
    `${rulePath}: expected ${example.file} to ${example.isValid ? 'pass' : 'produce a finding'}`,
  )
}

describe('native ast-grep guards', () => {
  it("executes every guard's positive and negative examples", async t => {
    await access(astGrep)
    const testDirectory = await mkdtemp(join(tmpdir(), 'voucha-ast-grep-examples-'))
    t.after(() => rm(testDirectory, { recursive: true, force: true }))
    const rules = await loadExamples()
    assert.ok(rules.length, 'no ast-grep rules discovered')
    for (const { examples, rulePath } of rules) {
      for (const example of examples) await assertExample(rulePath, example, testDirectory)
    }
  })

  it('scans the client-owned native sources with error-level enforcement', async () => {
    await access(astGrep)
    runAstGrep(
      'scan',
      '--error',
      '--no-ignore',
      'hidden',
      '--',
      'swift-clients/',
      'dotnet-clients/',
    )
  })
})
