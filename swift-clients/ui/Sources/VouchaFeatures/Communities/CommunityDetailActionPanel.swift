import SwiftUI
import VouchaDesignSystem
import VouchaModels

struct CommunityDetailActionPanel: View {
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if viewModel.canModerateCommunity, let listItemType = viewModel.selectedTab.listItemType {
                CommunityListItemControls(
                    viewModel: viewModel,
                    listItemType: listItemType,
                    isSignedIn: isSignedIn,
                    showSignIn: showSignIn
                )
            }

            if viewModel.canModerateCommunity, viewModel.selectedTab == .pinnedPosts {
                CommunityPinnedPostControls(viewModel: viewModel, isSignedIn: isSignedIn, showSignIn: showSignIn)
            }

            if viewModel.canModerateCommunity, viewModel.selectedTab == .applications {
                CommunityApplicationReviewControls(
                    viewModel: viewModel,
                    isSignedIn: isSignedIn,
                    showSignIn: showSignIn
                )
            }

            if viewModel.canModerateCommunity, viewModel.selectedTab == .invites {
                CommunityInviteManagementControls(
                    viewModel: viewModel,
                    isSignedIn: isSignedIn,
                    showSignIn: showSignIn
                )
            }

            if viewModel.canModerateCommunity, viewModel.selectedTab == .members {
                CommunityMemberControls(viewModel: viewModel, isSignedIn: isSignedIn, showSignIn: showSignIn)
            }

            if viewModel.selectedTab == .modmail {
                CommunityModmailControls(
                    viewModel: viewModel,
                    isSignedIn: isSignedIn,
                    canModerate: viewModel.canModerateCommunity,
                    showSignIn: showSignIn
                )
            }

            if viewModel.selectedTab != .modmail,
               viewModel.canModerateCommunity,
               viewModel.selectedTab.isManagementTab || viewModel.selectedTab == .moderation {
                CommunityModerationAdminControls(
                    viewModel: viewModel,
                    isSignedIn: isSignedIn,
                    showSignIn: showSignIn
                )
                CommunityAgentAndAutomodControls(
                    viewModel: viewModel,
                    isSignedIn: isSignedIn,
                    showSignIn: showSignIn
                )
            }

            if viewModel.selectedTab != .modmail,
               viewModel.canModerateCommunity,
               viewModel.selectedTab.isManagementTab || viewModel.selectedTab == .moderation {
                CommunityVacationControls(viewModel: viewModel, isSignedIn: isSignedIn, showSignIn: showSignIn)
            }
        }
    }
}
