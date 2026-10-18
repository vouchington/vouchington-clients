import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { pairedRepositories, validateDispatch } from '../scripts/paired-contract-attestation.mjs'
import { normalizeMaintainerPermission } from '../scripts/paired-contract-github.mjs'

function dispatch(overrides = {}) {
  const inputs = {
    producerPullRequest: '3292',
    expectedProducerHeadSha: '1'.repeat(40),
    clientsPullRequest: '376',
    expectedClientsHeadSha: '2'.repeat(40),
    nativeTestProfile: 'cookie-snapshot',
    reviewedBothSnapshots: true,
    ...overrides.inputs,
  }
  const { inputs: ignoredInputs, ...fields } = overrides
  void ignoredInputs
  return validateDispatch({
    eventName: 'workflow_dispatch',
    repository: pairedRepositories.clients,
    ref: 'refs/heads/main',
    workflowRef: `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main`,
    runAttempt: 1,
    actor: 'maintainer',
    triggeringActor: 'maintainer',
    clientsPermission: 'maintain',
    inputs,
    ...fields,
  })
}

describe('paired contract operator attestation inputs', () => {
  it('accepts only an explicit, first-run main dispatch by a live maintainer', () => {
    assert.equal(dispatch().actor, 'maintainer')
    assert.equal(dispatch({ clientsPermission: 'admin' }).clientsPermission, 'admin')
    assert.equal(
      dispatch({ inputs: { nativeTestProfile: 'pending-turn-recovery' } }).nativeTestProfile,
      'pending-turn-recovery',
    )
  })

  it('rejects non-main events, reruns, actor changes, and unconfirmed snapshots', () => {
    for (const override of [
      { eventName: 'pull_request' },
      { ref: 'refs/heads/feature' },
      {
        workflowRef: `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/feature`,
      },
      { runAttempt: 2 },
      { triggeringActor: 'different-actor' },
      { clientsPermission: 'write' },
      { inputs: { nativeTestProfile: '' } },
      { inputs: { nativeTestProfile: 'auto-detect' } },
      { inputs: { reviewedBothSnapshots: false } },
    ])
      assert.throws(() => dispatch(override), /Paired contract attestation failed/u)
  })

  it('requires numeric PR identifiers and exact reviewed head SHA assertions', () => {
    for (const [key, value] of [
      ['producerPullRequest', '0'],
      ['producerPullRequest', '01'],
      ['clientsPullRequest', '1;refs/heads/main'],
      ['expectedProducerHeadSha', 'refs/pull/1/merge'],
      ['expectedClientsHeadSha', 'A'.repeat(40)],
      ['expectedClientsHeadSha', 'b'.repeat(39)],
    ]) {
      assert.throws(
        () => dispatch({ inputs: { [key]: value } }),
        /Paired contract attestation failed/u,
      )
    }
  })

  it('accepts documented built-in role names and rejects custom or inconsistent roles', () => {
    assert.equal(
      normalizeMaintainerPermission({
        permission: 'write',
        role_name: 'maintain',
        permissions: { maintain: true, admin: false },
      }),
      'maintain',
    )
    assert.equal(
      normalizeMaintainerPermission({ permission: 'admin', role_name: 'admin' }),
      'admin',
    )
    for (const response of [
      { permission: 'write', role_name: 'custom', permissions: { maintain: true } },
      { permission: 'read', role_name: 'maintain', permissions: { maintain: true } },
      { permission: 'write', role_name: 'maintain', permissions: { maintain: false } },
    ])
      assert.throws(() => normalizeMaintainerPermission(response), /built-in maintain or admin/u)
  })
})
