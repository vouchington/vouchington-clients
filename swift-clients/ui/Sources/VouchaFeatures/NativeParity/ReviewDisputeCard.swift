import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ReviewDisputeCard: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: ReviewDisputesViewModel
    let dispute: ReviewDispute

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                Label(localized(.nativeSwiftReviewDisputesReviewDispute), systemImage: "exclamationmark.bubble")
                    .font(Typography.headline)
                Spacer()
                Text(UiMessages.string(dispute.status.titleKey, locale: nativeUiLocale))
                    .font(Typography.caption)
                if dispute.isOverdue == true {
                    Text(localized(.nativeSwiftReviewDisputesOverdue))
                        .font(Typography.caption)
                        .foregroundStyle(.red)
                }
            }
            reviewContext
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(localized(.nativeSwiftReviewDisputesClaim)).font(Typography.subheadline)
                Text(dispute.claimText).font(Typography.body)
            }
            if let recommendation = dispute.recommendedAction {
                Label(UiMessages.string(
                    .nativeSwiftReviewDisputesAiRecommendation,
                    parameters: [
                        "recommendation": UiMessages.string(recommendation.titleKey, locale: nativeUiLocale)
                    ],
                    locale: nativeUiLocale
                ), systemImage: "sparkles")
                    .font(Typography.subheadline)
            }
            lifecycle
            staffContext
            if dispute.status == .pending {
                editors
                actionButtons
            } else if let response = dispute.publicResponse, !response.isEmpty {
                labeledText(.nativeSwiftReviewDisputesPublicResponse, response)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("review-dispute-\(dispute.id)")
        .onDisappear {
            viewModel.cancelRerunAI(for: dispute.id)
        }
    }

    private var editors: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(localized(.nativeSwiftReviewDisputesPublicResponse)).font(Typography.subheadline)
            TextEditor(text: Binding(
                get: { viewModel.publicDraft(for: dispute) },
                set: { viewModel.setPublicResponseDraft($0, for: dispute) }
            ))
            .frame(minHeight: 90)
            .disabled(!viewModel.canEdit(dispute))
            .accessibilityLabel(localized(
                .nativeSwiftReviewDisputesPublicResponseAccessibility,
                parameters: ["id": dispute.id]
            ))
            if let draft = dispute.aiPublicResponse, !draft.isEmpty {
                DisclosureGroup(localized(.nativeSwiftReviewDisputesAiPublicDraft)) {
                    Text(draft).frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            Text(localized(.nativeSwiftReviewDisputesAnnotation)).font(Typography.subheadline)
            TextEditor(text: Binding(
                get: { viewModel.annotationDraft(for: dispute) },
                set: { viewModel.setAnnotationDraft($0, for: dispute) }
            ))
            .frame(minHeight: 80)
            .disabled(!(viewModel.canMutate(dispute) && dispute.sentAt != nil))
            .accessibilityLabel(localized(
                .nativeSwiftReviewDisputesAnnotationAccessibility,
                parameters: ["id": dispute.id]
            ))
        }
    }

    func localized(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        UiMessages.string(key, parameters: parameters, locale: nativeUiLocale)
    }

    func labeledText(_ key: UiMessageKey, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(localized(key)).font(Typography.subheadline)
            Text(value).font(Typography.body)
        }
    }
}
