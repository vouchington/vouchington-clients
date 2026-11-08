import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class LandingPageCandidatePaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testPagesAppendInServerOrderAndForwardOpaqueCursorUntilExhausted() async throws {
        let model = try LandingPagesViewModel(client: makeClient())
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let cursor = "opaque+/= &cursor"
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = try (
            candidatePage(ids: ["z", "a"], cursor: cursor), 200
        )
        await model.load()
        XCTAssertEqual(model.candidates.profileLinks.map(\.id), ["z", "a"])
        XCTAssertEqual(model.candidates.reviews.map(\.id), ["z", "a"])
        XCTAssertEqual(model.candidates.referralLinks.map(\.id), ["z", "a"])
        XCTAssertEqual(model.itemLimit, 100)
        XCTAssertTrue(model.hasMoreCandidates)
        XCTAssertNoThrow(try LandingPageContentSection(viewModel: model, locale: Locale(identifier: "en"))
            .inspect().find(button: "Load more"))
        let hydratedID = try XCTUnwrap(model.draftItems.first?.id)
        model.removeItem(id: hydratedID)
        model.addType = .profileLink
        model.selectedCandidateID = "profile-link-1"
        XCTAssertFalse(model.canAddItem)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = try (
            candidatePage(ids: ["a", "m", "profile-link-1"], cursor: nil), 200
        )
        await model.loadMoreCandidates()
        XCTAssertEqual(model.candidates.profileLinks.map(\.id), ["z", "a", "m", "profile-link-1"])
        XCTAssertEqual(model.candidates.reviews.map(\.id), ["z", "a", "m", "profile-link-1"])
        XCTAssertEqual(model.candidates.referralLinks.map(\.id), ["z", "a", "m", "profile-link-1"])
        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(
            URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?
                .first { $0.name == "after" }?.value,
            cursor
        )
        model.selectedCandidateID = "profile-link-1"
        XCTAssertTrue(model.canAddItem)
        XCTAssertFalse(model.hasMoreCandidates)
        XCTAssertThrowsError(try LandingPageContentSection(viewModel: model, locale: Locale(identifier: "en"))
            .inspect().find(button: "Load more"))
        let count = CannedFeedURLProtocol.capturedURLs.count
        await model.loadMoreCandidates()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, count)
    }

    func testHydratedItemsStayDisplayOnlyAndNestedEntriesCountTowardLimit() throws {
        let model = LandingPagesViewModel(client: nil)
        let response = try decoder.decode(
            LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        )
        model.setActivePage(response.landingPage)
        model.itemLimit = 8
        XCTAssertEqual(model.draftItemCount, 7)
        model.addType = .link
        model.linkLabel = "Docs"
        model.linkUrl = "https://example.com/docs"
        XCTAssertTrue(model.canAddItem)
        model.addSelectedItem()
        XCTAssertFalse(model.canAddItem)
        model.removeItem(id: response.landingPage.items[0].id)
        model.addType = .profileLink
        XCTAssertTrue(model.profileLinkChoices.isEmpty)
        model.selectedCandidateID = "profile-link-1"
        XCTAssertFalse(model.canAddItem)
    }

    func testTopicGroupAdditionIncludesContainerAndEverySelectedEntry() throws {
        let model = LandingPagesViewModel(client: nil)
        try model.setActivePage(decoder.decode(
            LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        )
        .landingPage)
        model.draftItems = []
        model.candidates = try decoder.decode(
            LandingPageCandidatesResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-candidates.default")
        )
        .candidates
        model.addType = .topicGroup
        model.selectedTopicID = "topic-1"
        model.selectedGroupReviewIDs = ["review-1"]
        model.selectedGroupReferralLinkIDs = ["referral-link-1"]
        model.itemLimit = 2
        XCTAssertFalse(model.canAddItem)
        model.itemLimit = 3
        XCTAssertTrue(model.canAddItem)
        model.addSelectedItem()
        XCTAssertEqual(model.draftItemCount, 3)
    }

    func testCodedCapRefusalRetainsDraftAndPresentsServerMessage() async throws {
        let model = try LandingPagesViewModel(client: makeClient())
        let response = try decoder.decode(
            LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        )
        model.setActivePage(response.landingPage)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1/items"] = (
            Data(#"{"code":"LANDING_PAGE_ITEM_LIMIT_REACHED","message":"Landing page item limit reached"}"#.utf8), 409
        )
        await model.saveContent()
        XCTAssertTrue(model.isItemLimitReached)
        XCTAssertEqual(model.draftItems, response.landingPage.items)
        XCTAssertEqual(
            model.errorMessage.map { UiMessages.string($0, locale: .english) },
            "Landing page item limit reached"
        )
    }

    func testEditorRetainsCapsAcrossDetailAndEveryMutation() async throws {
        let model = try LandingPagesViewModel(client: makeClient())
        let path = "/api/v1/my/landing-pages/landing-page-1"
        CannedFeedURLProtocol.handlers[path] = try (
            cappedFixture("native.landing-page-detail.default", limit: 17), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = try (
            cappedFixture("native.landing-page-mutation.default", limit: 12), 201
        )
        await model.createPage(title: "Home", slug: "home")
        XCTAssertEqual(model.itemLimit, 17)
        CannedFeedURLProtocol.handlers[path] = try (
            cappedFixture("native.landing-page-mutation.default", limit: 9), 200
        )
        await model.saveDetails()
        XCTAssertEqual(model.itemLimit, 9)
        CannedFeedURLProtocol.handlers[path] = try (
            cappedFixture("native.landing-page-mutation.default", limit: 8), 200
        )
        await model.setDefault()
        XCTAssertEqual(model.itemLimit, 8)
        CannedFeedURLProtocol.handlers[path + "/items"] = try (
            cappedFixture("native.landing-page-items-mutation.default", limit: 7), 200
        )
        model.isItemLimitReached = true
        await model.saveContent()
        XCTAssertEqual(model.itemLimit, 7)
        XCTAssertFalse(model.isItemLimitReached)
    }

    func testPendingPaginationSerializesEditorOperationsOnSuccessAndFailure() async throws {
        for status in [200, 500] {
            let model = try LandingPagesViewModel(client: makeClient())
            try model.setActivePage(decoder.decode(
                LandingPageWithItemsResponse.self,
                from: ApiFixtureLoader.data("native.landing-page-detail.default")
            )
            .landingPage)
            let firstPage = try decoder.decode(
                VouchaFeatures.LandingPageCandidatesResponse.self,
                from: candidatePage(ids: ["eligible"], cursor: "opaque-next")
            )
            model.candidates = firstPage.candidates
            model.candidatePageInfo = firstPage.pageInfo
            model.state = .loaded
            model.linkLabel = "Draft"
            model.linkUrl = "https://example.com/draft"
            let draft = model.draftItems
            let path = "/api/v1/my/landing-pages/candidates"
            CannedFeedURLProtocol.handlers[path] = try (candidatePage(ids: ["later"], cursor: nil), status)
            let suspended = expectation(description: "candidate page suspended")
            CannedFeedURLProtocol.suspendResponse(path: path) { suspended.fulfill() }
            let pagination = Task { await model.loadMoreCandidates() }
            await fulfillment(of: [suspended], timeout: 2)
            XCTAssertTrue(model.isLoading)
            XCTAssertFalse(model.canAddItem)
            XCTAssertFalse(model.canCreatePage)
            let requestCount = CannedFeedURLProtocol.capturedURLs.count
            await model.saveContent()
            await model.saveDetails()
            await model.selectPage(id: "other-page")
            await model.createPage(title: "New", slug: "new")
            model.addSelectedItem()
            try model.removeItem(id: XCTUnwrap(draft.first?.id))
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requestCount)
            XCTAssertEqual(model.draftItems, draft)
            XCTAssertTrue(model.isLoading)
            CannedFeedURLProtocol.releaseResponse(path: path)
            await pagination.value
            XCTAssertFalse(model.isLoading)
            XCTAssertEqual(model.draftItems, draft)
            XCTAssertEqual(model.hasMoreCandidates, status != 200)

            let savePath = "/api/v1/my/landing-pages/landing-page-1/items"
            CannedFeedURLProtocol.handlers[savePath] = (
                ApiFixtureLoader.data("native.landing-page-items-mutation.default"),
                200
            )
            let saveSuspended = expectation(description: "save suspended")
            CannedFeedURLProtocol.suspendResponse(path: savePath) { saveSuspended.fulfill() }
            let save = Task { await model.saveContent() }
            await fulfillment(of: [saveSuspended], timeout: 2)
            XCTAssertTrue(model.isLoading)
            let savingRequestCount = CannedFeedURLProtocol.capturedURLs.count
            await model.loadMoreCandidates()
            model.addSelectedItem()
            XCTAssertTrue(model.isLoading)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, savingRequestCount)
            XCTAssertEqual(model.draftItems, draft)
            CannedFeedURLProtocol.releaseResponse(path: savePath)
            await save.value
            XCTAssertFalse(model.isLoading)
            if case .loaded = model.state {} else { XCTFail("Expected successful save") }
        }
    }

    private func cappedFixture(_ id: String, limit: Int) throws -> Data {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: ApiFixtureLoader.data(id)) as? [String: Any])
        object["item_limit"] = limit
        return try JSONSerialization.data(withJSONObject: object)
    }

    private var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }

    private func candidatePage(ids: [String], cursor: String?) throws -> Data {
        let fixture = try XCTUnwrap(JSONSerialization.jsonObject(with:
            ApiFixtureLoader.data("native.landing-page-candidates.default")) as? [String: Any])
        let candidates = try XCTUnwrap(fixture["candidates"] as? [String: Any])
        func records(_ key: String) throws -> [[String: Any]] {
            let record = try XCTUnwrap((candidates[key] as? [[String: Any]])?.first)
            return ids.map { id in
                var value = record
                value["id"] = id
                return value
            }
        }
        return try JSONSerialization.data(withJSONObject: [
            "item_limit": 100,
            "page_info": [
                "start_cursor": NSNull(),
                "has_next_page": cursor != nil,
                "end_cursor": cursor as Any? ?? NSNull()
            ],
            "candidates": [
                "can_create_landing_pages": true,
                "profile_links": records("profile_links"),
                "reviews": records("reviews"),
                "referral_links": records("referral_links")
            ]
        ])
    }
}
