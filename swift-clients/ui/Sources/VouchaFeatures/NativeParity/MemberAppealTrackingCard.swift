import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct MemberAppealTrackingCard: View {
    @Environment(\.locale) private var locale
    let appeal: ModerationAppeal

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let targetTitle = appeal.memberTargetTitle {
                Text(UiMessages.string(targetTitle, locale: locale))
                    .font(Typography.headline)
            }
            LabeledContent(
                UiMessages.string(.nativeSwiftModerationAppealsStatus, locale: locale),
                value: UiMessages.string(appeal.memberStatusKey, locale: locale)
            )
            LabeledContent(
                UiMessages.string(.nativeSwiftModerationAppealsCreated, locale: locale),
                value: UiMessages.date(
                    appeal.createdAt,
                    date: .abbreviated,
                    time: .shortened,
                    locale: locale,
                    timeZone: .current
                )
            )
            if let publicResponse = appeal.publicResponse {
                Text(UiMessages.string(
                    .nativeSwiftModerationAppealsPublicResponse,
                    locale: locale
                )).font(Typography.caption)
                Text(verbatim: publicResponse)
            }
            if let approvedAt = appeal.approvedAt {
                lifecycleRow(.nativeSwiftModerationAppealsApproved, date: approvedAt)
            }
            if let sentAt = appeal.sentAt {
                lifecycleRow(.nativeSwiftModerationAppealsSent, date: sentAt)
            }
            if let resolvedAt = appeal.resolvedAt {
                lifecycleRow(
                    appeal.status == .dismissed
                        ? .nativeSwiftModerationAppealsDismissed
                        : .nativeSwiftModerationAppealsResolved,
                    date: resolvedAt
                )
            }
            if let action = appeal.resolutionAction {
                LabeledContent(
                    UiMessages.string(.nativeSwiftModerationAppealsResolution, locale: locale),
                    value: UiMessages.string(action.memberTitleKey, locale: locale)
                )
            }
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("member-appeal-tracking-\(appeal.id)")
    }

    private func lifecycleRow(_ key: UiMessageKey, date: Date) -> some View {
        LabeledContent(
            UiMessages.string(key, locale: locale),
            value: UiMessages.date(
                date,
                date: .abbreviated,
                time: .shortened,
                locale: locale,
                timeZone: .current
            )
        )
    }
}

private extension ModerationAppeal {
    var memberTargetTitle: UiVerbatimText? {
        guard let targetContext else {
            return .message(memberTargetTitleKey)
        }

        let context = switch targetContext {
        case let .warning(warning):
            firstMemberSafeContext(warning.publicMessage, warning.community?.name)
        case let .communityBan(ban):
            firstMemberSafeContext(ban.reason, ban.community.name)
        case let .postRemoval(removal):
            firstMemberSafeContext(removal.title)
        case let .suspension(suspension):
            firstMemberSafeContext(suspension.reason)
        }
        return context.map(UiVerbatimText.userContent)
    }

    private func firstMemberSafeContext(_ values: String?...) -> String? {
        values.compactMap { value in
            guard let value else { return nil }
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            return trimmed.isEmpty ? nil : trimmed
        }.first
    }

    var memberTargetTitleKey: UiMessageKey {
        if userWarningId != nil {
            return .nativeSwiftModerationAppealsWarningAppeal
        }
        if communityBanId != nil {
            return .nativeSwiftModerationAppealsCommunityBanAppeal
        }
        if postId != nil {
            return .nativeSwiftModerationAppealsPostRemovalAppeal
        }
        return .nativeSwiftModerationAppealsSuspensionAppeal
    }

    var memberStatusKey: UiMessageKey {
        switch status {
        case .pending: .nativeSwiftModerationAppealsPending
        case .resolved: .nativeSwiftModerationAppealsResolved
        case .dismissed: .nativeSwiftModerationAppealsDismissed
        }
    }
}

private extension ModerationAppealAction {
    var memberTitleKey: UiMessageKey {
        switch self {
        case .accept: .nativeSwiftModerationAppealsAccept
        case .reduce: .nativeSwiftModerationAppealsReduce
        case .deny: .nativeSwiftModerationAppealsDeny
        }
    }
}
