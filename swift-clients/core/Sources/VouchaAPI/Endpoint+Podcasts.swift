private struct UpdatePodcastPlaybackPositionBody: Encodable {
    let positionSeconds: Double
    let completed: Bool

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(positionSeconds, forKey: .positionSeconds)
        try container.encode(completed, forKey: .completed)
    }

    private enum CodingKeys: String, CodingKey {
        case positionSeconds = "position_seconds"
        case completed
    }
}

public extension Endpoint {
    /// Fetch the chapter markers for a podcast episode.
    static func podcastEpisodeChapters(rssFeedItemId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/podcast-episodes/\(Endpoint.pathSegment(rssFeedItemId))/chapters")
    }

    /// Report the current playback position for a podcast episode.
    /// Called on a ~10–15s throttle while playing; flush on pause / ended.
    static func updatePodcastPlaybackPosition(
        rssFeedItemId: String,
        positionSeconds: Double,
        completed: Bool
    ) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/podcast-episodes/\(Endpoint.pathSegment(rssFeedItemId))/playback-position",
            body: UpdatePodcastPlaybackPositionBody(positionSeconds: positionSeconds, completed: completed)
        )
    }

    /// Fetch the saved playback position for a podcast episode.
    /// Called once when an episode is loaded into the player to resume playback.
    static func podcastPlaybackPosition(rssFeedItemId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/podcast-episodes/\(Endpoint.pathSegment(rssFeedItemId))/playback-position")
    }
}
