import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeCopyrightNoticesSurface {
    @ViewBuilder
    func euDecision(_ euCase: CopyrightEuParticipantCase) -> some View {
        euDecisionSummary(euCase)
        complaintSection(euCase)
        otherRedressSection
        settlementsSection
    }

    private func euDecisionSummary(_ euCase: CopyrightEuParticipantCase) -> some View {
        section(.nativeCopyrightNoticesDecision) {
            Text(euDecisionSummaryText(euCase))
            if let decidedAt = euCase.decidedAt {
                Text(localized(.nativeCopyrightNoticesDecidedDate, parameters: [
                    "date": localizedDate(decidedAt, style: .date)
                ]))
            }
        }
    }

    private func euDecisionSummaryText(_ euCase: CopyrightEuParticipantCase) -> String {
        if euCase.reopenedAt != nil {
            return localized(.nativeCopyrightNoticesEuComplaintUpheldReviewing)
        }
        guard let outcome = euCase.outcome else {
            return localized(.nativeCopyrightNoticesEuDecisionReviewing)
        }
        return localized(outcome == "restrict"
            ? .nativeCopyrightNoticesEuMaterialRestricted
            : .nativeCopyrightNoticesEuNoAction)
    }

    private func complaintSection(_ euCase: CopyrightEuParticipantCase) -> some View {
        section(.nativeCopyrightNoticesYourComplaint) {
            complaintStatus(euCase)
            complaintWindow(euCase)
            complaintDecision(euCase)
        }
    }

    @ViewBuilder
    private func complaintStatus(_ euCase: CopyrightEuParticipantCase) -> some View {
        if euCase.complaint.request != nil {
            Text(localized(.nativeCopyrightNoticesComplaintReceived))
        } else if euCase.outcome == nil {
            Text(localized(.nativeCopyrightNoticesComplaintAfterDecision))
        } else if euCase.reopenedAt != nil {
            Text(localized(.nativeCopyrightNoticesComplaintAfterRedecision))
        } else if euCase.complaint.canSubmit {
            Text(localized(.nativeCopyrightNoticesComplaintAvailable))
        } else {
            Text(localized(.nativeCopyrightNoticesComplaintCannotSubmit))
        }
    }

    @ViewBuilder
    private func complaintWindow(_ euCase: CopyrightEuParticipantCase) -> some View {
        if let windowEndsAt = euCase.complaint.windowEndsAt {
            Text(localized(.nativeCopyrightNoticesComplaintWindowEnds, parameters: [
                "date": localizedDate(windowEndsAt, style: .date)
            ]))
        }
    }

    @ViewBuilder
    private func complaintDecision(_ euCase: CopyrightEuParticipantCase) -> some View {
        if let decision = euCase.complaint.decision {
            Text(localized(decision.staffDisposition == "revoke"
                    ? .nativeCopyrightNoticesComplaintUpheld
                    : .nativeCopyrightNoticesComplaintMaintained))
            Text(decision.rationale).textSelection(.enabled)
        }
    }

    private var otherRedressSection: some View {
        section(.nativeCopyrightNoticesOtherRedressRoutes) {
            Text(localized(.nativeCopyrightNoticesEuOtherRedressRoutes))
        }
    }

    private var settlementsSection: some View {
        section(.nativeCopyrightNoticesDisputeSettlements) {
            if euSettlements.isEmpty {
                Text(localized(.nativeCopyrightNoticesNoDisputeSettlements))
            } else {
                ForEach(euSettlements) { settlement in settlementRow(settlement) }
            }
            settlementsPagination
        }
    }

    private func settlementRow(_ settlement: CopyrightEuDisputeSettlement) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(settlement.bodyName)
            Text(localized(.nativeCopyrightNoticesReferredDate, parameters: [
                "date": localizedDate(settlement.referredAt, style: .date)
            ]))
            settlementOutcome(settlement.outcome)
        }
    }

    @ViewBuilder
    private func settlementOutcome(_ outcome: CopyrightEuDisputeOutcome?) -> some View {
        if let outcome {
            Text(localized(.nativeCopyrightNoticesSettlementOutcome, parameters: [
                "result": outcome.result.replacingOccurrences(of: "_", with: " "),
                "date": localizedDate(outcome.decidedAt, style: .date)
            ]))
            if let implementedAt = outcome.implementedAt {
                Text(localized(.nativeCopyrightNoticesSettlementImplemented, parameters: [
                    "date": localizedDate(implementedAt, style: .date)
                ]))
            }
        } else {
            Text(localized(.nativeCopyrightNoticesNoSettlementOutcome))
        }
    }

    @ViewBuilder
    private var settlementsPagination: some View {
        if let euSettlementsPageInfo,
           euSettlementsPageInfo.hasNextPage,
           euSettlementsPageInfo.endCursor != nil {
            if paginationFailed {
                Text(localized(.nativeSwiftRouteSurfaceLoadMoreFailed)).foregroundStyle(.red)
            }
            Button(localized(paginationFailed
                    ? .nativeCommonRetry
                    : isLoading ? .nativeSwiftCommonLoadingMore : .nativeSwiftCommonLoadMore)) {
                Task { await viewModel.loadMoreSettlements() }
            }
            .disabled(isLoading)
            .accessibilityIdentifier("copyright-notice-settlements-load-more")
        }
    }
}
