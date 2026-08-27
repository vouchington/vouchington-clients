import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension FriendsListView {
    func followButton(for user: PublicUser) -> some View {
        let following = viewModel.isFollowing(userId: user.id)
        let canToggle = viewModel.canToggleFollow(userId: user.id)
        return Button {
            Task { await viewModel.toggleFollow(userId: user.id) }
        } label: {
            Text(UiMessages.string(
                following ? .nativeSwiftFriendsUnfollow : .nativeSwiftDesignSystemFollow,
                locale: nativeUiLocale
            ))
            .font(Typography.caption)
            .padding(.horizontal, Spacing.sm)
            .padding(.vertical, Spacing.xs)
        }
        .buttonStyle(.bordered)
        .disabled(!canToggle)
        .tint(following ? .secondary : Colors.primary)
    }
}
