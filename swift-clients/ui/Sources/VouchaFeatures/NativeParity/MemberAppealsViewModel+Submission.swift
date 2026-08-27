import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension MemberAppealsViewModel {
    func submitAppeal() async {
        guard !isSubmitting, let target = activeTarget, let client, let currentUserId else { return }
        let draft = draftStore.draft(for: target, currentUserId: currentUserId)
        guard let reason = draft.reason else {
            submissionState = .failed(.message(.nativeSwiftModerationAppealsMemberReasonRequired))
            return
        }
        let details = draft.details.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !details.isEmpty else {
            submissionState = .failed(.message(.nativeSwiftModerationAppealsMemberDetailsRequired))
            return
        }
        let request = ModerationAppealSubmissionRequest(
            targetType: target.targetType,
            targetId: target.targetId,
            reason: reason,
            details: details,
            postRemovalKind: target.postRemovalKind,
            turnstileToken: turnstileToken
        )
        guard details.count <= 3_800,
              request.encodedAppealReason.utf16.count
              <= ModerationAppealSubmissionRequest.maximumAppealReasonUtf16Count
        else {
            submissionState = .failed(.message(.nativeSwiftModerationAppealsMemberDetailsTooLong))
            return
        }

        submissionRevision += 1
        let revision = submissionRevision
        activeSubmissionRevision = revision
        submissionState = .submitting
        defer {
            if activeSubmissionRevision == revision {
                activeSubmissionRevision = nil
            }
        }
        do {
            let response: ModerationAppealSubmissionResponse = try await client.send(.submitAppeal(
                request
            ))
            guard submissionRevision == revision, activeTarget == target else { return }
            replaceTrackingAppeal(response.appeal)
            draftStore.clear(target: target, currentUserId: currentUserId)
            turnstileToken = nil
            submissionState = .succeeded(isDuplicate: response.isDuplicate)
        } catch {
            guard submissionRevision == revision, activeTarget == target else { return }
            turnstileToken = nil
            submissionState = isCancellation(error) ? .idle : .failed(message(for: error))
        }
    }

    func message(for error: Error) -> UiVerbatimText {
        if let error = error as? VouchaError {
            return error.errorDescription.map(UiVerbatimText.verbatim)
                ?? .message(.nativeSwiftModerationAppealsMemberGenericError)
        }
        return .verbatim(error.localizedDescription)
    }

    private func isCancellation(_ error: Error) -> Bool {
        if error is CancellationError {
            return true
        }
        guard let error = error as? VouchaError else { return false }
        switch error {
        case .cancelled:
            return true
        case let .network(urlError):
            return urlError.code == .cancelled
        default:
            return false
        }
    }

    private func replaceTrackingAppeal(_ appeal: ModerationAppeal) {
        pendingAppealPagination.replaceItems(
            [appeal] + pendingAppealPagination.items.filter { $0.id != appeal.id }
        )
    }
}
