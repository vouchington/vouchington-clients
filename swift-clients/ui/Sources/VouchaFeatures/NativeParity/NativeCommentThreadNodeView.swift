import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeCommentThreadNodeView: View {
    let node: NativeCommentThreadNode
    var imageConfig: AppConfig = .shared
    let rootPostId: String
    let rootPostType: String
    let depth: Int
    let collapsedIds: Set<String>
    let isSignedIn: Bool
    let canCreateVote: Bool
    let showSignIn: () -> Void
    let currentUserId: String?
    var isStaffViewer = false
    var onNavigate: (String) -> Void = { _ in }
    let hideDownCount: Bool
    let inFlightVotePostIds: Set<String>
    let bookmarksByPostId: [String: [String: Bool]]
    let postEmbedsByPostId: [String: UrlEmbed]
    let voteChoiceByPostId: [String: ElectionVoteChoice]
    var postMetricsById: [String: PostMetrics] = [:]
    var inFlightCollapseIds: Set<String> = []
    var subtreePagination: [String: CursorPaginationState<Post>] = [:]
    var onLoadHiddenDescendants: (String) async -> Void = { _ in }
    let onToggleCollapse: (String) -> Void
    let onVote: (String, ElectionVoteChoice?) -> Void
    let onReply: (String) -> Void
    let onQuote: (Post) -> Void
    let onSave: (String) -> Void
    let onReport: (Post) -> Void
    let onEdit: (Post) -> Void
    let onDelete: (Post) -> Void
    let onLock: (Post) -> Void

    var body: some View {
        let post = node.post
        let isCollapsed = collapsedIds.contains(post.id)
        VStack(alignment: .leading, spacing: Spacing.sm) {
            CommentThreadPostSection(
                title: (post.createdBy?.username).map(UiVerbatimText.verbatim)
                    ?? post.createdById.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftPresentationValuesDeleted),
                post: post,
                imageConfig: imageConfig,
                embed: postEmbedsByPostId[post.id],
                pathText: "/\(rootPostType)/\(rootPostId)/comment/\(post.id)",
                voteChoice: voteChoiceByPostId[post.id],
                isSignedIn: isSignedIn,
                canCreateVote: canCreateVote && !inFlightVotePostIds.contains(post.id),
                showSignIn: showSignIn,
                hideDownCount: hideDownCount,
                isRoot: false,
                isCollapsed: isCollapsed,
                onToggleCollapse: { onToggleCollapse(post.id) },
                onVote: { onVote(post.id, $0) },
                onReply: { onReply(post.id) },
                onQuote: { onQuote(post) },
                onSave: { onSave(post.id) },
                onReport: canReport(post: post) ? { onReport(post) } : nil,
                onEdit: canEdit(post: post) ? { onEdit(post) } : nil,
                onDelete: canDelete(post: post) ? { onDelete(post) } : nil,
                onLock: canLock(post: post) ? { onLock(post) } : nil,
                isSaved: bookmarksByPostId[post.id]?["save"] ?? false,
                directReplyCount: postMetricsById[post.id]?.count.children ?? post.metrics?.count.children,
                collapseIsLoading: inFlightCollapseIds.contains(post.id),
                currentUserId: currentUserId,
                isStaffViewer: isStaffViewer,
                onViewAppeals: { onNavigate("/my/appeals") }
            )

            if !isCollapsed {
                if let pagination = subtreePagination[post.id] {
                    HybridPaginationControl(
                        hasMore: !pagination.hasLoadedPage || pagination.hasMore,
                        isLoading: pagination.isLoading,
                        hasError: pagination.lastError != nil,
                        accessibilityIdentifier: "comment-hidden-descendants-\(post.id)"
                    ) { await onLoadHiddenDescendants(post.id) }
                }
                ForEach(node.children) { child in
                    NativeCommentThreadNodeView(
                        node: child,
                        imageConfig: imageConfig,
                        rootPostId: rootPostId,
                        rootPostType: rootPostType,
                        depth: depth + 1,
                        collapsedIds: collapsedIds,
                        isSignedIn: isSignedIn,
                        canCreateVote: canCreateVote,
                        showSignIn: showSignIn,
                        currentUserId: currentUserId,
                        isStaffViewer: isStaffViewer,
                        onNavigate: onNavigate,
                        hideDownCount: hideDownCount,
                        inFlightVotePostIds: inFlightVotePostIds,
                        bookmarksByPostId: bookmarksByPostId,
                        postEmbedsByPostId: postEmbedsByPostId,
                        voteChoiceByPostId: voteChoiceByPostId,
                        postMetricsById: postMetricsById,
                        inFlightCollapseIds: inFlightCollapseIds,
                        subtreePagination: subtreePagination,
                        onLoadHiddenDescendants: onLoadHiddenDescendants,
                        onToggleCollapse: onToggleCollapse,
                        onVote: onVote,
                        onReply: onReply,
                        onQuote: onQuote,
                        onSave: onSave,
                        onReport: onReport,
                        onEdit: onEdit,
                        onDelete: onDelete,
                        onLock: onLock
                    )
                }
            }
        }
        .padding(.leading, depth > 0 ? Spacing.md : 0)
    }

    private func canEdit(post: Post) -> Bool {
        isSignedIn && currentUserId != nil && post.canEditContent == true
    }

    private func canDelete(post: Post) -> Bool {
        isSignedIn && currentUserId != nil && post.canDelete == true
    }

    private func canLock(post: Post) -> Bool {
        isSignedIn && currentUserId != nil && post.canLock == true
    }

    private func canReport(post: Post) -> Bool {
        isSignedIn && currentUserId != nil && post.createdById != currentUserId && post.deletedAt == nil
    }
}
