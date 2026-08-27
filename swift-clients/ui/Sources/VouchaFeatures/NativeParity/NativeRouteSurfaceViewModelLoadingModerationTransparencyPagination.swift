import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

private struct TransparencyContinuationRequest {
    let loadRevision: Int
    let pageRequestRevision: Int
    let continuationToken: Int
    let range: ModerationTransparencyRange
    let after: String
}

extension NativeRouteSurfaceViewModel {
    func loadOlderModerationTransparency() async {
        guard destination == .moderationTransparency,
              moderationTransparencyRange == .all,
              let after = moderationTransparencyNextCursor,
              let client,
              !moderationTransparencyIsLoadingOlder
        else { return }
        let loadRevision = moderationTransparencyLoadRevision
        moderationTransparencyPageRevision += 1
        let pageRequestRevision = moderationTransparencyPageRevision
        moderationTransparencyContinuationToken += 1
        let continuationToken = moderationTransparencyContinuationToken
        let range = moderationTransparencyRange
        let request = TransparencyContinuationRequest(
            loadRevision: loadRevision,
            pageRequestRevision: pageRequestRevision,
            continuationToken: continuationToken,
            range: range,
            after: after
        )
        moderationTransparencyIsLoadingOlder = true
        moderationTransparencyLoadMoreError = nil
        defer {
            if ownsModerationTransparencyRequest(
                loadRevision: loadRevision,
                pageRequestRevision: pageRequestRevision,
                range: range
            ), continuationToken == moderationTransparencyContinuationToken {
                moderationTransparencyIsLoadingOlder = false
            }
        }
        do {
            let response: ModerationTransparency = try await client.send(
                .moderationTransparency(range: ModerationTransparencyRange.all.rawValue, after: after)
            )
            guard ownsModerationTransparencyRequest(
                loadRevision: loadRevision,
                pageRequestRevision: pageRequestRevision,
                range: range
            ), continuationToken == moderationTransparencyContinuationToken,
            moderationTransparencyNextCursor == after
            else { return }
            moderationTransparencyBuckets.append(contentsOf: response.buckets)
            moderationTransparencyNextCursor = response.nextCursor
            rows = moderationTransparencyRows(moderationTransparencyBuckets, range: range)
        } catch {
            handleModerationTransparencyContinuationFailure(
                error,
                request: request
            )
        }
    }

    private func handleModerationTransparencyContinuationFailure(
        _ error: Error,
        request: TransparencyContinuationRequest
    ) {
        guard ownsModerationTransparencyRequest(
            loadRevision: request.loadRevision,
            pageRequestRevision: request.pageRequestRevision,
            range: request.range
        ), request.continuationToken == moderationTransparencyContinuationToken,
        moderationTransparencyNextCursor == request.after
        else { return }
        if let vouchaError = error as? VouchaError {
            switch vouchaError {
            case .forbidden, .notFound:
                moderationTransparencyBuckets = []
                moderationTransparencyNextCursor = nil
                moderationTransparencyLoadMoreError = nil
                rows = moderationTransparencyLockedRows()
                return
            default: break
            }
        }
        moderationTransparencyLoadMoreError = .message(.nativeSwiftRouteSurfaceLoadMoreFailed)
    }

    func ownsModerationTransparencyRequest(
        loadRevision: Int,
        pageRequestRevision: Int,
        range: ModerationTransparencyRange
    ) -> Bool {
        loadRevision == moderationTransparencyLoadRevision
            && pageRequestRevision == moderationTransparencyPageRevision
            && destination == .moderationTransparency
            && moderationTransparencyRange == range
    }
}
