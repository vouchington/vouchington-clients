import VouchaAPI
import VouchaModels

@MainActor
extension PodcastPlaybackController {
    func startChapterLoad(for item: RssFeedItem) {
        cancelChapterLoad()
        let loadToken = chapterLoadToken
        guard case .audio = item.playbackKind else { return }

        let apiClient = client
        chapterLoadTask = Task { @MainActor [weak self] in
            defer {
                if let self, loadToken == chapterLoadToken {
                    chapterLoadTask = nil
                }
            }
            guard !Task.isCancelled else { return }
            do {
                let response: PodcastChapterResponse = try await apiClient.send(
                    .podcastEpisodeChapters(rssFeedItemId: item.id)
                )
                guard !Task.isCancelled else { return }
                guard let self else { return }
                guard loadToken == chapterLoadToken else { return }
                guard currentItem?.id == item.id else { return }
                chapters = response.chapters.filter(\.isVisible)
            } catch {
                return
            }
        }
    }

    func cancelChapterLoad() {
        chapterLoadTask?.cancel()
        chapterLoadTask = nil
        chapterLoadToken &+= 1
        chapters = []
    }
}
