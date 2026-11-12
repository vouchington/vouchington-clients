import Foundation
import Observation
import SwiftUI
import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SourceCreationIntegrationTests: NativeRouteSurfaceViewModelTestCase {
    func testSourceListCreationControlAndAccountChangeClearState() async throws {
        let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
        let view = SourcesListView(viewModel: list, scope: .your, navigationTitle: "Sources")
        CannedFeedURLProtocol.handlers["/api/v1/users/account/rss-feeds/following"] = (
            Data(#"{"results":[],"topic_elections":{}}"#.utf8), 200
        )
        let loaded = expectation(description: "hosted source list fetch completes before teardown")
        observeSourceLoad(list, completion: loaded)
        let creation = view.creationModel
        XCTAssertEqual(view.creationIdentity(), "account")
        view.navigateToSource("/source/topic")
        creation.url = "https://example.com/feed"
        try await ViewHosting.host(NavigationStack { view }.frame(width: 600, height: 500)) {
            await fulfillment(of: [loaded], timeout: 2)
            XCTAssertNoThrow(try view.inspect().group().toolbar().itemGroup().find(button: "Create").tap())
            XCTAssertNoThrow(try view.inspect().group().callOnChange(
                oldValue: "account" as String?, newValue: nil as String?
            ))
            XCTAssertEqual(creation.url, "")
            XCTAssertFalse(creation.submitted)
        }
    }

    private func observeSourceLoad(_ model: SourcesListViewModel, completion: XCTestExpectation) {
        withObservationTracking { _ = model.yourLoaded } onChange: {
            Task { @MainActor in
                if model.yourLoaded {
                    completion.fulfill()
                } else {
                    self.observeSourceLoad(model, completion: completion)
                }
            }
        }
    }

    func testExplicitCurrentAPIOverridesRetainedListClient() throws {
        let oldClient = try makeClient()
        let current = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:3999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let list = SourcesListViewModel(client: oldClient, userId: "old-account", feedType: "article")
        let view = SourcesListView(
            viewModel: list,
            scope: .your,
            navigationTitle: "Sources",
            creationClient: current,
            creationIdentity: { "current-account" }
        )
        XCTAssertTrue(view.creationClient === current)
        XCTAssertEqual(view.creationModel.baseURL, current.baseURL)
        XCTAssertEqual(view.creationModel.identity(), "current-account")
        XCTAssertNotEqual(view.creationModel.baseURL, oldClient.baseURL)
    }

    func testSignedOutSourceListHasNoCreationControl() throws {
        let list = try SourcesListViewModel(client: makeClient(), userId: nil, feedType: nil)
        let view = SourcesListView(
            viewModel: list,
            scope: .your,
            navigationTitle: "Sources",
            isSignedIn: false,
            creationIdentity: { nil }
        )
        XCTAssertThrowsError(try view.inspect().group().toolbar().itemGroup().find(button: "Create"))
    }
}
