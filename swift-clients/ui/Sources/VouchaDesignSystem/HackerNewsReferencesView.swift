import SwiftUI
import VouchaLocalization
import VouchaModels

/// Stored references supplied with the entity; rendering never performs a provider search.
public struct HackerNewsReferencesView: View {
    @Environment(\.locale) private var locale
    public let references: HackerNewsReferences?
    public let expanded: Bool

    public init(references: HackerNewsReferences?, expanded: Bool = false) {
        self.references = references
        self.expanded = expanded
    }

    public var body: some View {
        if let references {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                if references.storyCount > 0 {
                    Text(UiMessages.string(
                        UiMessage(
                            .sharedHackerNewsSeenOnCount,
                            numberParameters: ["count": Double(references.storyCount)]
                        ),
                        locale: locale
                    ))
                }
                if references.commentCount > 0 {
                    Text(UiMessages.string(UiMessage(
                        references.storyCount > 0 ? .sharedHackerNewsMentionedCount : .sharedHackerNewsMentionedOnCount,
                        numberParameters: ["count": Double(references.commentCount)]
                    ), locale: locale))
                }
                if expanded { details(references) }
            }
            .font(Typography.caption)
        }
    }

    @ViewBuilder
    private func details(_ references: HackerNewsReferences) -> some View {
        if let stories = references.stories, !stories.isEmpty {
            Text(UiMessages.string(.sharedHackerNewsSeenOnHeading, locale: locale))
            ForEach(stories, id: \.hackerNewsItemId) { story in
                if let url = URL(string: "https://news.ycombinator.com/item?id=\(story.hackerNewsItemId)") {
                    Link(destination: url) {
                        Text(UiMessages.string(UiMessage(
                            .sharedHackerNewsStorySummary,
                            textParameters: [
                                "points": .verbatim(UiMessages.number(story.points, locale: locale)),
                                "comments": .verbatim(UiMessages.number(
                                    story.replyCount,
                                    locale: locale
                                ))
                            ]
                        ), locale: locale))
                    }
                    Text(story.postedAt, style: .date)
                }
            }
        }
        if let comments = references.comments, !comments.isEmpty {
            Text(UiMessages.string(.sharedHackerNewsMentionedOnHeading, locale: locale))
            ForEach(comments, id: \.hackerNewsItemId) { comment in
                if let url = URL(string: "https://news.ycombinator.com/item?id=\(comment.hackerNewsItemId)") {
                    Link(destination: url) {
                        Text(UiMessages.string(
                            UiMessage(
                                .sharedHackerNewsCommentOn,
                                dateParameters: ["date": .init(comment.postedAt)]
                            ),
                            locale: locale
                        ))
                    }
                }
            }
        }
    }
}
