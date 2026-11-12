import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class SourceCreationIntegrationTests: NativeRouteSurfaceViewModelTestCase {
    func testSourceListCreationControlAndAccountChangeClearState() async throws {
        let list = try SourcesListViewModel(client: makeClient(), userId: "account", feedType: "article")
        let view = SourcesListView(viewModel: list, scope: .your, navigationTitle: "Sources")
        let creation = view.creationModel
        XCTAssertEqual(view.creationIdentity(), "account")
        creation.url = "https://example.com/feed"
        try await ViewHosting.host(view) {
            try view.inspect().group().toolbar().item().button().tap()
            try view.inspect().group().callOnChange(oldValue: "account" as String?, newValue: nil as String?)
            XCTAssertEqual(creation.url, "")
            XCTAssertFalse(creation.submitted)
        }
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
        XCTAssertThrowsError(try view.inspect().group().toolbar().item().button())
    }
}
