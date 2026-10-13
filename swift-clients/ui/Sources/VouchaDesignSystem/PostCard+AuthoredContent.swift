import Foundation
import SwiftUI
import VouchaLocalization
import VouchaModels

extension PostCard {
    var contentPresentation: PostContentPresentation {
        post.contentPresentation(viewerId: currentUserId, isStaffViewer: isStaffViewer)
    }

    @ViewBuilder
    func authoredBody(lineLimit: Int) -> some View {
        switch contentPresentation {
        case .removedForAuthor:
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeAuthoredPostRemoved, locale: nativeUiLocale))
                if let reason = post.clearanceReason, !reason.isEmpty {
                    Text(verbatim: UiMessages.string(.protocolValue(reason), locale: nativeUiLocale))
                } else {
                    Text(UiMessages.string(.nativeAuthoredPostNoAdditionalReason, locale: nativeUiLocale))
                }
                if let onViewAppeals {
                    Button(
                        UiMessages.string(.nativeAuthoredPostViewAppeals, locale: nativeUiLocale),
                        action: onViewAppeals
                    )
                }
            }
        case .unavailable:
            Text(UiMessages.string(.nativeAuthoredPostUnavailable, locale: nativeUiLocale))
        case .underReviewForStaff:
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeTaxonomyModerationInReview, locale: nativeUiLocale))
                authoredMarkdownContent(lineLimit: lineLimit)
            }
        case .content:
            authoredMarkdownContent(lineLimit: lineLimit)
        }
    }

    @ViewBuilder
    func authoredMarkdownContent(lineLimit: Int) -> some View {
        if let markdown = authoredMarkdown {
            NativeHtmlContent(
                html: post.html,
                fallback: markdown,
                lineLimit: lineLimit,
                font: Typography.body,
                foregroundStyle: Colors.secondaryLabel,
                declaredLanguage: post.declaredLanguage,
                detectedLanguage: post.linguaRsDetectedLanguage
            )
        }
    }

    var authoredTitle: String? {
        normalizedAuthoredText(post.title)
    }

    var authoredMarkdown: String? {
        normalizedAuthoredText(post.markdown)
    }

    func normalizedAuthoredText(_ value: String?) -> String? {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }
        return value
    }
}
