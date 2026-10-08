import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityAutomodActionForm: View {
    @Environment(\.locale)
    private var locale
    @Bindable
    var viewModel: CommunityAutomodWorkspaceViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(
                .extractedCommunitiesCommunityAutomodActionFormChooseWhatHappensToApublishedPostB2e29694,
                locale: locale
            ))
            Picker(
                UiMessages.string(.extractedCommunitiesCommunityAutomodActionFormAutomodAction83211784, locale: locale),
                selection: $viewModel.selectedAction
            ) {
                ForEach([CommunityAutomodActionSetting.recordOnly, .reviewQueue, .unpublish], id: \.self) { action in
                    Text(UiMessages.string(action.nativeTitleKey, locale: locale)).tag(Optional(action))
                }
            }
            .accessibilityIdentifier("community-automod-action-picker")
            .disabled(viewModel.isSavingAction)
            if let action = viewModel.selectedAction {
                Text(UiMessages.string(action.nativeDescriptionKey, locale: locale))
                    .foregroundStyle(.secondary)
            }
            Button(UiMessages.string(
                viewModel.isSavingAction
                    ? .extractedCommunitiesCommunityAutomodActionFormSavingDc85af8f
                    : .extractedCommunitiesCommunityAutomodActionFormSaveAutomodAction5f2b209e,
                locale: locale
            )) {
                Task { await viewModel.saveAction() }
            }
            .disabled(viewModel.isSavingAction || viewModel.selectedAction == nil)
            .accessibilityIdentifier("community-save-automod-action")
        }
    }
}

struct CommunityAutomodSettingsView: View {
    @Environment(\.locale)
    private var locale
    @State
    var viewModel: CommunityAutomodWorkspaceViewModel

    var body: some View {
        if viewModel.canModerate {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                CommunityAutomodActionForm(viewModel: viewModel)
                if let notice = viewModel.notice {
                    Text(UiMessages.string(notice, locale: locale))
                }
                if let error = viewModel.mutationError {
                    Text(UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                }
            }
        }
    }
}

extension CommunityAutomodActionSetting {
    var nativeTitleKey: UiMessageKey {
        switch self {
        case .recordOnly: .extractedCommunitiesCommunityAutomodActionFormRecordOnlyC9184072
        case .reviewQueue: .extractedCommunitiesCommunityAutomodActionFormSendToReviewQueue1c3a1b74
        case .unpublish: .extractedCommunitiesCommunityAutomodActionFormUnpublish2db04a54
        }
    }

    var nativeDescriptionKey: UiMessageKey {
        switch self {
        case .recordOnly: .extractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedTheFlagAppears01b0cba0
        case .reviewQueue: .extractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedAndAddIt56f75b30
        case .unpublish: .extractedCommunitiesCommunityAutomodActionFormRemoveThePostFromTheCommunityRight215c4ec5
        }
    }
}
