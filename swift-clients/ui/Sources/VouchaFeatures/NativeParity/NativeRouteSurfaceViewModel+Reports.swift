import VouchaAPI
import VouchaCore
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func report(
        target: NativeDetailReportTarget,
        reason: String,
        note: String?,
        turnstileToken: String? = nil
    ) async -> Bool {
        guard let client,
              detailReportSubmissionState == .idle,
              target == detailReportTarget else { return false }
        detailReportOperationGeneration += 1
        let operationGeneration = detailReportOperationGeneration
        detailReportSubmissionState = .submitting
        detailReportErrorMessage = nil
        do {
            let _: EmptyResponse = try await client.send(.report(
                entityType: target.entityType,
                entityId: target.entityId,
                reason: reason,
                note: note,
                turnstileToken: turnstileToken
            ))
            if isCurrentReportOperation(operationGeneration, target: target) {
                detailReportSubmissionState = .submitted
                detailReportSuccessPresented = true
                return true
            }
        } catch let error as VouchaError {
            if isCurrentReportOperation(operationGeneration, target: target) {
                detailReportSubmissionState = .idle
                detailReportErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftDetailReportFailed)
            }
        } catch {
            if isCurrentReportOperation(operationGeneration, target: target) {
                detailReportSubmissionState = .idle
                detailReportErrorMessage = .verbatim(error.localizedDescription)
            }
        }
        return false
    }

    private func isCurrentReportOperation(
        _ operationGeneration: Int,
        target: NativeDetailReportTarget
    ) -> Bool {
        detailReportOperationGeneration == operationGeneration && detailReportTarget == target
    }

    func clearPendingReportDraft() {
        detailPendingReportReason = nil
        detailPendingReportNote = ""
        detailPendingReportTurnstile = false
        detailShowingReportTurnstile = false
    }
}
