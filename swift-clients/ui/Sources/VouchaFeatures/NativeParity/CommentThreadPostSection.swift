import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommentThreadPostSection: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let title: UiVerbatimText
    let post: Post
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

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack(alignment: .top, spacing: Spacing.sm) {
                if !isRoot {
                    Button(action: onToggleCollapse) {
                        Image(systemName: isCollapsed ? "chevron.right" : "chevron.down")
                    }
                    .buttonStyle(.plain)
                }
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(verbatim: UiMessages.string(title, locale: nativeUiLocale))
                        .font(Typography.headline)
                        .authoredContentLanguage(
                            declared: isRoot && post.title != nil ? post.declaredLanguage : nil,
                            detected: isRoot && post.title != nil ? post.linguaRsDetectedLanguage : nil
                        )
                    AccountTypeBadge(accountType: post.isAnonymous || post.deletedAt != nil ? nil : post.createdBy?
                        .accountType)
                    Text(pathText)
                        .font(Typography.caption.monospaced())
                        .foregroundStyle(Colors.secondaryLabel)
                }
                Spacer(minLength: 0)
            }

            if let markdown = post.markdown, !markdown.isEmpty {
                NativeHtmlContent(
                    html: post.html,
                    fallback: markdown,
                    declaredLanguage: post.declaredLanguage,
                    detectedLanguage: post.linguaRsDetectedLanguage
                )
            }
            if let embed {
                ProviderEmbedPreview(embed: embed)
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
                onSave: onSave,
                onReport: onReport,
                onEdit: onEdit,
                onDelete: onDelete,
                onLock: onLock
            )
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }
}
