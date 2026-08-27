import SwiftUI
import VouchaLocalization
import VouchaModels

/// A list row card for an RSS feed source (news outlet, podcast, YouTube channel, etc.).
public struct RssFeedSourceCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    public let source: RssFeedSource
    public let isFollowing: Bool
    public let isFollowingTopic: Bool
    public let isMutedSource: Bool
    public let isMutedTopic: Bool
    public let followAction: (() -> Void)?
    public let muteAction: (() -> Void)?
    public let followTopicAction: (() -> Void)?
    public let muteTopicAction: (() -> Void)?
    public let topicElection: TopicElection?
    public let myTopicVote: ElectionVoteChoice?
    public let canCreateTopicVote: Bool
    public let onTopicVote: ((ElectionVoteChoice?) -> Void)?
    public let onTopicSignedOutTap: (() -> Void)?

    public init(
        source: RssFeedSource,
        isFollowing: Bool = false,
        isFollowingTopic: Bool = false,
        isMutedSource: Bool = false,
        isMutedTopic: Bool = false,
        followAction: (() -> Void)? = nil,
        muteAction: (() -> Void)? = nil,
        followTopicAction: (() -> Void)? = nil,
        muteTopicAction: (() -> Void)? = nil,
        topicElection: TopicElection? = nil,
        myTopicVote: ElectionVoteChoice? = nil,
        canCreateTopicVote: Bool = true,
        onTopicVote: ((ElectionVoteChoice?) -> Void)? = nil,
        onTopicSignedOutTap: (() -> Void)? = nil
    ) {
        self.source = source
        self.isFollowing = isFollowing
        self.isFollowingTopic = isFollowingTopic
        self.isMutedSource = isMutedSource
        self.isMutedTopic = isMutedTopic
        self.followAction = followAction
        self.muteAction = muteAction
        self.followTopicAction = followTopicAction
        self.muteTopicAction = muteTopicAction
        self.topicElection = topicElection
        self.myTopicVote = myTopicVote
        self.canCreateTopicVote = canCreateTopicVote
        self.onTopicVote = onTopicVote
        self.onTopicSignedOutTap = onTopicSignedOutTap
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack(alignment: .top, spacing: Spacing.md) {
                RssFeedSourceThumbnail(source: source)
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(source.title)
                        .font(Typography.subheadline)
                        .fontWeight(.semibold)
                        .lineLimit(2)
                    Text(source.displayHost)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                    Text(verbatim: UiMessages.string(source.cardFeedTypeLabel, locale: nativeUiLocale))
                        .font(Typography.caption2)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                Spacer(minLength: 0)
                HStack(spacing: Spacing.xs) {
                    if let followAction {
                        SourceCardActionButton(
                            isActive: isFollowing,
                            activeTitle: .nativeSwiftDesignSystemFollowing,
                            inactiveTitle: .nativeSwiftDesignSystemFollow,
                            action: followAction
                        )
                    }
                    if let muteAction {
                        SourceCardActionButton(
                            isActive: isMutedSource,
                            activeTitle: .nativeSwiftDesignSystemMuted,
                            inactiveTitle: .nativeSwiftDesignSystemMute,
                            activeIcon: "speaker.slash.fill",
                            inactiveIcon: "speaker.slash",
                            action: muteAction
                        )
                    }
                }
            }
            topicRow
        }
        .padding(.vertical, Spacing.sm)
    }

    @ViewBuilder
    private var topicRow: some View {
        if let topic = source.topic {
            HStack(spacing: Spacing.sm) {
                Label(topic.name, systemImage: "tag")
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                    .lineLimit(1)
                Spacer(minLength: 0)
                HStack(spacing: Spacing.xs) {
                    if let followTopicAction {
                        SourceCardActionButton(
                            isActive: isFollowingTopic,
                            activeTitle: .nativeSwiftDesignSystemTopicFollowed,
                            inactiveTitle: .nativeSwiftDesignSystemFollowTopic,
                            action: followTopicAction
                        )
                    }
                    if let muteTopicAction {
                        SourceCardActionButton(
                            isActive: isMutedTopic,
                            activeTitle: .nativeSwiftDesignSystemTopicMuted,
                            inactiveTitle: .nativeSwiftDesignSystemMuteTopic,
                            action: muteTopicAction
                        )
                    }
                }
                if let topicElection {
                    VoteControls(
                        election: topicElection,
                        myVote: myTopicVote,
                        policy: .sentiment,
                        canCreateVote: canCreateTopicVote,
                        onVote: onTopicVote,
                        onSignedOutTap: onTopicSignedOutTap
                    )
                }
            }
        }
    }

}

private struct SourceCardActionButton: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let isActive: Bool
    let activeTitle: UiMessageKey
    let inactiveTitle: UiMessageKey
    var activeIcon = "checkmark"
    var inactiveIcon = "plus"
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Label(
                UiMessages.string(isActive ? activeTitle : inactiveTitle, locale: nativeUiLocale),
                systemImage: isActive ? activeIcon : inactiveIcon
            )
            .font(Typography.caption)
        }
        .buttonStyle(.bordered)
        .tint(isActive ? .secondary : Colors.primary)
    }
}

private struct RssFeedSourceThumbnail: View {
    let source: RssFeedSource

    var body: some View {
        if let coverArtURL = source.podcastShow?.coverArtUrl {
            AsyncImageView(urlString: coverArtURL, contentMode: .fill)
                .frame(width: 56, height: 56)
                .clipShape(RoundedRectangle(cornerRadius: Spacing.xs))
        } else {
            ZStack {
                RoundedRectangle(cornerRadius: Spacing.xs)
                    .fill(Colors.primary.opacity(0.12))
                Image(systemName: source.cardFeedTypeSystemImage)
                    .font(.system(size: 22))
                    .foregroundStyle(Colors.primary)
            }
            .frame(width: 56, height: 56)
        }
    }
}
