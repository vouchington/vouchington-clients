import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension ModerationAppealCard {
    var actionButtons: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            LazyVGrid(columns: actionColumns, alignment: .leading, spacing: Spacing.sm) {
                Button(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale)) {
                    Task { await viewModel.saveDraft(for: appeal) }
                }
                .disabled(!viewModel.canEdit(appeal))
                .accessibilityLabel(localized(
                    .nativeSwiftModerationAppealsSaveResponseAccessibility,
                    parameters: ["id": appeal.id]
                ))
                Button(localized(.nativeSwiftModerationAppealsApprove)) {
                    Task { await viewModel.approve(appeal) }
                }
                .disabled(!viewModel.canApprove(appeal))
                .accessibilityLabel(localized(
                    .nativeSwiftModerationAppealsApproveAccessibility,
                    parameters: ["id": appeal.id]
                ))
                Button(localized(.nativeSwiftModerationAppealsSend)) {
                    Task { await viewModel.send(appeal) }
                }
                .disabled(!viewModel.canSend(appeal))
                .accessibilityLabel(localized(
                    .nativeSwiftModerationAppealsSendAccessibility,
                    parameters: ["id": appeal.id]
                ))
                Button(localized(.nativeSwiftModerationAppealsRerunAi)) {
                    Task { await viewModel.rerunAI(for: appeal) }
                }
                .disabled(!viewModel.canRerun(appeal))
                .accessibilityLabel(localized(
                    .nativeSwiftModerationAppealsRerunAiAccessibility,
                    parameters: ["id": appeal.id]
                ))
                ForEach([ModerationAppealAction.accept, .reduce, .deny], id: \.self) { action in
                    Button(UiMessages.string(action.titleKey, locale: nativeUiLocale)) {
                        Task { await viewModel.resolve(appeal, action: action) }
                    }
                    .disabled(!viewModel.canResolve(appeal, action: action))
                    .accessibilityLabel(localized(
                        .nativeSwiftModerationAppealsActionAccessibility,
                        parameters: [
                            "action": UiMessages.string(action.titleKey, locale: nativeUiLocale),
                            "id": appeal.id
                        ]
                    ))
                }
                if viewModel.ambiguousDeliveryAppealIds.contains(appeal.id) {
                    Button(localized(.nativeSwiftModerationAppealsRefreshDelivery)) {
                        Task { await viewModel.clearAmbiguousDelivery(for: appeal.id) }
                    }
                    .accessibilityLabel(localized(
                        .nativeSwiftModerationAppealsRefreshDeliveryAccessibility,
                        parameters: ["id": appeal.id]
                    ))
                }
            }
        }
    }

    private var actionColumns: [GridItem] {
        [GridItem(.adaptive(minimum: 112), alignment: .leading)]
    }
}
