import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommentThreadPlaceholderHeader: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let post: Post
    let isRoot: Bool
    let isCollapsed: Bool
    let directReplyCount: Int?
    let collapseIsLoading: Bool
    let onToggleCollapse: () -> Void

    var body: some View {
        HStack(spacing: Spacing.sm) {
            if !isRoot {
                Button(action: onToggleCollapse) {
                    Image(systemName: isCollapsed ? "chevron.right" : "chevron.down")
                }
                .buttonStyle(.plain)
                .disabled(collapseIsLoading)
            }
            Text(UiMessages.string(
                post.placeholderState == "unmoderated" ? .nativeSwiftModerationReportsPending
                    : .nativeSwiftPresentationValuesDeleted,
                locale: nativeUiLocale
            ))
            if isCollapsed, let directReplyCount {
                Text(UiMessages.number(directReplyCount, locale: nativeUiLocale))
                    .accessibilityIdentifier("comment-direct-reply-count")
            }
        }
        .accessibilityIdentifier("comment-placeholder")
    }

}
