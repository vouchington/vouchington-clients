import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class LandingPagesDraftRefreshTests: NativeRouteSurfaceViewModelTestCase {
    func testSamePageRefreshPreservesDirtySectionsAndAdoptsCleanSections() async throws {
        let viewModel = try await loadedViewModel()
        viewModel.title = "Draft title"
        CannedFeedURLProtocol.handlers[detailPath] = try (detailData(title: "Server title", items: []), 200)

        await viewModel.reload()

        XCTAssertEqual(viewModel.title, "Draft title")
        XCTAssertTrue(viewModel.hasUnsavedMetadata)
        XCTAssertTrue(viewModel.draftItems.isEmpty)
        XCTAssertFalse(viewModel.hasUnsavedItems)

        viewModel.title = "Server title"
        viewModel.linkLabel = "Draft"
        viewModel.linkUrl = "https://example.com/draft"
        XCTAssertTrue(viewModel.addLinkItem())
        let draftItems = viewModel.draftItems
        CannedFeedURLProtocol.handlers[detailPath] = try (detailData(title: "Server title 2", items: []), 200)

        await viewModel.reload()

        XCTAssertEqual(viewModel.title, "Server title 2")
        XCTAssertFalse(viewModel.hasUnsavedMetadata)
        XCTAssertEqual(viewModel.draftItems, draftItems)
        XCTAssertTrue(viewModel.hasUnsavedItems)
    }

    func testRefreshFailurePreservesAllLandingPageState() async throws {
        let viewModel = try await loadedViewModel()
        viewModel.title = "Draft title"
        try viewModel.removeItem(id: XCTUnwrap(viewModel.draftItems.last?.id))
        viewModel.addType = .review
        viewModel.selectedCandidateID = nil
        let pages = viewModel.pages
        let candidates = viewModel.candidates
        let selectedPage = viewModel.selectedPage
        let draftItems = viewModel.draftItems
        let metadataBaseline = viewModel.persistedMetadataBaseline
        let itemBaseline = viewModel.persistedItemInputBaseline
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (Data(#"{}"#.utf8), 500)

        await viewModel.reload()

        XCTAssertEqual(viewModel.pages, pages)
        XCTAssertEqual(viewModel.candidates, candidates)
        XCTAssertEqual(viewModel.selectedPage, selectedPage)
        XCTAssertEqual(viewModel.draftItems, draftItems)
        XCTAssertEqual(viewModel.persistedMetadataBaseline, metadataBaseline)
        XCTAssertEqual(viewModel.persistedItemInputBaseline, itemBaseline)
    }

    func testRefreshClearsMissingPageAndExplicitSwitchDiscardsDrafts() async throws {
        let viewModel = try await loadedViewModel()
        viewModel.title = "Draft title"
        try viewModel.removeItem(id: XCTUnwrap(viewModel.draftItems.last?.id))
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (missingSelectedPageList, 200)

        await viewModel.reload()

        XCTAssertNil(viewModel.selectedPage)
        XCTAssertTrue(viewModel.draftItems.isEmpty)
        XCTAssertFalse(viewModel.hasUnsavedMetadata)
        XCTAssertFalse(viewModel.hasUnsavedItems)

        try viewModel.setActivePage(decodeDetail())
        viewModel.title = "Another draft"
        viewModel.linkLabel = "Page A link"
        viewModel.linkUrl = "https://example.com/page-a"
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = try (
            detailData(id: "landing-page-2", title: "Travel Stack", slug: "travel", items: []),
            200
        )
        await viewModel.selectPage(id: "landing-page-2")
        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
        XCTAssertEqual(viewModel.title, "Travel Stack")
        XCTAssertEqual(viewModel.addType, .link)
        XCTAssertTrue(viewModel.linkLabel.isEmpty)
        XCTAssertTrue(viewModel.linkUrl.isEmpty)
        XCTAssertFalse(viewModel.hasUnsavedMetadata)
    }

    func testRouteDrivenPageChangeClearsTransientPickerInput() async throws {
        let viewModel = try await loadedViewModel()
        viewModel.addType = .review
        viewModel.selectedCandidateID = viewModel.availableReviews.first?.id
        viewModel.linkLabel = "Page A draft"
        viewModel.linkUrl = "https://example.com/page-a"
        viewModel.initialSlug = "travel"
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (missingSelectedPageList, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = try (
            detailData(id: "landing-page-2", title: "Travel Stack", slug: "travel", items: []),
            200
        )

        await viewModel.reload()

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
        XCTAssertEqual(viewModel.addType, .link)
        XCTAssertNil(viewModel.selectedCandidateID)
        XCTAssertTrue(viewModel.linkLabel.isEmpty)
        XCTAssertTrue(viewModel.linkUrl.isEmpty)
    }

    func testSaveContentAcceptsServerItemsAndPreservesDirtyMetadata() async throws {
        let viewModel = try LandingPagesViewModel(client: makeClient())
        try viewModel.setActivePage(decodeDetail().replacingItems([]))
        viewModel.title = "Draft title"
        viewModel.linkLabel = "Draft"
        viewModel.linkUrl = "https://example.com/draft"
        viewModel.addLinkItem()
        CannedFeedURLProtocol.handlers["\(detailPath)/items"] = (
            ApiFixtureLoader.data("native.landing-page-items-mutation.default"), 200
        )

        await viewModel.saveContent()

        XCTAssertEqual(viewModel.title, "Draft title")
        XCTAssertTrue(viewModel.hasUnsavedMetadata)
        XCTAssertFalse(viewModel.hasUnsavedItems)
        XCTAssertEqual(viewModel.draftItems.map(\.id), [
            "item-profile-1", "item-review-1", "item-referral-1", "item-topic-group-1", "item-link-1"
        ])
    }

    func testMetadataAndDefaultSavesPreserveDirtyItems() async throws {
        let viewModel = try LandingPagesViewModel(client: makeClient())
        try viewModel.setActivePage(decodeDetail())
        try viewModel.removeItem(id: XCTUnwrap(viewModel.draftItems.last?.id))
        let draft = viewModel.draftItems
        viewModel.title = "Updated Links"
        viewModel.slug = "updated-links"
        CannedFeedURLProtocol.queuedHandlers[detailPath] = [
            (ApiFixtureLoader.data("native.landing-page-mutation.default"), 200, 0),
            (ApiFixtureLoader.data("native.landing-page-mutation.default"), 200, 0)
        ]

        await viewModel.saveDetails()
        XCTAssertEqual(viewModel.draftItems, draft)
        XCTAssertTrue(viewModel.hasUnsavedItems)
        XCTAssertFalse(viewModel.hasUnsavedMetadata)

        await viewModel.setDefault()
        XCTAssertEqual(viewModel.draftItems, draft)
        XCTAssertTrue(viewModel.hasUnsavedItems)
    }

    func testSaveFailuresPreserveDraftsAndBaselines() async throws {
        let viewModel = try LandingPagesViewModel(client: makeClient())
        try viewModel.setActivePage(decodeDetail())
        viewModel.title = "Draft title"
        try viewModel.removeItem(id: XCTUnwrap(viewModel.draftItems.last?.id))
        let draft = viewModel.draftItems
        let metadataBaseline = viewModel.persistedMetadataBaseline
        let itemBaseline = viewModel.persistedItemInputBaseline
        CannedFeedURLProtocol.handlers["\(detailPath)/items"] = (Data(#"{}"#.utf8), 500)
        CannedFeedURLProtocol.handlers[detailPath] = (Data(#"{}"#.utf8), 500)

        await viewModel.saveContent()
        await viewModel.saveDetails()

        XCTAssertEqual(viewModel.title, "Draft title")
        XCTAssertEqual(viewModel.draftItems, draft)
        XCTAssertEqual(viewModel.persistedMetadataBaseline, metadataBaseline)
        XCTAssertEqual(viewModel.persistedItemInputBaseline, itemBaseline)

        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.errors["\(detailPath)/items"] = CancellationError()
        viewModel.linkLabel = "Unsubmitted label"
        viewModel.linkUrl = "https://example.com/unsubmitted"
        await viewModel.saveContent()

        XCTAssertEqual(viewModel.title, "Draft title")
        XCTAssertEqual(viewModel.draftItems, draft)
        XCTAssertEqual(viewModel.linkLabel, "Unsubmitted label")
        XCTAssertEqual(viewModel.linkUrl, "https://example.com/unsubmitted")
        XCTAssertEqual(viewModel.persistedMetadataBaseline, metadataBaseline)
        XCTAssertEqual(viewModel.persistedItemInputBaseline, itemBaseline)
    }

    private let detailPath = "/api/v1/my/landing-pages/landing-page-1"

    private var missingSelectedPageList: Data {
        Data(
            #"{"results":[{"id":"landing-page-2","user_id":"user-abc","title":"Travel Stack","subtitle":null,"slug":"travel","is_default":true,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z"}],"page_info":{"has_next_page":false,"end_cursor":null}}"#
                .utf8
        )
    }

    private func loadedViewModel() async throws -> LandingPagesViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        await viewModel.load()
        return viewModel
    }

    private func decodeDetail() throws -> LandingPageWithItems {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        ).landingPage
    }

    private func detailData(
        id: String = "landing-page-1",
        title: String,
        slug: String = "links",
        items: [[String: Any]]
    ) throws -> Data {
        var root = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("native.landing-page-detail.default"))
                as? [String: Any]
        )
        var page = try XCTUnwrap(root["landing_page"] as? [String: Any])
        page["id"] = id
        page["title"] = title
        page["slug"] = slug
        page["items"] = items
        root["landing_page"] = page
        return try JSONSerialization.data(withJSONObject: root)
    }
}

private extension LandingPageWithItems {
    func replacingItems(_ items: [LandingPageItem]) -> LandingPageWithItems {
        LandingPageWithItems(
            id: id,
            userId: userId,
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: createdAt,
            updatedAt: updatedAt,
            items: items
        )
    }
}
