import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension ModerationAppealCard {
    @ViewBuilder
    var appealContext: some View {
        if let context = appeal.targetContext {
            switch context {
            case let .warning(warning):
                contextValue(.nativeSwiftModerationAppealsDecisionContext, value: warning.publicMessage)
                contextValue(.nativeSwiftModerationAppealsCommunityContext, value: warning.community?.name)
            case let .communityBan(ban):
                contextValue(.nativeSwiftModerationAppealsDecisionContext, value: ban.reason)
                contextValue(.nativeSwiftModerationAppealsCommunityContext, value: ban.community.name)
            case let .postRemoval(removal):
                contextValue(.nativeSwiftModerationAppealsPostContext, value: removal.title)
                contextValue(.nativeSwiftModerationAppealsDecisionContext, value: removal.publicReason)
                contextValue(.nativeSwiftModerationAppealsCommunityContext, value: removal.community?.name)
            case let .suspension(suspension):
                contextValue(.nativeSwiftModerationAppealsDecisionContext, value: suspension.reason)
            }
        }
        if let staff = appeal.staffContext {
            contextValue(
                .nativeSwiftModerationAppealsAppellantId,
                parameter: "id",
                value: actorLabel(staff.appellant)
            )
            contextValue(
                .nativeSwiftModerationAppealsOriginalDecisionReason,
                value: staff.originalDecision.internalReason
            )
            if let actor = staff.originalDecision.actor {
                contextValue(.nativeSwiftModerationAppealsOriginalDecisionActor, value: actorLabel(actor))
            }
        }
    }

    @ViewBuilder
    private func contextValue(
        _ key: UiMessageKey,
        parameter: String = "value",
        value: String?
    ) -> some View {
        if let value, !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            Text(UiMessages.string(
                key,
                parameters: [parameter: value],
                locale: nativeUiLocale
            ))
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
        }
    }

    private func actorLabel(_ actor: ModerationActorSummary) -> String {
        actor.verifiedDisplayName ?? actor.username ?? actor.id
    }

    @ViewBuilder
    var lifecycle: some View {
        if let draftedAt = appeal.draftedAt {
            lifecycleRow(label: .nativeSwiftModerationAppealsDrafted, date: draftedAt)
        }
        if let editedAt = appeal.editedAt {
            lifecycleRow(label: .nativeSwiftModerationAppealsEdited, date: editedAt, actorId: appeal.editedById)
        }
        if let approvedAt = appeal.approvedAt {
            lifecycleRow(label: .nativeSwiftModerationAppealsApproved, date: approvedAt, actorId: appeal.approvedById)
        }
        if let sentAt = appeal.sentAt {
            lifecycleRow(label: .nativeSwiftModerationAppealsSent, date: sentAt)
        }
        if let resolvedAt = appeal.resolvedAt {
            lifecycleRow(label: .nativeSwiftModerationAppealsResolved, date: resolvedAt, actorId: appeal.resolvedById)
        }
        if let action = appeal.resolutionAction {
            Text(UiMessages.string(
                .nativeSwiftModerationAppealsResolution,
                parameters: ["action": UiMessages.string(action.titleKey, locale: nativeUiLocale)],
                locale: nativeUiLocale
            ))
            .font(Typography.subheadline)
        }
    }

    func lifecycleRow(label: UiMessageKey, date: Date, actorId: String? = nil) -> some View {
        HStack(spacing: Spacing.xs) {
            Text(UiMessages.string(label, locale: nativeUiLocale)).font(Typography.subheadline)
            Text(UiMessages.date(
                date,
                date: .abbreviated,
                time: .shortened,
                locale: nativeUiLocale,
                timeZone: .current
            ))
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
            if let actorId {
                Text(UiMessages.string(
                    .nativeSwiftModerationAppealsByActor,
                    parameters: ["actor": actorId],
                    locale: nativeUiLocale
                ))
                .font(Typography.caption.monospaced())
                .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

    @ViewBuilder
    var staffContext: some View {
        if let response = appeal.aiInternalResponse, !response.isEmpty {
            DisclosureGroup(UiMessages.string(
                .nativeSwiftModerationAppealsInternalAiResponse,
                locale: nativeUiLocale
            )) {
                Text(response).frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        if let notes = appeal.internalNotes, !notes.isEmpty {
            DisclosureGroup(UiMessages.string(
                .nativeSwiftModerationAppealsInternalNotes,
                locale: nativeUiLocale
            )) {
                Text(notes).frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }
}
