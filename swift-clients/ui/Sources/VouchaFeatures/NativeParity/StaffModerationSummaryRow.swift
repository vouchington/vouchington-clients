import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct StaffModerationSummaryRow: View {
    @Environment(\.locale)
    private var locale
    private let actor: ReviewDisputeActorSummary
    private let status: UiMessageKey
    private let reason: UiVerbatimText?
    private let context: String?
    private let createdAt: Date
    private let hasDraft: Bool
    private let hasAiDraft: Bool
    private let response: String?
    private let isOverdue: Bool
    private let overdueKey: UiMessageKey
    private let recommendation: UiMessageKey?
    private let recommendationKey: UiMessageKey

    init(dispute: ReviewDisputeSummary) {
        isOverdue = dispute.isOverdue == true
        overdueKey = .nativeSwiftReviewDisputesOverdue
        recommendation = dispute.recommendedAction?.titleKey
        recommendationKey = .nativeSwiftReviewDisputesAiRecommendation
        actor = dispute.disputant
        status = dispute.status.titleKey
        reason = .protocolValue(dispute.reason.rawValue)
        context = dispute.postLabel
        createdAt = dispute.createdAt
        hasDraft = dispute.hasDraft
        hasAiDraft = dispute.hasAiDraft
        response = dispute.sentAt == nil ? nil : dispute.publicResponse
    }

    init(appeal: ModerationAppealSummary) {
        isOverdue = appeal.isOverdue == true
        overdueKey = .nativeSwiftModerationAppealsOverdue
        recommendation = appeal.recommendedAction?.titleKey
        recommendationKey = .nativeSwiftModerationAppealsAiRecommendation
        actor = appeal.appellant
        status = appeal.status.titleKey
        switch appeal.targetContext {
        case let .warning(value):
            reason = value.publicMessage.map(UiVerbatimText.verbatim)
            context = value.community?.name
        case let .communityBan(value):
            reason = value.reason.map(UiVerbatimText.verbatim)
            context = value.community.name
        case let .postRemoval(value):
            reason = value.publicReason.map(UiVerbatimText.verbatim)
            context = value.title
        case let .suspension(value):
            reason = value.reason.map(UiVerbatimText.verbatim)
            context = nil
        case nil:
            reason = nil
            context = nil
        }
        createdAt = appeal.createdAt
        hasDraft = appeal.hasDraft
        hasAiDraft = appeal.hasAiDraft
        response = appeal.sentAt == nil ? nil : appeal.publicResponse
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(actorLabel)
                .font(Typography.headline)
            Text(UiMessages.string(status, locale: locale))
            if isOverdue {
                Text(UiMessages.string(overdueKey, locale: locale))
                    .foregroundStyle(.red)
            }
            if let recommendation {
                Label(UiMessages.string(recommendationKey, parameters: [
                    "recommendation": UiMessages.string(recommendation, locale: locale)
                ], locale: locale), systemImage: "sparkles")
            }
            if let context { Text(verbatim: context) }
            if let reason { Text(UiMessages.string(reason, locale: locale)) }
            Text(UiMessages.abbreviatedDateTime(createdAt, locale: locale))
            if hasDraft {
                Label(
                    UiMessages.string(.nativeSwiftPostComposeDrafts, locale: locale),
                    systemImage: "doc.text"
                )
            }
            if hasAiDraft {
                Label(
                    UiMessages.string(.nativeSwiftModerationAppealsAiPublicDraft, locale: locale),
                    systemImage: "sparkles"
                )
            }
            if let response { Text(verbatim: response) }
        }
        .padding(Spacing.md)
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var actorLabel: String {
        [actor.verifiedDisplayName, actor.username].compactMap { $0?.trimmedOrNil }.first
            ?? UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsDeletedUser, locale: locale)
    }

}
