import SwiftUI
import VouchaLocalization
import VouchaModels

/// Displays a post summary in compact or expanded layout with optional voting controls.
public struct PostCard: View {
    @Environment(\.locale)
    var nativeUiLocale
    public let post: Post
    public let variant: CardVariant
    public let myVote: ElectionVoteChoice?
    public let canCreateVote: Bool
    public let onVote: ((ElectionVoteChoice?) -> Void)?
    public let onSignedOutTap: (() -> Void)?
    public let hideDownCount: Bool
    public let isSaved: Bool
    public let isHidden: Bool
    public let onToggleSaved: (() -> Void)?
    public let onToggleHidden: (() -> Void)?
    public let currentUserId: String?
    public let isStaffViewer: Bool
    public let onViewAppeals: (() -> Void)?

    public init(
        post: Post,
        variant: CardVariant = .compact,
        myVote: ElectionVoteChoice? = nil,
        canCreateVote: Bool = true,
        onVote: ((ElectionVoteChoice?) -> Void)? = nil,
        onSignedOutTap: (() -> Void)? = nil,
        hideDownCount: Bool = false,
        isSaved: Bool = false,
        isHidden: Bool = false,
        onToggleSaved: (() -> Void)? = nil,
        onToggleHidden: (() -> Void)? = nil,
        currentUserId: String? = nil,
        isStaffViewer: Bool = false,
        onViewAppeals: (() -> Void)? = nil
    ) {
        self.post = post
        self.variant = variant
        self.myVote = myVote
        self.canCreateVote = canCreateVote
        self.onVote = onVote
        self.onSignedOutTap = onSignedOutTap
        self.hideDownCount = hideDownCount
        self.isSaved = isSaved
        self.isHidden = isHidden
        self.onToggleSaved = onToggleSaved
        self.onToggleHidden = onToggleHidden
        self.currentUserId = currentUserId
        self.isStaffViewer = isStaffViewer
        self.onViewAppeals = onViewAppeals
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
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(alignment: .top, spacing: Spacing.sm) {
                postTypeBadge
                AccountTypeBadge(accountType: post.isAnonymous || post.deletedAt != nil ? nil : post.createdBy?
                    .accountType)
                Spacer(minLength: 0)
            }
            if let title = authoredTitle {
                Text(title)
                    .font(Typography.subheadline)
                    .lineLimit(2)
                    .authoredContentLanguage(declared: post.declaredLanguage, detected: post.linguaRsDetectedLanguage)
                if contentPresentation != .content { authoredBody(lineLimit: 2) }
            } else {
                authoredBody(lineLimit: 2)
            }
            postFooter
        }
        .padding(.vertical, Spacing.sm)
    }

    private var expandedBody: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            HStack {
                postTypeBadge
                AccountTypeBadge(accountType: post.isAnonymous || post.deletedAt != nil ? nil : post.createdBy?
                    .accountType)
                Spacer()
            }
            if let title = authoredTitle {
                Text(title)
                    .font(Typography.headline)
                    .authoredContentLanguage(declared: post.declaredLanguage, detected: post.linguaRsDetectedLanguage)
                    .lineLimit(4)
            }
            authoredBody(lineLimit: 6)
            postFooter
        }
        .padding(.vertical, Spacing.sm)
    }
}

private extension PostCard {
    private var postTypeBadge: some View {
        Text(UiMessages.string(postTypeLabel, locale: nativeUiLocale))
            .font(Typography.caption2)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, 2)
            .background(Colors.primary.opacity(0.1))
            .foregroundStyle(Colors.primary)
            .clipShape(Capsule())
    }

    private var postTypeLabel: UiMessageKey {
        switch post.postType {
        case .discussion: .nativeSwiftDesignSystemDiscussion
        case .review: .nativeSwiftDesignSystemReview
        case .dataPoint: .nativeSwiftDesignSystemData
        case .comment: .nativeSwiftDesignSystemComment
        case .article: .nativeSwiftDesignSystemArticle
        case .link: .nativeSwiftDesignSystemLink
        case .blogPost: .nativeSwiftDesignSystemBlog
        case .story: .nativeSwiftDesignSystemStory
        case .topicRecommendation: .nativeSwiftDesignSystemRecommendation
        }
    }

    private var postFooter: some View {
        HStack(spacing: Spacing.md) {
            ProvenanceBadge(
                provenance: post.provenance,
                staffProvenance: post.staffProvenance,
                isStaffViewer: isStaffViewer,
                showClient: variant == .expanded
            )
            voteControls
            relationControls
            commentCount
            Text(post.createdAt, style: .relative)
                .font(Typography.caption2)
                .foregroundStyle(Colors.secondaryLabel)
        }
    }

    @ViewBuilder
    private var voteControls: some View {
        if let election = post.election {
            VoteControls(
                election: election,
                myVote: myVote,
                policy: post.postType == .topicRecommendation ? .recommendation : .sentiment,
                canCreateVote: canCreateVote && (onVote != nil || onSignedOutTap != nil),
                onVote: onVote,
                onSignedOutTap: onSignedOutTap,
                hideDownCount: hideDownCount
            )
        }
    }

    private var relationControls: some View {
        RelationActionButtons(
            isSaved: isSaved,
            isHidden: isHidden,
            onToggleSaved: onToggleSaved,
            onToggleHidden: onToggleHidden
        )
    }

    @ViewBuilder
    private var commentCount: some View {
        if let metrics = post.metrics {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "bubble.left")
                    .font(.system(size: 11))
                Text(verbatim: UiMessages.number(metrics.count.descendants, locale: nativeUiLocale))
                    .font(Typography.caption.monospacedDigit())
            }
            .foregroundStyle(Colors.secondaryLabel)
        }
    }
}
