import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeCommentThreadNodeView: View {
    let node: NativeCommentThreadNode
    let rootPostId: String
    let rootPostType: String
    let depth: Int
    let collapsedIds: Set<String>
    let isSignedIn: Bool
    let canCreateVote: Bool
    let showSignIn: () -> Void
    let currentUserId: String?
    let hideDownCount: Bool
    let inFlightVotePostIds: Set<String>
    let bookmarksByPostId: [String: [String: Bool]]
    let postEmbedsByPostId: [String: UrlEmbed]
    let voteChoiceByPostId: [String: ElectionVoteChoice]
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
                isSaved: bookmarksByPostId[post.id]?["save"] ?? false
            )

            if !isCollapsed {
                ForEach(node.children) { child in
                    NativeCommentThreadNodeView(
                        node: child,
                        rootPostId: rootPostId,
                        rootPostType: rootPostType,
                        depth: depth + 1,
                        collapsedIds: collapsedIds,
                        isSignedIn: isSignedIn,
                        canCreateVote: canCreateVote,
                        showSignIn: showSignIn,
                        currentUserId: currentUserId,
                        hideDownCount: hideDownCount,
                        inFlightVotePostIds: inFlightVotePostIds,
                        bookmarksByPostId: bookmarksByPostId,
                        postEmbedsByPostId: postEmbedsByPostId,
                        voteChoiceByPostId: voteChoiceByPostId,
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
