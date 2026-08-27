import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ModerationAppealCard: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: ModerationAppealsViewModel
    let appeal: ModerationAppeal

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                Label(
                    UiMessages.string(appeal.targetTypeKey, locale: nativeUiLocale),
                    systemImage: "arrow.uturn.left.circle"
                )
                .font(Typography.headline)
                Spacer()
                Text(UiMessages.string(appeal.status.titleKey, locale: nativeUiLocale))
                    .font(Typography.caption)
                if appeal.isOverdue == true {
                    Text(UiMessages.string(.nativeSwiftModerationAppealsOverdue, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(.red)
                }
            }
            context
            appealContext
            Text(appeal.appealReason
                ?? UiMessages.string(.nativeSwiftModerationAppealsNoReason, locale: nativeUiLocale))
                .font(Typography.body)
            if let recommendation = appeal.recommendedAction {
                Label(UiMessages.string(
                    .nativeSwiftModerationAppealsAiRecommendation,
                    parameters: [
                        "recommendation": UiMessages.string(recommendation.titleKey, locale: nativeUiLocale)
                    ],
                    locale: nativeUiLocale
                ), systemImage: "sparkles")
                    .font(Typography.subheadline)
            }
            lifecycle
            staffContext
            if appeal.status == .pending {
                draftEditor
                actionButtons
            } else if let response = appeal.publicResponse, !response.isEmpty {
                Text(response).font(Typography.body)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("appeal-\(appeal.id)")
    }

    private var context: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(localized(.nativeSwiftModerationAppealsAppealId, parameters: ["id": appeal.id]))
            if let id = appeal.caseId {
                Text(localized(.nativeSwiftModerationAppealsCaseId, parameters: ["id": id]))
            }
            if let id = appeal.appellantId {
                Text(localized(.nativeSwiftModerationAppealsAppellantId, parameters: ["id": id]))
            }
            if let id = appeal.targetId {
                Text(localized(.nativeSwiftModerationAppealsTargetId, parameters: ["id": id]))
            }
            Text(localized(
                .nativeSwiftModerationAppealsCreated,
                parameters: ["date": localizedDate(appeal.createdAt)]
            ))
            Text(localized(
                .nativeSwiftModerationAppealsUpdated,
                parameters: ["date": localizedDate(appeal.updatedAt)]
            ))
        }
        .font(Typography.caption.monospaced())
        .foregroundStyle(Colors.secondaryLabel)
    }

    private var draftEditor: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(localized(.nativeSwiftModerationAppealsPublicResponse)).font(Typography.subheadline)
            TextEditor(text: Binding(
                get: { viewModel.drafts[appeal.id] ?? "" },
                set: { viewModel.setDraft($0, for: appeal) }
            ))
            .frame(minHeight: 90)
            .disabled(!viewModel.canEdit(appeal))
            .accessibilityIdentifier("appeal-draft-\(appeal.id)")
            .accessibilityLabel(localized(
                .nativeSwiftModerationAppealsPublicResponseAccessibility,
                parameters: ["id": appeal.id]
            ))
            if let aiDraft = appeal.aiPublicResponse, !aiDraft.isEmpty {
                DisclosureGroup(localized(.nativeSwiftModerationAppealsAiPublicDraft)) {
                    Text(aiDraft).frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
    }

    func localized(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        UiMessages.string(key, parameters: parameters, locale: nativeUiLocale)
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

extension ModerationAppeal {
    var targetId: String? {
        userWarningId ?? userSuspensionId ?? communityBanId ?? postId
    }

    var targetTypeKey: UiMessageKey {
        if userWarningId != nil {
            return .nativeSwiftModerationAppealsWarningAppeal
        }
        if userSuspensionId != nil {
            return .nativeSwiftModerationAppealsSuspensionAppeal
        }
        if communityBanId != nil {
            return .nativeSwiftModerationAppealsCommunityBanAppeal
        }
        return .nativeSwiftModerationAppealsPostRemovalAppeal
    }
}

extension ModerationAppealAction {
    var titleKey: UiMessageKey {
        switch self {
        case .accept: .nativeSwiftModerationAppealsAccept
        case .reduce: .nativeSwiftModerationAppealsReduce
        case .deny: .nativeSwiftModerationAppealsDeny
        }
    }
}
