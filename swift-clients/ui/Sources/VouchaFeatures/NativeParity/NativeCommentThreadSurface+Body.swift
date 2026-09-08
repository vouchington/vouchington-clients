import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

extension NativeCommentThreadSurface {
    var body: some View {
        @Bindable
        var viewModel = viewModel

        return ScrollView {
            VStack(alignment: .leading, spacing: Spacing.lg) {
                sortPicker

                if let rootPost = viewModel.rootPost {
                    CommentThreadPostSection(
                        title: rootPost.title.map(UiVerbatimText.verbatim)
                            ?? rootPost.slug.map(UiVerbatimText.verbatim)
                            ?? .message(.nativeSwiftPresentationValuesCommentThread),
                        post: rootPost,
                        embed: viewModel.postEmbedsByPostId[rootPost.id],
                        pathText: "/\(threadRootType(rootPost))/\(viewModel.rootPostId)",
                        voteChoice: viewModel.voteChoicesByPostId[rootPost.id],
                        isSignedIn: isSignedIn,
                        canCreateVote: canCreateVote && !viewModel.inFlightVotePostIds.contains(rootPost.id),
                        showSignIn: showSignIn,
                        hideDownCount: hideDownCount,
                        isRoot: true,
                        isCollapsed: false,
                        onToggleCollapse: {},
                        onVote: { handleVote(postId: rootPost.id, choice: $0) },
                        onReply: { startReply(post: rootPost, quote: false) },
                        onQuote: { startReply(post: rootPost, quote: true) },
                        onSave: { handleSave(postId: rootPost.id) },
                        onReport: canReport(post: rootPost) ? { startReport(post: rootPost) } : nil,
                        onEdit: rootPost.canEditContent == true ? { startEdit(post: rootPost) } : nil,
                        onDelete: rootPost.canDelete == true ? { startDelete(post: rootPost) } : nil,
                        onLock: rootPost.canLock == true ? { handleLock(post: rootPost) } : nil,
                        isSaved: viewModel.bookmarksByPostId[rootPost.id]?["save"] ?? false
                    )
                    if let client, canDistribute(rootPost) {
                        HStack {
                            Spacer()
                            FollowerDistributionActions(
                                client: client,
                                currentUserId: currentUserId,
                                target: .post(rootPost.id)
                            )
                        }
                    }

                    HnDiscussionsPanel(
                        urls: HnDiscussionURLCollector.extract(fromMarkdown: rootPost.markdown),
                        client: client
                    )

                    ancestorPaginationControl

                    if !viewModel.ancestorPosts.isEmpty {
                        VStack(alignment: .leading, spacing: Spacing.md) {
                            Text(UiMessages.string(.nativeSwiftCommentThreadAncestorChain, locale: nativeUiLocale))
                                .font(Typography.headline)
                            ForEach(viewModel.ancestorPosts, id: \.id) { post in
                                CommentThreadPostSection(
                                    title: authorName(for: post),
                                    post: post,
                                    embed: viewModel.postEmbedsByPostId[post.id],
                                    pathText: "/\(threadRootType(rootPost))/\(viewModel.rootPostId)/comment/\(post.id)",
                                    voteChoice: viewModel.voteChoicesByPostId[post.id],
                                    isSignedIn: isSignedIn,
                                    canCreateVote: canCreateVote && !viewModel.inFlightVotePostIds.contains(post.id),
                                    showSignIn: showSignIn,
                                    hideDownCount: hideDownCount,
                                    isRoot: false,
                                    isCollapsed: false,
                                    onToggleCollapse: {},
                                    onVote: { handleVote(postId: post.id, choice: $0) },
                                    onReply: { startReply(post: post, quote: false) },
                                    onQuote: { startReply(post: post, quote: true) },
                                    onSave: { handleSave(postId: post.id) },
                                    onReport: canReport(post: post) ? { startReport(post: post) } : nil,
                                    onEdit: post.canEditContent == true ? { startEdit(post: post) } : nil,
                                    onDelete: post.canDelete == true ? { startDelete(post: post) } : nil,
                                    onLock: post.canLock == true ? { handleLock(post: post) } : nil,
                                    isSaved: viewModel.bookmarksByPostId[post.id]?["save"] ?? false
                                )
                            }
                        }
                    }

                    VStack(alignment: .leading, spacing: Spacing.md) {
                        Text(UiMessages.string(.nativeSwiftCommentThreadComments, locale: nativeUiLocale))
                            .font(Typography.headline)
                        if viewModel.commentTree.isEmpty {
                            EmptyStateView(
                                icon: "bubble.left",
                                title: .message(.nativeSwiftEmptyStateNoComments),
                                message: .message(.nativeSwiftEmptyStateNoCommentsMessage)
                            )
                        } else {
                            ForEach(viewModel.commentTree) { node in
                                NativeCommentThreadNodeView(
                                    node: node,
                                    rootPostId: viewModel.rootPostId,
                                    rootPostType: threadRootType(rootPost),
                                    depth: 0,
                                    collapsedIds: viewModel.collapsedCommentIds,
                                    isSignedIn: isSignedIn,
                                    canCreateVote: canCreateVote,
                                    showSignIn: showSignIn,
                                    currentUserId: currentUserId,
                                    hideDownCount: hideDownCount,
                                    inFlightVotePostIds: viewModel.inFlightVotePostIds,
                                    bookmarksByPostId: viewModel.bookmarksByPostId,
                                    postEmbedsByPostId: viewModel.postEmbedsByPostId,
                                    voteChoiceByPostId: viewModel.voteChoicesByPostId,
                                    onToggleCollapse: viewModel.toggleCollapse(commentId:),
                                    onVote: { handleVote(postId: $0, choice: $1) },
                                    onReply: { startReply(postId: $0, quote: false) },
                                    onQuote: { startQuote(post: $0) },
                                    onSave: { handleSave(postId: $0) },
                                    onReport: { startReport(post: $0) },
                                    onEdit: { startEdit(post: $0) },
                                    onDelete: { startDelete(post: $0) },
                                    onLock: { handleLock(post: $0) }
                                )
                            }
                        }
                        descendantPaginationControl
                    }
                } else if viewModel.isLoading {
                    ProgressView()
                        .frame(maxWidth: .infinity, alignment: .center)
                } else if let message = loadErrorMessage {
                    EmptyStateView(
                        icon: "exclamationmark.triangle",
                        title: .message(.nativeSwiftEmptyStateUnableToLoadThread),
                        message: .verbatim(message)
                    )
                }
            }
            .padding(Spacing.md)
        }
        .sheet(item: $composer, content: composerSheet)
        .emailVerificationRecovery(
            client: client,
            gate: viewModel.emailVerificationGate
        )
        .task(id: loadTaskId) {
            await loadThread()
        }
        .onChange(of: currentUserId) { _, newValue in
            viewModel = NativeCommentThreadViewModel(
                client: client,
                rootPostId: viewModel.rootPostId,
                currentUserId: newValue
            )
        }
    }
}
