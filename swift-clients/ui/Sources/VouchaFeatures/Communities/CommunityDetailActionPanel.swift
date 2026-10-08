import SwiftUI
import VouchaDesignSystem
import VouchaModels

struct CommunityDetailActionPanel: View {
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void
    var onNavigate: (String) -> Void = { _ in }

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

            if isSignedIn, viewModel.canModerateCommunity, viewModel.selectedTab == .settings {
                CommunityAutomodSettingsView(
                    viewModel: CommunityAutomodWorkspaceViewModel(
                        client: viewModel.client, slug: viewModel.slug,
                        action: viewModel.communityDetail?.community.automodAction,
                        canModerate: true
                    )
                )
                .id(viewModel.communityLoadRevision)
            }

            if isSignedIn, viewModel.canModerateCommunity, viewModel.selectedTab == .moderation {
                CommunityAutomodWorkspaceView(
                    viewModel: CommunityAutomodWorkspaceViewModel(
                        client: viewModel.client, slug: viewModel.slug,
                        action: viewModel.communityDetail?.community.automodAction,
                        canModerate: true
                    ),
                    onNavigate: onNavigate
                )
                .id(viewModel.communityLoadRevision)
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
