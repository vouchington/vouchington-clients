import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class HackerNewsInitialLoadTests: NativeRouteSurfaceViewModelTestCase {
    func testSupersededUnfilteredFailureCannotReplaceFilteredSuccess() async throws {
        let path = "/api/v1/posts"
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 503)
        CannedFeedURLProtocol.suspendResponse(path: path)
        let oldBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let oldLoad = Task { await model.load() }
        do {
            _ = try await oldBarrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await oldLoad.value
            throw error
        }
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("native.posts.hacker-news.filtered"), 200)
        let freshBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        model.hasHackerNewsReferences = true
        do {
            let request = try await freshBarrier.wait()
            let query = URLComponents(url: request.url, resolvingAgainstBaseURL: false)?.queryItems
            XCTAssertEqual(query?.first { $0.name == "has_hacker_news_references" }?.value, "true")
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await oldLoad.value
            await model.hackerNewsFilterTask?.value
            throw error
        }
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await model.hackerNewsFilterTask?.value
        XCTAssertTrue(model.rows.contains { $0.detail.contains("Seen on Hacker News") })
        let filteredTitles = model.rows.map(\.title)
        CannedFeedURLProtocol.releaseOldestResponse(path: path)
        await oldLoad.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        guard case .loaded = model.state else {
            return XCTFail("A superseded request must not replace the current filtered state")
        }
        XCTAssertEqual(model.rows.map(\.title), filteredTitles)
    }
}
