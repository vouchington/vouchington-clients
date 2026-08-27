import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct HnDiscussionsPanel: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    private var loader: HnDiscussionsLoader

    init(
        urls: [String],
        client: APIClient? = nil,
        searchClient: HnDiscussionsClient = HnDiscussionsClient()
    ) {
        self.init(loader: HnDiscussionsLoader(urls: urls, client: client, searchClient: searchClient))
    }

    init(loader: HnDiscussionsLoader) {
        _loader = State(initialValue: loader)
    }

    var body: some View {
        Group {
            if !loader.threads.isEmpty {
                VStack(alignment: .leading, spacing: Spacing.sm) {
                    Text(
                        UiMessages.string(
                            .extractedAsidesHnDiscussionsAsideHackerNews619f304a,
                            locale: nativeUiLocale
                        )
                    )
                    .font(Typography.headline)
                    ForEach(loader.threads, id: \.objectID) { thread in
                        VStack(alignment: .leading, spacing: Spacing.xs) {
                            Link(destination: thread.itemURL) {
                                Text(verbatim: thread.title)
                                    .font(Typography.subheadline)
                            }
                            Text(verbatim: metadata(for: thread))
                                .font(Typography.caption)
                                .foregroundStyle(Colors.secondaryLabel)
                        }
                        .accessibilityIdentifier("hn-discussions-thread")
                    }
                }
                .accessibilityIdentifier("hn-discussions-aside")
            }
        }
        .task(id: loader.taskID) { await loader.load() }
    }

    private func metadata(for thread: HnDiscussionThread) -> String {
        let points = UiMessages.string(
            .extractedAsidesHnDiscussionsAsidePointsPoints29c78cd8,
            parameters: ["points": "\(thread.score)"],
            locale: nativeUiLocale
        )
        let comments = UiMessages.string(
            .extractedAsidesHnDiscussionsAsideCommentsComments29834540,
            parameters: ["comments": "\(thread.commentCount)"],
            locale: nativeUiLocale
        )
        return "\(points) · \(comments)"
    }
}
