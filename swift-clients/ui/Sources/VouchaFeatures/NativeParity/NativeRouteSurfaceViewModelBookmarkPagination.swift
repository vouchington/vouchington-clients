import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization

extension NativeRouteSurfaceViewModel {
    func loadInitialBookmarkPage(collection: NativeBookmarkCollection, client: APIClient) async throws {
        bookmarkPagination.reset()
        bookmarkEmbedsByEntityId = [:]
        guard let request = bookmarkPagination.beginInitialPageIfNeeded() else { return }
        do {
            let page = try await loadBookmarkCollectionPage(
                collection,
                client: client,
                after: request.cursor,
                rankOffset: 0,
                excluding: []
            )
            guard bookmarkPagination.complete(
                request,
                items: page.rows,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return }
            bookmarkEmbedsByEntityId.merge(page.embedsByEntityId) { _, new in new }
            bookmarkRows = bookmarkPagination.items
        } catch let error as VouchaError {
            _ = bookmarkPagination.fail(request, error: error)
            throw error
        } catch {
            let error = VouchaError.api(statusCode: 0, preconditionCode: nil)
            _ = bookmarkPagination.fail(request, error: error)
            throw error
        }
    }

    func loadMoreBookmarkRows() async {
        guard inFlightBookmarkRowIds.isEmpty,
              let client, let collection = currentBookmarkCollection,
              let request = bookmarkPagination.beginNextPage()
        else { return }
        do {
            let page = try await loadBookmarkCollectionPage(
                collection,
                client: client,
                after: request.cursor,
                rankOffset: (bookmarkPagination.items.map(\.rank).max() ?? -1) + 1,
                excluding: Set(bookmarkPagination.items.map(\.entityId))
            )
            guard bookmarkPagination.complete(
                request,
                items: page.rows,
                endCursor: page.endCursor,
                hasNextPage: page.hasMore
            ) else { return }
            bookmarkEmbedsByEntityId.merge(page.embedsByEntityId) { _, new in new }
            bookmarkRows = bookmarkPagination.items
        } catch let error as VouchaError {
            _ = bookmarkPagination.fail(request, error: error)
        } catch {
            _ = bookmarkPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
        }
    }

}
