import VouchaModels

struct RssFeedListRow: Identifiable, Sendable {
    let deliveryId: String
    let item: RssFeedItem
    let showsStory: Bool

    var id: String { deliveryId }
}
