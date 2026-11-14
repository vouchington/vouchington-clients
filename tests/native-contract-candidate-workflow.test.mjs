import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { it } from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

it('routes pull-request artifact identity through the tested candidate validator', async () => {
  const [workflow, validator] = await Promise.all([
    readFile(new URL('../.github/workflows/native-contract-tests.yml', import.meta.url), 'utf8'),
    readFile(new URL('../scripts/validate-native-contract-candidate.mjs', import.meta.url), 'utf8'),
  ])
  const verify = jobBlock(workflow, 'verify')

  assert.match(
    verify,
    /import \{ validatePullRequestCandidate \} from '\.\/scripts\/validate-native-contract-candidate\.mjs'/u,
  )
  assert.match(verify, /validatePullRequestCandidate\(\{/u)
  assert.match(verify, /PR_NUMBER: \$\{\{ github\.event\.pull_request\.number \}\}/u)
  assert.match(verify, /PR_BASE_SHA: \$\{\{ github\.event\.pull_request\.base\.sha \}\}/u)
  assert.match(verify, /PR_HEAD_SHA: \$\{\{ github\.event\.pull_request\.head\.sha \}\}/u)
  assert.match(verify, /pulls\/\$\{number\}/u)
  assert.match(verify, /git\/ref\/pull\/\$\{number\}\/merge/u)
  assert.match(verify, /base_sha=\$\{identity\.baseSha\}/u)
  assert.match(verify, /revision_sha=\$\{identity\.revisionSha\}/u)
  assert.match(validator, /currentMergeRevision === candidate\.revision/u)
  const orderedSteps = [
    'Download exact producer artifact',
    'Verify artifact bytes and producer identity',
    'Validate candidate event identity',
  ].map(name => verify.indexOf(`name: ${name}`))
  assert.ok(orderedSteps.every(position => position !== -1))
  assert.ok(orderedSteps[0] < orderedSteps[1] && orderedSteps[1] < orderedSteps[2])
  assert.match(workflow, /run-id: \$\{\{ github\.run_id \}\}/u)
  assert.match(workflow, /--expected-producer-run-id "\$PRODUCER_RUN_ID"/u)
  assert.match(workflow, /--expected-producer-run-attempt "\$PRODUCER_RUN_ATTEMPT"/u)
})
