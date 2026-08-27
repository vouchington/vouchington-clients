import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeCommentThreadActionRow: View {
    @Environment(\.locale)
    var nativeUiLocale
    let isSignedIn: Bool
    let canCreateVote: Bool
    let hideDownCount: Bool
    let post: Post
    let election: PostElection?
    let voteChoice: ElectionVoteChoice?
    let isSaved: Bool
    let onVote: (ElectionVoteChoice?) -> Void
    let showSignIn: () -> Void
    let onReply: () -> Void
    let onQuote: () -> Void
    let onSave: () -> Void
    let onReport: (() -> Void)?
    let onEdit: (() -> Void)?
    let onDelete: (() -> Void)?
    let onLock: (() -> Void)?

    var body: some View {
        HStack(spacing: Spacing.sm) {
            VoteControls(
                election: election,
                myVote: voteChoice,
                policy: post.postType == .topicRecommendation ? .recommendation : .sentiment,
                canCreateVote: canCreateVote,
                onVote: onVote,
                onSignedOutTap: isSignedIn ? nil : showSignIn,
                hideDownCount: hideDownCount
            )

            Button(action: onReply) { Label(
                UiMessages.string(.nativeSwiftCommentThreadActionRowReply, locale: nativeUiLocale),
                systemImage: "arrowshape.turn.up.left"
            ) }
            .buttonStyle(.plain)
            Button(action: onQuote) { Label(
                UiMessages.string(.nativeSwiftCommentThreadActionRowQuote, locale: nativeUiLocale),
                systemImage: "quote.opening"
            ) }
            .buttonStyle(.plain)

            Button(action: onSave) {
                Label(
                    UiMessages.string(
                        isSaved ? .nativeSwiftCommonSaved : .nativeSwiftCommonSave,
                        locale: nativeUiLocale
                    ),
                    systemImage: "bookmark"
                )
            }
            .buttonStyle(.plain)
            .disabled(!isSignedIn)

            if let onReport {
                Button(action: onReport) { Label(
                    UiMessages.string(.nativeSwiftCommentThreadActionRowReport, locale: nativeUiLocale),
                    systemImage: "flag"
                ) }
                .buttonStyle(.plain)
            }

            if let onEdit {
                Button(action: onEdit) { Label(
                    UiMessages.string(.nativeSwiftCommonEdit, locale: nativeUiLocale),
                    systemImage: "pencil"
                ) }
                .buttonStyle(.plain)
            }
            if let onDelete {
                Button(role: .destructive, action: onDelete) {
                    Label(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), systemImage: "trash")
                }
                .buttonStyle(.plain)
            }
            if let onLock {
                Button(action: onLock) {
                    Label(
                        UiMessages.string(
                            post.lockedAt == nil ? .nativeSwiftCommonLock : .nativeSwiftCommonUnlock,
                            locale: nativeUiLocale
                        ),
                        systemImage: "lock"
                    )
                }
                .buttonStyle(.plain)
            }

            Spacer(minLength: 0)
        }
        .font(Typography.caption)
        .foregroundStyle(Colors.secondaryLabel)
        .fixedSize(horizontal: false, vertical: true)
    }
}
