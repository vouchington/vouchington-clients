import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class HackerNewsProfilePaginationTests: NativeRouteSurfaceViewModelTestCase {
    private let postId = "profile-review-1"
    private let path = "/api/v1/posts"

    private func page(stories: Int, hasNext: Bool) throws -> Data {
        var body = try XCTUnwrap(JSONSerialization.jsonObject(with:
            ApiFixtureLoader.data("native.posts.hacker-news.filtered")) as? [String: Any])
        body["post_hacker_news_references"] = [postId: ["story_count": stories, "comment_count": 2]]
        body["page_info"] = ["has_next_page": hasNext, "end_cursor": hasNext ? "opaque" as Any : NSNull()]
        return try JSONSerialization.data(withJSONObject: body)
    }

    private func loadedModel() async throws -> NativeRouteSurfaceViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers[path] = try (page(stories: 1, hasNext: true), 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        await model.load()
        return model
    }

    func testCurrentContinuationUpdatesExistingReferenceCounts() async throws {
        let model = try await loadedModel()
        CannedFeedURLProtocol.handlers[path] = try (page(stories: 7, hasNext: false), 200)
        await model.loadMoreUserProfileRows()
        XCTAssertEqual(model.profileHackerNewsReferences[postId]?.storyCount, 7)
        XCTAssertFalse(model.userProfile.pagination.hasMore)
    }

    func testSupersededContinuationCannotOverwriteReloadedReferenceCounts() async throws {
        let model = try await loadedModel()
        CannedFeedURLProtocol.handlers[path] = try (page(stories: 9, hasNext: false), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        let oldBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let oldPage = Task { await model.loadMoreUserProfileRows() }
        do {
            _ = try await oldBarrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await oldPage.value
            throw error
        }
        CannedFeedURLProtocol.handlers[path] = try (page(stories: 5, hasNext: false), 200)
        let freshBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        model.state = .idle
        let reload = Task { await model.load() }
        do {
            _ = try await freshBarrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await oldPage.value
            await reload.value
            throw error
        }
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await reload.value
        XCTAssertEqual(model.profileHackerNewsReferences[postId]?.storyCount, 5)
        CannedFeedURLProtocol.releaseOldestResponse(path: path)
        await oldPage.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        XCTAssertEqual(model.profileHackerNewsReferences[postId]?.storyCount, 5)
        XCTAssertFalse(model.userProfile.pagination.hasMore)
    }
}
