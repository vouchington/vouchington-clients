import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeReferralLinksManagementViewTests: NativeRouteSurfaceViewModelTestCase {
    func testReferralLinkRowsShowLoadMoreActionWhenMoreLinksExist() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (referralLinksPage(ids: ["referral-link-1"], endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (referralLinksPage(ids: ["referral-link-2"], endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadLinks()

        let sut = NativeReferralLinksRows(viewModel: viewModel)
        XCTAssertNoThrow(try sut.inspect().find(button: "Load more"))
    }

    func testReferralLinkRowsRenderEmptyAndManageActions() async throws {
        let client = try makeClient()
        let emptyViewModel = NativeReferralLinksManagementViewModel(client: client)
        XCTAssertEqual(
            try NativeReferralLinksRows(viewModel: emptyViewModel)
                .inspect()
                .find(text: "No referral links")
                .string(),
            "No referral links"
        )

        seedReferralLinksFeed()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)
        await viewModel.loadLinks()
        let sut = NativeReferralLinksRows(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "My test card link").string(), "My test card link")
        XCTAssertEqual(
            try sut.inspect().find(text: "https://example.com/apply/test-card").string(),
            "https://example.com/apply/test-card"
        )
        XCTAssertNoThrow(try sut.inspect().find(button: "Rename"))
        try sut.inspect().find(button: "Rename").tap()
        try sut.inspect().find(button: "Deactivate").tap()
        try sut.inspect().find(button: "Delete").tap()
    }

    func testReferralLinkRowsGroupProgramsAlphabetically() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            groupedReferralLinksPage(),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadLinks()
        let texts = try NativeReferralLinksRows(viewModel: viewModel)
            .inspect()
            .findAll(ViewType.Text.self)
            .map { try $0.string() }

        let alphaProgramIndex = try XCTUnwrap(texts.firstIndex(of: "Alpha Program"))
        let alphaLinkIndex = try XCTUnwrap(texts.firstIndex(of: "Alpha link"))
        let betaProgramIndex = try XCTUnwrap(texts.firstIndex(of: "Beta Program"))
        let betaLinkIndex = try XCTUnwrap(texts.firstIndex(of: "Beta link"))
        XCTAssertLessThan(alphaProgramIndex, alphaLinkIndex)
        XCTAssertLessThan(alphaLinkIndex, betaProgramIndex)
        XCTAssertLessThan(betaProgramIndex, betaLinkIndex)
    }

    func testReferralClickRowsRenderClickSignupAndLoadMoreAction() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/referral-clicks"] = [
            (referralClicksPage(id: "click-1", endCursor: "cursor-1", hasNextPage: true), 200, 0),
            (referralClicksPage(id: "click-2", endCursor: nil, hasNextPage: false), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadClicks()
        let sut = NativeReferralClickRows(viewModel: viewModel)
        let locale = Locale(identifier: "es")
        let timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let expectedTimestamp = UiMessages.date(
            viewModel.clicks[0].createdAt,
            date: .abbreviated,
            time: .shortened,
            locale: locale,
            timeZone: timeZone
        )

        XCTAssertEqual(try sut.inspect().find(text: "Click analytics").string(), "Click analytics")
        XCTAssertNoThrow(try sut.inspect().find(text: "https://example.com/click-1"))
        XCTAssertEqual(
            NativeReferralClickRows.timestamp(viewModel.clicks[0], locale: locale, timeZone: timeZone),
            expectedTimestamp
        )
        XCTAssertNoThrow(try sut.inspect().find(text: "Click"))
        try sut.inspect().find(button: "Load more").tap()
        try await waitForClickCount(2, in: viewModel)

        XCTAssertEqual(viewModel.clicks.map(\.id), ["click-1", "click-2"])
    }

    func testReferralClickRowsRenderAnonymousSignupWhenUsernameIsMissing() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            anonymousSignupClickPage(),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadClicks()
        let sut = NativeReferralClickRows(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Signup: Anonymous"))
    }

    func testReferralClickRowsRenderNamedSignup() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            ApiFixtureLoader.data("native.referral-clicks.mine.default"),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.loadClicks()
        let sut = NativeReferralClickRows(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Signup: @newmember"))
    }

    func testReferralManagementViewRendersSignedOutAndLoadedContent() async throws {
        let signedOut = NativeReferralLinksManagementView(client: nil)
        XCTAssertEqual(try signedOut.inspect().find(text: "Sign in required").string(), "Sign in required")

        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            emptyReferralClicksFeed(),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            ApiFixtureLoader.data("web.topics.search.referral-programs.default"),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)
        await viewModel.load()
        let sut = NativeReferralLinksManagementView(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "My referral links"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Click analytics"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Search"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Add referral link"))
    }

    func testReferralManagementViewRendersLoadingAndErrorStates() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/referral-links"] = [
            (emptyReferralLinksFeed(), 200, 0.2)
        ]
        let client = try makeClient()
        let loadingViewModel = NativeReferralLinksManagementViewModel(client: client)

        async let loadTask: Void = loadingViewModel.loadLinks()
        try await Task.sleep(nanoseconds: 20_000_000)
        XCTAssertNoThrow(try NativeReferralLinksManagementView(viewModel: loadingViewModel)
            .inspect()
            .find(ViewType.ProgressView.self))
        await loadTask

        ThrowingFeedURLProtocol.lastURL = nil
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let errorClient = APIClient(
            config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [ThrowingFeedURLProtocol.self]
        )
        let errorViewModel = NativeReferralLinksManagementViewModel(client: errorClient)

        await errorViewModel.loadLinks()
        let errorView = NativeReferralLinksManagementView(viewModel: errorViewModel)

        XCTAssertEqual(ThrowingFeedURLProtocol.lastURL?.path, "/api/v1/referral-links")
        XCTAssertEqual(try errorView.inspect().find(text: "An error occurred.").string(), "An error occurred.")
        XCTAssertNoThrow(try errorView.inspect().find(button: "Try Again"))
    }

    func testReferralManagementViewShowsProgramPickerAfterSearch() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            ApiFixtureLoader.data("web.topics.search.referral-programs.default"),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)

        await viewModel.searchReferralPrograms(query: "test")
        let sut = NativeReferralLinksManagementView(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(ViewType.Picker.self))
        XCTAssertEqual(viewModel.programs.first?.name, "Test Referral Program")
    }

    func testReferralManagementViewRespectsRouteMode() async throws {
        seedReferralLinksFeed()
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            referralClicksPage(id: "click-1", endCursor: nil, hasNextPage: false),
            200
        )
        let client = try makeClient()
        let viewModel = NativeReferralLinksManagementViewModel(client: client)
        await viewModel.load()

        let management = NativeReferralLinksManagementView(viewModel: viewModel, mode: .management)
        XCTAssertNoThrow(try management.inspect().find(text: "My referral links"))
        XCTAssertThrowsError(try management.inspect().find(text: "Click analytics"))

        let analytics = NativeReferralLinksManagementView(viewModel: viewModel, mode: .analytics)
        XCTAssertNoThrow(try analytics.inspect().find(text: "Click analytics"))
        XCTAssertThrowsError(try analytics.inspect().find(text: "My referral links"))
        XCTAssertThrowsError(try analytics.inspect().find(button: "Add referral link"))
    }

    private func seedReferralLinksFeed() {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            ApiFixtureLoader.data("native.referral-links.mine.default"),
            200
        )
    }

    private func referralLinksPage(ids: [String], endCursor: String?, hasNextPage: Bool) -> Data {
        let results = ids.map { #"{"id":"\#($0)"}"# }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            """
            {
              "results": [\(results)],
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(cursor),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func emptyReferralLinksFeed() -> Data {
        Data(
            """
            {
              "results": [],
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func groupedReferralLinksPage() -> Data {
        Data(
            """
            {
              "results": [
                {
                  "id": "beta-link",
                  "label": "Beta link",
                  "url": "https://example.com/beta",
                  "referral_program_name": "Beta Program"
                },
                {
                  "id": "alpha-link",
                  "label": "Alpha link",
                  "url": "https://example.com/alpha",
                  "referral_program_name": "Alpha Program"
                }
              ],
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func referralClicksPage(id: String, endCursor: String?, hasNextPage: Bool) -> Data {
        Data(
            """
            {
              "results": [{ "id": "\(id)" }],
              "clicks": {
                "\(id)": {
                  "id": "\(id)",
                  "landing_url": "https://example.com/\(id)",
                  "signed_up_at": null,
                  "user_id": null,
                  "created_at": "2026-03-01T12:00:00Z"
                }
              },
              "users": {},
              "page_info": {
                "has_next_page": \(hasNextPage),
                "end_cursor": \(endCursor.map { "\"\($0)\"" } ?? "null"),
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func waitForClickCount(
        _ expectedCount: Int,
        in viewModel: NativeReferralLinksManagementViewModel
    ) async throws {
        for _ in 0 ..< 100 {
            if viewModel.clicks.count == expectedCount {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for \(expectedCount) referral clicks")
    }

    private func anonymousSignupClickPage() -> Data {
        Data(
            """
            {
              "results": [{ "id": "click-1" }],
              "clicks": {
                "click-1": {
                  "id": "click-1",
                  "landing_url": "https://example.com/click-1",
                  "signed_up_at": "2026-03-01T12:00:00Z",
                  "user_id": "user-1",
                  "created_at": "2026-03-01T12:00:00Z"
                }
              },
              "users": {
                "user-1": {
                  "id": "user-1",
                  "roles": [],
                  "profile_image_id": null,
                  "markdown": null
                }
              },
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }

    private func emptyReferralClicksFeed() -> Data {
        Data(
            """
            {
              "results": [],
              "clicks": {},
              "users": {},
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8
        )
    }
}
