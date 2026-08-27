import SwiftUI
import VouchaDesignSystem
import VouchaModels

@MainActor
extension PodcastMiniPlayerView {
    var chapterControls: some View {
        Group {
            if !controller.chapters.isEmpty {
                ScrollView(.horizontal, showsIndicators: false) {
                    LazyHStack(spacing: Spacing.xs) {
                        ForEach(Array(controller.chapters.enumerated()), id: \.offset) { _, chapter in
                            Button {
                                controller.seek(to: chapter.startSeconds)
                            } label: {
                                Text(chapter.title)
                                    .font(Typography.caption)
                                    .lineLimit(1)
                            }
                            .buttonStyle(.bordered)
                        }
                    }
                }
            }
        }
    }
}
