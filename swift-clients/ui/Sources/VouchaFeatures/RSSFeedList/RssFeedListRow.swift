import VouchaModels

struct RssFeedListRow: Identifiable {
    let deliveryId: String
    let item: RssFeedItem
    let showsStory: Bool

    var id: String {
        deliveryId
    }
}
