import VouchaModels

struct PendingHideDelivery {
    let row: RssFeedListRow
    var nextVisibleDeliveryId: String?
}

extension RSSFeedListViewModel {
    func bufferSkippedPendingHideDeliveries(
        page: RssFeedPage,
        visibleRows: [RssFeedListRow],
        skipped: [(index: Int, row: RssFeedListRow)]
    ) {
        let existingDeliveryIds = Set(pagination.items.map(\.deliveryId))
        if let nextPageDeliveryId = visibleRows.first(where: { !existingDeliveryIds.contains($0.deliveryId) })?
            .deliveryId {
            for itemId in Array(skippedPendingHideDeliveriesByItemId.keys) {
                skippedPendingHideDeliveriesByItemId[itemId] = skippedPendingHideDeliveriesByItemId[itemId]?
                    .map { entry in
                        var updated = entry
                        if updated.nextVisibleDeliveryId == nil {
                            updated.nextVisibleDeliveryId = nextPageDeliveryId
                        }
                        return updated
                    }
            }
        }
        let visibleDeliveryIds = Set(visibleRows.map(\.deliveryId))
        for entry in skipped {
            let next = page.results.dropFirst(entry.index + 1).first {
                visibleDeliveryIds.contains($0.id)
            }?.id
            skippedPendingHideDeliveriesByItemId[entry.row.item.id, default: []].append(
                PendingHideDelivery(row: entry.row, nextVisibleDeliveryId: next)
            )
        }
    }
}
