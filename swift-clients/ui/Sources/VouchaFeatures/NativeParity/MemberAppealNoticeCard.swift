import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct MemberAppealNoticeCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let target: MemberAppealTarget
    let canAppeal: Bool
    let onAppeal: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                Label(
                    UiMessages.string(target.titleKey, locale: nativeUiLocale),
                    systemImage: target.icon
                )
                .font(Typography.headline)
                Spacer()
                Text(UiMessages.date(
                    target.date,
                    date: .abbreviated,
                    time: .shortened,
                    locale: nativeUiLocale,
                    timeZone: .current
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            }
            if let context = target.authoredContext {
                Text(verbatim: context.value)
                    .font(Typography.body)
                    .authoredContentLanguage(
                        declared: context.declaredLanguage,
                        detected: context.detectedLanguage
                    )
            } else if let context = target.context {
                Text(verbatim: context)
                    .font(Typography.body)
            }
            Button(
                UiMessages.string(
                    canAppeal
                        ? .nativeSwiftModerationAppealsMemberFileAppeal
                        : .nativeSwiftModerationAppealsMemberPendingExists,
                    locale: nativeUiLocale
                ),
                action: onAppeal
            )
            .disabled(!canAppeal)
            .accessibilityIdentifier("member-appeal-\(target.id)")
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }
}

extension MemberAppealTarget {
    var titleKey: UiMessageKey {
        switch self {
        case .warning: .nativeSwiftModerationAppealsWarningAppeal
        case .ban: .nativeSwiftModerationAppealsCommunityBanAppeal
        case let .removal(_, _, _, _, _, kind, _): kind.memberAppealTitleKey
        case .suspension: .nativeSwiftModerationAppealsSuspensionAppeal
        }
    }

    var icon: String {
        switch self {
        case .warning: "exclamationmark.triangle"
        case .ban: "hand.raised"
        case .removal: "trash"
        case .suspension: "person.crop.circle.badge.xmark"
        }
    }

    var context: String? {
        switch self {
        case let .warning(_, message, community, _):
            message ?? community
        case let .ban(_, reason, community, _):
            reason ?? community
        case let .removal(_, _, community, _, _, _, _):
            community
        case .suspension:
            nil
        }
    }

    var authoredContext: NormalizedAuthoredText? {
        guard case let .removal(_, title, _, declared, detected, _, _) = self else { return nil }
        return NormalizedAuthoredText(text: title, declaredLanguage: declared, detectedLanguage: detected)
    }
}

private extension ModerationAppealPostRemovalKind {
    var memberAppealTitleKey: UiMessageKey {
        switch self {
        case .platform: .nativeSwiftModerationAppealsPlatformPostRemovalAppeal
        case .community: .nativeSwiftModerationAppealsCommunityPostRemovalAppeal
        }
    }
}
