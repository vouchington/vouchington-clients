/// The server-side response for `GET /api/v1/podcast-episodes/:id/playback-position`.
///
/// `playbackPosition` is `nil` when no position has been saved yet; start from 0.
/// If `completedAt` is set, the episode was played to completion — restart from 0.
///
/// Snake-case JSON keys are decoded automatically via the client's
/// `keyDecodingStrategy = .convertFromSnakeCase`.
public struct PodcastPlaybackPositionResponse: Codable, Sendable {
    /// Wrapped position; nil means no saved position exists.
    public let playbackPosition: PodcastPlaybackPosition?
}

public struct PodcastPlaybackPosition: Codable, Sendable {
    /// Current playback offset in seconds.
    public let positionSeconds: Double
    /// ISO 8601 timestamp when the episode was marked complete, or nil.
    public let completedAt: String?

    /// Whether the episode has been played to completion.
    public var isCompleted: Bool {
        completedAt != nil
    }
}
