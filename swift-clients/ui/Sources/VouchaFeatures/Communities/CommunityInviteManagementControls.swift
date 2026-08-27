import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityInviteManagementControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var inviteId = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesInviteManagement, locale: nativeUiLocale))
                .font(Typography.headline)
            TextField(UiMessages.string(.nativeSwiftCommunitiesInviteId, locale: nativeUiLocale), text: $inviteId)
                .textFieldStyle(.roundedBorder)
            Button(
                UiMessages.string(.nativeSwiftCommunitiesRevokeInvite, locale: nativeUiLocale),
                systemImage: "trash"
            ) {
                guard isSignedIn else {
                    showSignIn()
                    return
                }
                Task { await viewModel.revokeInvite(inviteId: inviteId) }
            }
        }
    }
}

extension CommunityListItemType {
    var titleKey: UiMessageKey {
        switch self {
        case .topic: .nativeSwiftNavigationTitlesListTopics
        case .rssFeed: .nativeSwiftNavigationTitlesListSources
        case .post: .nativeSwiftNavigationTitlesListPosts
        case .urlHostname: .nativeSwiftNavigationTitlesListDomains
        case .url: .nativeSwiftNavigationTitlesListUrls
        }
    }
}
