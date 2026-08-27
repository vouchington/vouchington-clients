import Foundation

struct PodcastPositionWrite: Decodable {
    let positionSeconds: Double
    let completed: Bool

    enum CodingKeys: String, CodingKey {
        case positionSeconds = "position_seconds"
        case completed
    }
}
