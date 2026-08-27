import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct PodcastMiniPlayerMediaFallback: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let playbackKind: RssFeedItemPlaybackKind?
    let externalAudioURL: URL?
    let sourceURLString: String?

    var body: some View {
        switch playbackKind {
        case .embedOnlyVideo:
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftRssFeedPlaybackVideoUnavailable, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                if let sourceURLString, let sourceURL = URL(string: sourceURLString) {
                    Link(destination: sourceURL) {
                        Label(
                            UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: nativeUiLocale),
                            systemImage: "arrow.up.right.square"
                        )
                    }
                    .font(Typography.caption)
                }
            }
        case .externalAudio:
            if let externalAudioURL {
                Link(destination: externalAudioURL) {
                    Label(
                        UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: nativeUiLocale),
                        systemImage: "arrow.up.right.square"
                    )
                }
                .font(Typography.caption)
            }
        case .audio, .directVideo, .unavailable, nil:
            EmptyView()
        }
    }
}
