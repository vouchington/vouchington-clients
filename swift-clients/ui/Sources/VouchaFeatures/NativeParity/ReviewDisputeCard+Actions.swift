import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension ReviewDisputeCard {
    var actionButtons: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 112), alignment: .leading)], spacing: Spacing.sm) {
            Button(localized(.nativeSwiftReviewDisputesSave)) {
                Task { await viewModel.savePublicResponse(for: dispute) }
            }
            .disabled(!viewModel.canEdit(dispute))
            Button(localized(.nativeSwiftReviewDisputesApprove)) {
                Task { await viewModel.approve(dispute) }
            }
            .disabled(!viewModel.canApprove(dispute))
            Button(localized(.nativeSwiftReviewDisputesDeliver)) {
                Task { await viewModel.deliver(dispute) }
            }
            .disabled(!viewModel.canDeliver(dispute))
            Button(localized(.nativeSwiftReviewDisputesRerunAi)) {
                viewModel.startRerunAI(for: dispute)
            }
            .disabled(!viewModel.canRerun(dispute))
            ForEach(
                [ReviewDisputeResolutionAction.remove, .annotate, .dismiss],
                id: \.self
            ) { action in
                Button(UiMessages.string(action.titleKey, locale: nativeUiLocale)) {
                    Task { await viewModel.resolve(dispute, action: action) }
                }
                .disabled(!viewModel.canResolve(dispute, action: action))
                .accessibilityLabel(UiMessages.string(
                    .nativeSwiftReviewDisputesActionAccessibility,
                    parameters: [
                        "action": UiMessages.string(action.titleKey, locale: nativeUiLocale),
                        "id": dispute.id
                    ],
                    locale: nativeUiLocale
                ))
            }
            if viewModel.ambiguousDisputeIds.contains(dispute.id) {
                Button(localized(.nativeSwiftReviewDisputesRefreshStatus)) {
                    Task { await viewModel.refreshAmbiguousMutation(for: dispute.id) }
                }
            }
        }
    }
}

private extension ReviewDisputeResolutionAction {
    var titleKey: UiMessageKey {
        switch self {
        case .remove: .nativeTaxonomyModerationRemove
        case .annotate: .nativeTaxonomyModerationAnnotate
        case .dismiss: .nativeSwiftReviewDisputesDismiss
        }
    }
}
