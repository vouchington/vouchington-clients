import SwiftUI
import VouchaDesignSystem
import VouchaModels

struct CommunityTransparencyPaginationControl: View {
    @Bindable
    var viewModel: CommunityDetailViewModel

    var body: some View {
        if viewModel.selectedTab == .moderationAnalytics,
           viewModel.moderationTransparencyRange == .all {
            HybridPaginationControl(
                hasMore: viewModel.moderationTransparencyNextCursor != nil,
                isLoading: viewModel.moderationTransparencyIsLoadingOlder,
                hasError: viewModel.moderationTransparencyLoadMoreError != nil,
                accessibilityIdentifier: "community-moderation-transparency-pagination"
            ) {
                await viewModel.loadOlderCommunityModerationTransparency()
            }
        }
    }
}
