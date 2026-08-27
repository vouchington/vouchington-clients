import Foundation
import ViewInspector
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class HnDiscussionsPanelTests: NativeRouteSurfaceViewModelTestCase {
    func testRendersMatchingThreadsWhenThePreferenceIsOn() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: ["hn_discussions": true]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/search"] = (
            Data("""
            {"hits":[{"objectID":"123","title":"Example","url":"https://example.com/a/","points":42,"num_comments":18}]}
            """.utf8),
            200
        )
        let loader = try HnDiscussionsLoader(
            urls: ["https://example.com/a"],
            client: makeClient(),
            searchClient: HnDiscussionsClient(session: makeSearchSession())
        )
        await loader.load()
        XCTAssertEqual(loader.threads.map(\.title), ["Example"])

        let sut = HnDiscussionsPanel(loader: loader)
        XCTAssertNoThrow(try sut.inspect().find(viewWithAccessibilityIdentifier: "hn-discussions-aside"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Hacker News"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Example"))
        XCTAssertNoThrow(try sut.inspect().find(text: "42 points · 18 comments"))
    }

    func testHidesTheAsideWhenThePreferenceIsOff() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: ["hn_discussions": false]),
            200
        )
        let loader = try HnDiscussionsLoader(urls: ["https://example.com/a"], client: makeClient())
        await loader.load()
        XCTAssertEqual(loader.threads, [])
        XCTAssertThrowsError(try HnDiscussionsPanel(loader: loader).inspect().find(text: "Hacker News"))
    }

    func testHidesTheAsideWhenSignedOut() throws {
        let sut = HnDiscussionsPanel(urls: ["https://example.com/a"])
        XCTAssertThrowsError(try sut.inspect().find(text: "Hacker News"))
    }

    private func makeSearchSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CannedFeedURLProtocol.self]
        return URLSession(configuration: configuration)
    }
}
