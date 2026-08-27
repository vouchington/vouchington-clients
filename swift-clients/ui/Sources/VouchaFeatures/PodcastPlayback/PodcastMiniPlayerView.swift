import AVKit
import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

@MainActor
public struct PodcastMiniPlayerView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var controller: PodcastPlaybackController

    public init(controller: PodcastPlaybackController) {
        self.controller = controller
    }

    public var body: some View {
        if let item = controller.currentItem {
            content(for: item)
                .padding(.horizontal, Spacing.md)
                .padding(.vertical, Spacing.sm)
                .background(Colors.background)
                .overlay(alignment: .top) {
                    Divider()
                }
        }
    }

    private func content(for item: RssFeedItem) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            switch item.playbackKind {
            case .audio, .directVideo:
                header(for: item, showsPlaybackControl: true)
                videoPlayer
                progressControl
                chapterControls
                speedControls
            case .externalAudio, .embedOnlyVideo, .unavailable:
                header(for: item, showsPlaybackControl: false)
            }
            PodcastMiniPlayerMediaFallback(
                playbackKind: controller.currentPlaybackKind,
                externalAudioURL: controller.currentExternalURL,
                sourceURLString: item.externalURLString
            )
        }
    }

    private func header(for item: RssFeedItem, showsPlaybackControl: Bool) -> some View {
        HStack(alignment: .top, spacing: Spacing.sm) {
            artwork
            titleStack
            Spacer(minLength: 0)
            if showsPlaybackControl {
                Button { Task { await controller.togglePlayback(for: item) } } label: {
                    Image(systemName: controller.isPlaying ? "pause.fill" : "play.fill")
                }
                .buttonStyle(.borderedProminent)
            }
        }
    }

    @ViewBuilder
    private var artwork: some View {
        if let artworkURL = controller.currentArtworkURL {
            AsyncImageView(urlString: artworkURL, baseURL: controller.apiBaseURL, contentMode: .fill)
                .frame(width: 52, height: 52)
                .clipShape(RoundedRectangle(cornerRadius: Spacing.sm))
        }
    }

    private var titleStack: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(
                controller.currentTitle
                    ?? UiMessages.string(.nativeSwiftPodcastPlaybackNowPlaying, locale: nativeUiLocale)
            )
            .font(Typography.subheadline)
            .lineLimit(2)
            if let subtitle = controller.currentSubtitle {
                Text(subtitle)
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                    .lineLimit(1)
            }
        }
    }

    @ViewBuilder
    private var videoPlayer: some View {
        if controller.showsVideoPlayer {
            VideoPlayer(player: controller.player)
                .frame(minHeight: 180)
                .clipShape(RoundedRectangle(cornerRadius: Spacing.sm))
        }
    }

    private var progressControl: some View {
        VStack(spacing: Spacing.xs) {
            Slider(
                value: Binding(
                    get: { controller.currentTimeSeconds },
                    set: { controller.seek(to: $0) }
                ),
                in: 0 ... max(controller.durationSeconds, 1)
            )
            HStack {
                Text(timeLabel(controller.currentTimeSeconds))
                Spacer()
                Text(timeLabel(controller.durationSeconds))
            }
            .font(Typography.caption2)
            .foregroundStyle(Colors.secondaryLabel)
        }
    }

    @ViewBuilder
    private var speedControls: some View {
        if controller.showsSpeedControls {
            HStack(spacing: Spacing.xs) {
                ForEach(controller.availablePlaybackRates, id: \.self) { rate in
                    speedButton(rate)
                }
            }
        }
    }

    @ViewBuilder
    private func speedButton(_ rate: Double) -> some View {
        if controller.playbackRate == rate {
            Button(rateLabel(rate)) {
                controller.setPlaybackRate(rate)
            }
            .buttonStyle(.borderedProminent)
        } else {
            Button(rateLabel(rate)) {
                controller.setPlaybackRate(rate)
            }
            .buttonStyle(.bordered)
        }
    }

    private func timeLabel(_ seconds: Double) -> String {
        let total = max(0, Int(seconds.rounded()))
        let minutes = total / 60
        let remainingSeconds = total % 60
        return UiMessages.string(
            .nativeSwiftPodcastPlaybackDuration,
            parameters: [
                "minutes": UiMessages.number(minutes, locale: nativeUiLocale),
                "seconds": UiMessages.integer(
                    remainingSeconds,
                    minimumIntegerDigits: 2,
                    locale: nativeUiLocale
                )
            ],
            locale: nativeUiLocale
        )
    }

    private func rateLabel(_ rate: Double) -> String {
        UiMessages.string(
            .nativeSwiftPodcastPlaybackPlaybackRate,
            parameters: ["rate": UiMessages.number(rate, maximumFractionDigits: 2, locale: nativeUiLocale)],
            locale: nativeUiLocale
        )
    }
}
