import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension ReviewDisputeCard {
    @ViewBuilder
    var reviewContext: some View {
        let context = dispute.staffContext
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(localized(.nativeSwiftReviewDisputesDisputant))
            Text(disputantLabel).font(Typography.subheadline)
            if let review = context?.review {
                Text(review.post.title.isEmpty ? dispute.postId : review.post.title)
                    .font(Typography.subheadline)
                if !review.post.markdownPreview.isEmpty {
                    Text(review.post.markdownPreview)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                Text(localized(.nativeSwiftReviewDisputesTopic))
                Text(review.topic?.name ?? dispute.topicId).font(Typography.subheadline)
                Text(localized(
                    .nativeSwiftReviewDisputesRating,
                    parameters: ["rating": String(review.rating)]
                ))
            } else {
                Text(dispute.postId).font(Typography.subheadline)
                Text(localized(.nativeSwiftReviewDisputesTopic))
                Text(dispute.topicId).font(Typography.subheadline)
            }
            Text(localized(
                .nativeSwiftReviewDisputesCreated,
                parameters: ["date": localizedDate(dispute.createdAt)]
            ))
            Text(localized(
                .nativeSwiftReviewDisputesUpdated,
                parameters: ["date": localizedDate(dispute.updatedAt)]
            ))
        }
        .font(Typography.caption)
        .foregroundStyle(Colors.secondaryLabel)
    }

    @ViewBuilder
    var lifecycle: some View {
        if let date = dispute.draftedAt {
            lifecycleRow(.nativeSwiftReviewDisputesDrafted, date: date)
        }
        if let date = dispute.editedAt {
            lifecycleRow(.nativeSwiftReviewDisputesEdited, date: date, actorId: dispute.editedById)
        }
        if let date = dispute.approvedAt {
            lifecycleRow(.nativeSwiftReviewDisputesApproved, date: date, actorId: dispute.approvedById)
        }
        if let date = dispute.sentAt {
            lifecycleRow(.nativeSwiftReviewDisputesDelivered, date: date)
        }
        if let date = dispute.resolvedAt {
            lifecycleRow(.nativeSwiftReviewDisputesResolved, date: date, actorId: dispute.resolvedById)
        }
        if let action = dispute.resolutionAction {
            Text(UiMessages.string(
                .nativeSwiftReviewDisputesResolution,
                parameters: ["action": UiMessages.string(action.titleKey, locale: nativeUiLocale)],
                locale: nativeUiLocale
            ))
            .font(Typography.subheadline)
        }
    }

    @ViewBuilder
    var staffContext: some View {
        if let response = dispute.aiInternalResponse, !response.isEmpty {
            DisclosureGroup(localized(.nativeSwiftReviewDisputesInternalAiResponse)) {
                Text(response).frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        if let notes = dispute.internalNotes, !notes.isEmpty {
            DisclosureGroup(localized(.nativeSwiftReviewDisputesInternalNotes)) {
                Text(notes).frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }

    private var disputantLabel: String {
        guard let actor = dispute.staffContext?.disputant else { return dispute.disputantUserId }
        if let name = actor.verifiedDisplayName, !name.isEmpty {
            return name
        }
        if let username = actor.username, !username.isEmpty {
            return username
        }
        return actor.id
    }

    private func lifecycleRow(_ key: UiMessageKey, date: Date, actorId: String? = nil) -> some View {
        HStack(spacing: Spacing.xs) {
            Text(localized(key)).font(Typography.subheadline)
            Text(localizedDate(date)).font(Typography.caption).foregroundStyle(Colors.secondaryLabel)
            if let actorId {
                Text(localized(.nativeSwiftReviewDisputesByActor, parameters: ["actor": actorId]))
                    .font(Typography.caption.monospaced())
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

    private func localizedDate(_ date: Date) -> String {
        UiMessages.date(
            date,
            date: .abbreviated,
            time: .shortened,
            locale: nativeUiLocale,
            timeZone: .current
        )
    }
}
