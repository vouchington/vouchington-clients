@MainActor
extension PodcastPlaybackController {
    func normalizedPlayableMediaURLString(_ urlString: String) -> String {
        guard urlString.lowercased().hasPrefix("http://") else { return urlString }
        return "https://" + urlString.dropFirst("http://".count)
    }
}
