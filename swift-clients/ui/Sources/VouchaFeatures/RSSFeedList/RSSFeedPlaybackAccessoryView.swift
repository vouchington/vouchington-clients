import Foundation
import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

@MainActor
struct RSSFeedPlaybackAccessoryView: View {
    @Environment(\.locale)
    var nativeUiLocale
    let item: RssFeedItem
    let isCurrentItem: Bool
    let isPlaying: Bool
    let onPlayPauseTap: () -> Void
    let showsSourceLink: Bool

    init(
        item: RssFeedItem,
        isCurrentItem: Bool,
        isPlaying: Bool,
        onPlayPauseTap: @escaping () -> Void,
        showsSourceLink: Bool = true
    ) {
        self.item = item
        self.isCurrentItem = isCurrentItem
        self.isPlaying = isPlaying
        self.onPlayPauseTap = onPlayPauseTap
        self.showsSourceLink = showsSourceLink
    }

    var body: some View {
        switch item.playbackKind {
        case .audio:
            Button(action: onPlayPauseTap) {
                Label(
                    UiMessages.string(
                        isCurrentItem && isPlaying ? .nativeSwiftCommonPause : .nativeSwiftCommonPlay,
                        locale: nativeUiLocale
                    ),
                    systemImage: isCurrentItem && isPlaying ? "pause.fill" : "play.fill"
                )
            }
            .buttonStyle(.bordered)
        case .directVideo:
            Button(action: onPlayPauseTap) {
                Label(
                    UiMessages.string(
                        isCurrentItem && isPlaying ? .nativeSwiftCommonPauseVideo : .nativeSwiftCommonPlayVideo,
                        locale: nativeUiLocale
                    ),
                    systemImage: isCurrentItem && isPlaying ? "pause.fill" : "play.fill"
                )
            }
            .buttonStyle(.bordered)
        case let .externalAudio(urlString):
            if let urlString, let url = URL(string: urlString) {
                Link(destination: url) {
                    Label(
                        UiMessages.string(.nativeSwiftRssFeedListOpenEpisode, locale: nativeUiLocale),
                        systemImage: "arrow.up.right.square"
                    )
                }
            } else {
                Text(UiMessages.string(.nativeSwiftRssFeedListEpisodeUnavailable, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        case .embedOnlyVideo:
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftRssFeedPlaybackVideoUnavailable, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                if showsSourceLink,
                   let sourceURLString = item.externalURLString,
                   let sourceURL = URL(string: sourceURLString) {
                    Link(destination: sourceURL) {
                        Label(
                            UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: nativeUiLocale),
                            systemImage: "arrow.up.right.square"
                        )
                    }
                    .font(Typography.caption)
                }
            }
        case .unavailable:
            EmptyView()
        }
    }
}
