import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeUserProfileSurface {
    @ViewBuilder
    var scopedCollection: some View {
        switch viewModel.state {
        case .loading where viewModel.userProfile.collection.isEmpty:
            ProgressView().frame(maxWidth: .infinity)
        case let .error(error):
            ErrorStateView(error: error) { await viewModel.load() }
        default:
            if viewModel.userProfile.collection.isEmpty {
                EmptyStateView(
                    icon: "square.stack.3d.down.right",
                    title: .message(currentScope?.emptyTitle ?? .nativeSwiftEmptyStateNoNativeRows),
                    message: .message(.nativeSwiftProfileEmptySectionMessage)
                )
            } else {
                typedCollectionRows
            }
        }
    }

    @ViewBuilder
    private var typedCollectionRows: some View {
        switch viewModel.userProfile.collection {
        case .none:
            EmptyView()
        case let .posts(posts):
            ForEach(posts) { row in
                Button { onNavigate(NativeUserProfileNavigationTarget.post(row)) } label: {
                    PostCard(post: row.post, hideDownCount: hideDownCount)
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("public-profile-post-row")
            }
        case let .users(users):
            ForEach(users) { user in
                Button { onNavigate(NativeUserProfileNavigationTarget.user(user)) } label: {
                    UserRow(
                        user: user,
                        avatarURL: AppConfig.shared.imageURL(forImageId: user.profileImageId, width: 96)
                    )
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("public-profile-user-row")
            }
        case let .topics(topics):
            ForEach(topics) { topic in
                Button { onNavigate(NativeUserProfileNavigationTarget.topic(topic)) } label: {
                    NativeUserProfileTopicRow(topic: topic)
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("public-profile-topic-row")
            }
        case let .sources(sources):
            ForEach(sources) { source in
                Button { onNavigate(NativeUserProfileNavigationTarget.source(source)) } label: {
                    RssFeedSourceCard(source: source)
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("public-profile-source-row")
            }
        case let .communities(communities):
            ForEach(communities) { community in
                Button { onNavigate(NativeUserProfileNavigationTarget.community(community)) } label: {
                    NativeUserProfileCommunityRow(community: community)
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("public-profile-community-row")
            }
        }
    }
}

private struct NativeUserProfileTopicRow: View {
    let topic: Topic

    var body: some View {
        HStack(spacing: Spacing.sm) {
            Image(systemName: "tag")
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(topic.name).font(Typography.subheadline).fontWeight(.semibold)
                Text(topic.topicType.replacingOccurrences(of: "_", with: " "))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            Spacer(minLength: 0)
        }
        .padding(.vertical, Spacing.xs)
    }
}

private struct NativeUserProfileCommunityRow: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let community: Community

    var body: some View {
        HStack(spacing: Spacing.sm) {
            Image(systemName: "person.3")
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(community.name).font(Typography.subheadline).fontWeight(.semibold)
                Text(UiMessages.string(community.visibility.titleKey, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            Spacer(minLength: 0)
        }
        .padding(.vertical, Spacing.xs)
    }
}
