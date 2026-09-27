import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, it } from 'node:test'

const root = resolve(import.meta.dirname, '..')
const tools = [
  'entry_append',
  'entry_get',
  'session_archive',
  'session_create',
  'session_ensure',
  'session_patch',
  'session_search',
  'snapshot_export',
].map(name => `mcp__agent-blackboard__${name}`)

function readJson(path) {
  return JSON.parse(readFileSync(resolve(root, path), 'utf8'))
}

describe('Agent Blackboard host configuration', () => {
  it('keeps Claude MCP registration pinned and environment-only', () => {
    assert.deepEqual(readJson('.mcp.json'), {
      mcpServers: {
        'agent-blackboard': {
          command: 'npx',
          args: ['-y', 'agent-blackboard@0.5.0', 'mcp'],
          env: {
            AGENT_BLACKBOARD_URL: '${AGENT_BLACKBOARD_URL}',
            AGENT_BLACKBOARD_TOKEN: '${AGENT_BLACKBOARD_TOKEN}',
          },
        },
      },
    })
  })

  it('pre-authorizes exactly the current eight MCP tools', () => {
    const settings = readJson('.claude/settings.json')
    for (const key of [
      'sandbox',
      'defaultMode',
      'model',
      'effortLevel',
      'advisorModel',
      'statusLine',
      'permission_mode',
      'permissionMode',
    ])
      assert.equal(settings[key], undefined, `machine setting ${key} must stay out of project config`)
    assert.deepEqual(settings.enabledMcpjsonServers, ['agent-blackboard'])
    assert.deepEqual(settings.permissions?.allow, tools)
    assert.equal(
      settings.permissions.allow.some(tool => tool.includes('*')),
      false,
      'MCP approval must not use a wildcard',
    )
  })

  it('documents the upstream Codex plugin registration', () => {
    const instructions = readFileSync(resolve(root, '.codex/README.md'), 'utf8')
    assert.match(instructions, /codex plugin marketplace add jonathanong\/agent-blackboard/u)
    assert.match(instructions, /codex plugin add agent-blackboard@agent-blackboard/u)
    assert.match(instructions, /agent-blackboard@0\.5\.0/u)
    const config = readFileSync(resolve(root, '.codex/config.toml'), 'utf8')
    assert.match(config, /\[plugins\."agent-blackboard@agent-blackboard"\]\nenabled = true/u)
    const approvals = [...config.matchAll(/\.tools\.([a-z_]+)\]\napproval_mode = "approve"/gu)].map(
      match => match[1],
    )
    assert.equal((config.match(/approval_mode = "approve"/gu) ?? []).length, 8)
    assert.deepEqual(approvals, [
      'entry_append',
      'entry_get',
      'session_archive',
      'session_create',
      'session_ensure',
      'session_patch',
      'session_search',
      'snapshot_export',
    ])
  })

  it('requires fail-closed, explicit session journaling in root instructions', () => {
    const instructions = readFileSync(resolve(root, 'AGENTS.md'), 'utf8')
    assert.match(instructions, /upstream `agent-blackboard` plugin/u)
    assert.match(instructions, /`vouchington-workflow:blackboard`/u)
    assert.match(instructions, /Session ids.*must\s+be\s+explicit/isu)
    assert.match(instructions, /fail closed/iu)
  })

  it("keeps Cursor's native registration and allowlists pinned to eight tools", () => {
    const mcp = readJson('.cursor/mcp.json')
    assert.equal(mcp.mcpServers['agent-blackboard'].command, 'npx')
    assert.deepEqual(mcp.mcpServers['agent-blackboard'].args, [
      '-y',
      'agent-blackboard@0.5.0',
      'mcp',
    ])
    assert.deepEqual(readJson('.cursor/permissions.json').mcpAllowlist, [
      'agent-blackboard:entry_append',
      'agent-blackboard:entry_get',
      'agent-blackboard:session_archive',
      'agent-blackboard:session_create',
      'agent-blackboard:session_ensure',
      'agent-blackboard:session_patch',
      'agent-blackboard:session_search',
      'agent-blackboard:snapshot_export',
    ])
    const cli = readJson('.cursor/cli.json')
    assert.deepEqual(cli.permissions.deny, ['Shell(sudo)'])
    assert.equal(
      readJson('.cursor/permissions.json').autoRun.block_instructions.some(rule => rule.includes('~/')),
      false,
    )
    assert.deepEqual(
      cli.permissions.allow.filter(entry => entry.startsWith('Shell(')),
      ['Shell(no-mistakes)', 'Shell(swift)', 'Shell(xcodebuild)', 'Shell(dotnet)'],
    )
    assert.deepEqual(
      cli.permissions.allow.filter(entry => entry.startsWith('Mcp(')),
      [
        'Mcp(agent-blackboard:entry_append)',
        'Mcp(agent-blackboard:entry_get)',
        'Mcp(agent-blackboard:session_archive)',
        'Mcp(agent-blackboard:session_create)',
        'Mcp(agent-blackboard:session_ensure)',
        'Mcp(agent-blackboard:session_patch)',
        'Mcp(agent-blackboard:session_search)',
        'Mcp(agent-blackboard:snapshot_export)',
      ],
    )
    assert.equal(
      cli.permissions.allow.some(entry => entry.includes('*')),
      false,
    )
    assert.equal(existsSync(resolve(root, '.cursor/hooks.json')), false)
    assert.equal(existsSync(resolve(root, '.cursor/sandbox.json')), false)
  })

  it("keeps Grok's project MCP registration and permissions without machine sandbox settings", () => {
    const config = readFileSync(resolve(root, '.grok/config.toml'), 'utf8')
    assert.doesNotMatch(config, /^\[mcp_servers\./mu)
    assert.doesNotMatch(config, /^\s*(?:sandbox|model|permission_mode|approval_mode|startup_timeout)\s*=/mu)
    assert.equal((config.match(/MCPTool\(agent-blackboard__/gu) ?? []).length, 8)
    assert.match(config, /MCPTool\(agent-blackboard__snapshot_export\)/u)
    assert.doesNotMatch(config, /^\[(?:sandbox|model|ui)\]/mu)
    assert.equal(existsSync(resolve(root, '.grok/sandbox.toml')), false)
    const instructions = readFileSync(resolve(root, '.grok/README.md'), 'utf8')
    assert.match(instructions, /vouchington-machines\/blob\/main\/docs\/agent-config\.md/u)
    assert.equal(existsSync(resolve(root, '.grok/hooks')), false)
  })

  it('keeps machine sandbox, model, approval-mode, and startup defaults out of project settings', () => {
    const codex = readFileSync(resolve(root, '.codex/config.toml'), 'utf8')
    assert.doesNotMatch(codex, /^\s*(?:sandbox_mode|approval_policy|model|model_reasoning_effort|startup_timeout)\s*=/mu)
    for (const path of ['.claude/README.md', '.codex/README.md', '.cursor/README.md']) {
      const instructions = readFileSync(resolve(root, path), 'utf8')
      assert.match(instructions, /vouchington-machines\/blob\/main\/docs\/agent-config\.md/u)
    }
  })

  it('documents focused plugin provisioning and reads AGENTS.md without a CLAUDE fallback', () => {
    const claude = readFileSync(resolve(root, '.claude/README.md'), 'utf8')
    assert.match(claude, /vouchington-workflow@vouchington/u)
    assert.match(claude, /vouchington-testing@vouchington/u)
    assert.match(claude, /pr-shepherd@jonathanong/u)
    assert.match(claude, /canonical plugin is unavailable, stop/iu)
    const codex = readFileSync(resolve(root, '.codex/README.md'), 'utf8')
    assert.match(codex, /vouchington-testing@vouchington/u)
    const codexConfig = readFileSync(resolve(root, '.codex/config.toml'), 'utf8')
    assert.equal(codexConfig.includes('project_doc_fallback_filenames'), false)
    assert.match(codexConfig, /Do not add a CLAUDE\.md fallback/u)
    const settings = readJson('.claude/settings.json')
    assert.equal(settings.enabledPlugins['vouchington-workflow@vouchington'], true)
    assert.equal(settings.enabledPlugins['vouchington-testing@vouchington'], true)
    assert.equal(settings.enabledPlugins['pr-shepherd@jonathanong'], true)
  })

  it('keeps native test overlays dependent on canonical testing skills', () => {
    for (const [name, canonical, guidance] of [
      [
        'swift-test-authoring',
        'vouchington-testing:swift-test-authoring',
        'swift-clients/AGENTS.md',
      ],
      [
        'dotnet-test-authoring',
        'vouchington-testing:dotnet-test-authoring',
        'dotnet-clients/AGENTS.md',
      ],
    ]) {
      const skill = readFileSync(resolve(root, `.agents/skills/${name}/SKILL.md`), 'utf8')
      const escapeRegExp = value => value.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')
      assert.match(skill, new RegExp(escapeRegExp(canonical), 'u'))
      assert.match(skill, new RegExp(escapeRegExp(guidance), 'u'))
      assert.match(skill, /stop and report the missing prerequisite/iu)
    }
  })

  it('keeps client-owned architecture links local and Vouchington-owned links explicit', () => {
    const swift = readFileSync(resolve(root, 'swift-clients/AGENTS.md'), 'utf8')
    const dotnet = readFileSync(resolve(root, 'dotnet-clients/AGENTS.md'), 'utf8')
    for (const instructions of [swift, dotnet]) {
      assert.match(
        instructions,
        /github\.com\/vouchington\/vouchington\/blob\/main\/docs\/overview\/architecture\/pagination\.md/u,
      )
      assert.match(
        instructions,
        /github\.com\/vouchington\/vouchington\/blob\/main\/docs\/overview\/architecture\/native-clients\.md/u,
      )
    }
    assert.match(dotnet, /docs\/overview\/architecture\/dotnet-deep-linking\.md/u)
    assert.match(
      dotnet,
      /github\.com\/vouchington\/vouchington\/blob\/main\/docs\/runbooks\/native-tls-pinning\.md/u,
    )
    assert.equal(
      existsSync(
        resolve(root, 'docs/overview/architecture/reference-native-clients-current-footprint.md'),
      ),
      true,
    )
    assert.equal(
      existsSync(resolve(root, 'docs/overview/architecture/dotnet-deep-linking.md')),
      true,
    )
  })

  it('ignores provider worktree state without ignoring checked-in configuration', () => {
    const gitignore = readFileSync(resolve(root, '.gitignore'), 'utf8')
    for (const path of [
      '.claude/worktrees/',
      '.codex/worktrees/',
      '.cursor/worktrees/',
      '.grok/worktrees/',
      '.worktrees/',
    ])
      assert.match(gitignore, new RegExp(`^${path.replaceAll('.', '\\.')}$`, 'mu'))
  })
})
