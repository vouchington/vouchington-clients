function requireMatch(condition, message) {
  if (!condition) throw new Error(message)
}

export function validatePullRequestCandidate({
  candidate,
  event,
  readPullRequest,
  readCurrentMergeRevision,
}) {
  requireMatch(candidate.event === 'pull_request', 'artifact is not for a pull request')
  requireMatch(candidate.number === Number(event.number), 'pull request number changed')
  requireMatch(candidate.base === event.baseSha, 'event base SHA changed')
  requireMatch(candidate.head === event.headSha, 'event head SHA changed')

  const pullRequest = readPullRequest(candidate.number)
  requireMatch(pullRequest.state === 'open', `pull request is ${pullRequest.state}`)
  requireMatch(pullRequest.base.sha === candidate.base, 'pull request base SHA changed')
  requireMatch(pullRequest.head.sha === candidate.head, 'pull request head SHA changed')

  const currentMergeRevision = readCurrentMergeRevision(candidate.number)
  requireMatch(currentMergeRevision === candidate.revision, 'pull request merge SHA changed')

  return { baseSha: candidate.base, revisionSha: candidate.revision }
}
