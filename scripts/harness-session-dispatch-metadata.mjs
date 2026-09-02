import { HarnessDispatchError } from 'auto-harness-client/actions'

const MAX_FIELD_CHARACTERS = 512
const MAX_RELATED_CANDIDATES_BYTES = 512

/**
 * Builds the HARNESS_METADATA payload for a dispatch step, so it is linted and
 * unit-testable instead of only exercised as inline YAML.
 */
export function buildHarnessDispatchMetadata(environment) {
  const fields = Object.fromEntries(
    Object.entries({
      agentRef: environment.AGENT_REF,
      expectedHeadSha: environment.EXPECTED_HEAD_SHA,
      completionMode: environment.COMPLETION_MODE,
      issueNumber: environment.ISSUE_NUMBER,
      triggerCommentId: environment.TRIGGER_COMMENT_ID,
      publishContract: environment.PUBLISH_CONTRACT,
      publishBaseRef: environment.PUBLISH_BASE_REF,
      publishTargetPrNumber: environment.PUBLISH_TARGET_PR_NUMBER,
      publishTitlePrefix: environment.PUBLISH_TITLE_PREFIX,
      publishTitleSuffix: environment.PUBLISH_TITLE_SUFFIX,
      publishDuplicateKey: environment.PUBLISH_DUPLICATE_KEY,
      existingIssueMaintenance: environment.EXISTING_ISSUE_MAINTENANCE,
      allowDuplicateIssueCompletion: environment.ALLOW_DUPLICATE_ISSUE_COMPLETION,
      prLabel: environment.PR_LABEL,
      prShepherdVersion: environment.PR_SHEPHERD_VERSION,
      checkpointIssueNumber: environment.CHECKPOINT_ISSUE_NUMBER,
      slackSource: environment.SLACK_SOURCE,
      sourceRunId: environment.SOURCE_RUN_ID,
      sourceRunAttempt: environment.SOURCE_RUN_ATTEMPT,
      sourceRunConclusion: environment.SOURCE_RUN_CONCLUSION,
    }).filter(entry => entry[1] !== undefined && entry[1] !== ''),
  )

  for (const [key, value] of Object.entries(fields)) {
    if (value.length > MAX_FIELD_CHARACTERS) {
      throw new HarnessDispatchError('INVALID_METADATA', `${key} exceeds 512 characters`)
    }
  }

  const related = JSON.parse(environment.PUBLISH_RELATED_CANDIDATES ?? '[]')
  if (!Array.isArray(related)) {
    throw new HarnessDispatchError(
      'INVALID_METADATA',
      'publish-related-candidates must be a JSON array',
    )
  }
  if (related.length > 0) {
    const relatedSerialized = JSON.stringify(related)
    const relatedBytes = Buffer.byteLength(relatedSerialized, 'utf8')
    if (relatedBytes <= MAX_RELATED_CANDIDATES_BYTES) {
      fields.publishRelatedCandidates = relatedSerialized
    } else {
      process.stderr.write(
        `publish-related-candidates dropped: ${relatedBytes} bytes exceeds the ${MAX_RELATED_CANDIDATES_BYTES}-byte non-gating limit\n`,
      )
    }
  }

  return fields
}

if (import.meta.main) {
  process.stdout.write(JSON.stringify(buildHarnessDispatchMetadata(process.env)))
}
