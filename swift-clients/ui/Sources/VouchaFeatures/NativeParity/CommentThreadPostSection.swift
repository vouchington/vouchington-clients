import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommentThreadPostSection: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let title: UiVerbatimText
    let post: Post
    var imageConfig: AppConfig = .shared
    let embed: UrlEmbed?
    let pathText: String
    let voteChoice: ElectionVoteChoice?
    let isSignedIn: Bool
    let canCreateVote: Bool
    let showSignIn: () -> Void
    let hideDownCount: Bool
    let isRoot: Bool
    let isCollapsed: Bool
    let onToggleCollapse: () -> Void
    let onVote: (ElectionVoteChoice?) -> Void
    let onReply: () -> Void
    let onQuote: () -> Void
    let onSave: () -> Void
    let onReport: (() -> Void)?
    let onEdit: (() -> Void)?
    let onDelete: (() -> Void)?
    let onLock: (() -> Void)?
    let isSaved: Bool
    var directReplyCount: Int?
    var collapseIsLoading = false
    var currentUserId: String?
    var isStaffViewer = false
    var onViewAppeals: () -> Void = {}

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if post.isCommentPlaceholder {
                CommentThreadPlaceholderHeader(
                    post: post, isRoot: isRoot, isCollapsed: isCollapsed,
                    directReplyCount: directReplyCount, collapseIsLoading: collapseIsLoading,
                    onToggleCollapse: onToggleCollapse
                )
            } else {
                HStack(alignment: .top, spacing: Spacing.sm) {
                    if !isRoot {
                        Button(action: onToggleCollapse) {
                            Image(systemName: isCollapsed ? "chevron.right" : "chevron.down")
                        }
                        .buttonStyle(.plain)
                        .disabled(collapseIsLoading)
                    }
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        if isRoot {
                            if showsAuthorAvatar {
                                HStack(spacing: Spacing.xs) {
                                    PostAuthorAvatar(post: post, imageConfig: imageConfig)
                                    Text(UiMessages.string(authorName, locale: nativeUiLocale))
                                        .font(Typography.caption)
                                }
                            }
                            postTitle
                        } else {
                            HStack(spacing: Spacing.xs) {
                                PostAuthorAvatar(post: post, imageConfig: imageConfig)
                                postTitle
                                if isCollapsed, let directReplyCount {
                                    Text(UiMessages.number(directReplyCount, locale: nativeUiLocale))
                                        .accessibilityIdentifier("comment-direct-reply-count")
                                }
                            }
                        }
                        AccountTypeBadge(accountType: post.isAnonymous || post.deletedAt != nil ? nil : post.createdBy?
                            .accountType)
                        ProvenanceBadge(
                            provenance: post.provenance,
                            staffProvenance: post.staffProvenance,
                            isStaffViewer: isStaffViewer,
                            showClient: isRoot
                        )
                        Text(pathText)
                            .font(Typography.caption.monospaced())
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                    Spacer(minLength: 0)
                }

                if !isCollapsed {
                    postBody
                    if post.canShowAuthoredMedia(isStaffViewer: isStaffViewer),
                       let embed {
                        ProviderEmbedPreview(embed: embed)
                    }
                }
                NativeCommentThreadActionRow(
                    isSignedIn: isSignedIn,
                    canCreateVote: canCreateVote,
                    hideDownCount: hideDownCount,
                    post: post,
                    election: post.election,
                    voteChoice: voteChoice,
                    isSaved: isSaved,
                    onVote: onVote,
                    showSignIn: showSignIn,
                    onReply: onReply,
                    onQuote: onQuote,
                    canQuote: showsBodyContent,
                    onSave: onSave,
                    onReport: onReport,
                    onEdit: onEdit,
                    onDelete: onDelete,
                    onLock: onLock
                )
            }
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

}

private extension CommentThreadPostSection {
    var showsBodyContent: Bool {
        switch post.contentPresentation(viewerId: currentUserId, isStaffViewer: isStaffViewer) {
        case .content, .underReviewForStaff: true
        case .removedForAuthor, .unavailable: false
        }
    }

    var showsAuthorAvatar: Bool {
        post.deletedAt == nil && !post.isAnonymous && post.createdBy != nil
    }

    var authorName: UiVerbatimText {
        post.createdBy?.username.map(UiVerbatimText.verbatim)
            ?? post.createdById.map(UiVerbatimText.verbatim)
            ?? .message(.nativeSwiftPresentationValuesDeleted)
    }

    var postTitle: some View {
        Text(verbatim: UiMessages.string(title, locale: nativeUiLocale))
            .font(Typography.headline)
            .authoredContentLanguage(
                declared: isRoot && post.title != nil ? post.declaredLanguage : nil,
                detected: isRoot && post.title != nil ? post.linguaRsDetectedLanguage : nil
            )
    }

    @ViewBuilder
    var postBody: some View {
        switch post.contentPresentation(viewerId: currentUserId, isStaffViewer: isStaffViewer) {
        case .removedForAuthor:
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeAuthoredPostRemoved, locale: nativeUiLocale))
                if let reason = post.clearanceReason, !reason.isEmpty {
                    Text(verbatim: UiMessages.string(.protocolValue(reason), locale: nativeUiLocale))
                } else {
                    Text(UiMessages.string(.nativeAuthoredPostNoAdditionalReason, locale: nativeUiLocale))
                }
                Button(UiMessages.string(.nativeAuthoredPostViewAppeals, locale: nativeUiLocale), action: onViewAppeals)
            }
        case .unavailable:
            Text(UiMessages.string(.nativeAuthoredPostUnavailable, locale: nativeUiLocale))
        case .underReviewForStaff:
            Text(UiMessages.string(.nativeTaxonomyModerationInReview, locale: nativeUiLocale))
            postMarkdown
        case .content:
            postMarkdown
        }
    }

    @ViewBuilder
    var postMarkdown: some View {
        if let markdown = post.markdown, !markdown.isEmpty {
            NativeHtmlContent(
                html: post.html,
                fallback: markdown,
                declaredLanguage: post.declaredLanguage,
                detectedLanguage: post.linguaRsDetectedLanguage
            )
        }
    }
}
