import Foundation
import VouchaModels

/// One page of `/my/notifications`: the rows plus the hydrated facts their copy is rendered from.
struct NotificationsPage: Decodable {
    struct ResultItem: Decodable {
        let id: String
    }

    struct PageInfo: Decodable {
        let hasNextPage: Bool
        let endCursor: String?
    }

    let results: [ResultItem]
    let pageInfo: PageInfo
    let notifications: [String: VouchaNotification]
    let hydration: NotificationHydration

    private enum CodingKeys: String, CodingKey {
        case results, pageInfo, notifications
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        results = try container.decode([ResultItem].self, forKey: .results)
        pageInfo = try container.decode(PageInfo.self, forKey: .pageInfo)
        notifications = try container.decode([String: VouchaNotification].self, forKey: .notifications)
        hydration = try NotificationHydration(from: decoder)
    }
}
