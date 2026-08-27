import SwiftUI
import VouchaCore
import VouchaModels

public enum CardVariant {
    case compact
    case expanded
}

public struct RssFeedItemCard: View {
    public let item: RssFeedItem
    public let variant: CardVariant
    public let election: RssFeedItemElection?
    public let myVote: ElectionVoteChoice?
    public let canCreateVote: Bool
    public let onVote: ((ElectionVoteChoice?) -> Void)?
    public let onSignedOutTap: (() -> Void)?
    public let isSaved: Bool
    public let isHidden: Bool
    public let showsDescription: Bool
    public let onToggleSaved: (() -> Void)?
    public let onToggleHidden: (() -> Void)?
    public let apiBaseURL: URL

    public init(
        item: RssFeedItem,
        variant: CardVariant = .compact,
        election: RssFeedItemElection? = nil,
        myVote: ElectionVoteChoice? = nil,
        canCreateVote: Bool = true,
        onVote: ((ElectionVoteChoice?) -> Void)? = nil,
        onSignedOutTap: (() -> Void)? = nil,
        isSaved: Bool = false,
        isHidden: Bool = false,
        showsDescription: Bool = true,
        onToggleSaved: (() -> Void)? = nil,
        onToggleHidden: (() -> Void)? = nil,
        apiBaseURL: URL = AppConfig.shared.baseURL
    ) {
        self.item = item
        self.variant = variant
        self.election = election ?? item.election
        self.myVote = myVote
        self.canCreateVote = canCreateVote
        self.onVote = onVote
        self.onSignedOutTap = onSignedOutTap
        self.isSaved = isSaved
        self.isHidden = isHidden
        self.showsDescription = showsDescription
        self.onToggleSaved = onToggleSaved
        self.onToggleHidden = onToggleHidden
        self.apiBaseURL = apiBaseURL
    }

    public var body: some View {
        switch variant {
        case .compact:
            compactBody
        case .expanded:
            expandedBody
        }
    }

    private var compactBody: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            if let thumbnailURL = item.displayThumbnailURLString {
                AsyncImageView(urlString: thumbnailURL, baseURL: apiBaseURL, contentMode: .fill)
                    .frame(width: 72, height: 72)
                    .clipShape(RoundedRectangle(cornerRadius: Spacing.sm))
            }
            VStack(alignment: .leading, spacing: Spacing.xs) {
                if let title = item.title {
                    Text(title)
                        .font(Typography.subheadline)
                        .lineLimit(2)
                }
                if let creator = item.creator {
                    Text(creator)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                if let publishedAt = item.publishedAt {
                    Text(publishedAt, style: .relative)
                        .font(Typography.caption2)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                cardActions
            }
            Spacer(minLength: 0)
        }
        .padding(.vertical, Spacing.sm)
    }

    private var expandedBody: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if let thumbnailURL = item.displayThumbnailURLString {
                AsyncImageView(urlString: thumbnailURL, baseURL: apiBaseURL, contentMode: .fill)
                    .frame(maxWidth: .infinity)
                    .frame(height: 180)
                    .clipShape(RoundedRectangle(cornerRadius: Spacing.md))
            }
            VStack(alignment: .leading, spacing: Spacing.xs) {
                if let title = item.title {
                    Text(title)
                        .font(Typography.headline)
                        .lineLimit(3)
                }
                if showsDescription, let description = item.description {
                    Text(description)
                        .font(Typography.body)
                        .foregroundStyle(Colors.secondaryLabel)
                        .lineLimit(4)
                }
                HStack {
                    if let creator = item.creator {
                        Text(creator)
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                    Spacer()
                    if let publishedAt = item.publishedAt {
                        Text(publishedAt, style: .relative)
                            .font(Typography.caption2)
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                }
                cardActions
            }
        }
        .padding(.vertical, Spacing.sm)
    }

    private var cardActions: some View {
        HStack(spacing: Spacing.md) {
            voteControls
            RelationActionButtons(
                isSaved: isSaved,
                isHidden: isHidden,
                onToggleSaved: onToggleSaved,
                onToggleHidden: onToggleHidden
            )
        }
    }

    @ViewBuilder
    private var voteControls: some View {
        if let election {
            VoteControls(
                election: election,
                myVote: myVote,
                canCreateVote: canCreateVote && (onVote != nil || onSignedOutTap != nil),
                onVote: onVote,
                onSignedOutTap: onSignedOutTap
            )
        }
    }
}
