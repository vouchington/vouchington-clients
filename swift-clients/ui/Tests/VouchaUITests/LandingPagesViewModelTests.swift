import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class LandingPagesViewModelTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testLoadFetchesPagesCandidatesAndSelectedDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.pages.map(\.id), ["landing-page-1", "landing-page-2"])
        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-1")
        XCTAssertEqual(viewModel.draftItems.count, 5)
        XCTAssertTrue(viewModel.canCreatePage)
        XCTAssertEqual(viewModel.candidates.profileLinks.first?.id, "profile-link-1")
        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(\.path)), Set([
            "/api/v1/my/landing-pages",
            "/api/v1/my/landing-pages/candidates",
            "/api/v1/my/landing-pages/landing-page-1"
        ]))
    }

    func testLoadSelectsInitialSlugWhenAvailable() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                """
                {
                  "landing_page": {
                    "id": "landing-page-2",
                    "user_id": "user-abc",
                    "title": "Travel Stack",
                    "subtitle": null,
                    "slug": "travel",
                    "is_default": false,
                    "created_at": "2026-06-28T11:00:00Z",
                    "updated_at": "2026-06-29T11:00:00Z",
                    "items": []
                  }
                }
                """.utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient(), initialSlug: "travel")

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/my/landing-pages/landing-page-2")
    }

    func testLoadLeavesSelectionEmptyWhenInitialSlugIsMissing() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient(), initialSlug: "missing")

        await viewModel.load()

        XCTAssertNil(viewModel.selectedPage)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/landing-pages/landing-page-1"
        })
    }

    func testLoadDisablesCreateWhenCandidatesDoNotAllowCreation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            Data(
                """
                {"candidates":{"can_create_landing_pages":false,"profile_links":[],"reviews":[],"referral_links":[]}}
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertFalse(viewModel.canCreatePage)
    }

    func testMissingClientSurfacesErrorState() async {
        let viewModel = LandingPagesViewModel(client: nil)

        await viewModel.load()
        await viewModel.createPage(title: "Links", slug: "links", subtitle: nil)

        if case .error = viewModel.state {} else {
            XCTFail("Expected missing API client to surface an error state")
        }
        XCTAssertEqual(
            viewModel.errorMessage,
            .verbatim("Landing pages are unavailable because the API client is not configured.")
        )
    }

    func testCreatePagePostsMetadataAndLoadsCreatedDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-page-mutation.default"), 201
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())

        await viewModel.createPage(title: " Updated Links ", slug: " Updated-Links ", subtitle: "  ")

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.first, "POST")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/landing-pages")
        XCTAssertBodyContains(CannedFeedURLProtocol.capturedBodies[0], #""title":"Updated Links""#)
        XCTAssertBodyContains(CannedFeedURLProtocol.capturedBodies[0], #""subtitle":null"#)
        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-1")
    }

    func testSaveDetailsCanClearSubtitleAndSetDefaultUsesSeparateBody() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-mutation.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        try viewModel.setActivePage(decodeFixture("native.landing-page-detail.default").landingPage)
        let preservedDraftItems = [
            VouchaFeatures.LandingPageItem.link(
                id: "draft-link",
                label: "Draft Link",
                url: "https://example.com/draft"
            )
        ]
        viewModel.draftItems = preservedDraftItems
        viewModel.title = "Updated Links"
        viewModel.slug = "updated-links"
        viewModel.subtitle = ""

        await viewModel.saveDetails()
        await viewModel.setDefault()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH", "PATCH"])
        XCTAssertBodyContains(CannedFeedURLProtocol.capturedBodies[0], #""subtitle":null"#)
        XCTAssertBodyContains(CannedFeedURLProtocol.capturedBodies[1], #""is_default":true"#)
        XCTAssertFalse(CannedFeedURLProtocol.capturedBodies[1]?.contains("title") == true)
        XCTAssertEqual(viewModel.draftItems, preservedDraftItems)
        XCTAssertEqual(viewModel.selectedPage?.items, preservedDraftItems)
    }

    func testDraftItemConversionCoversAllItemTypesAndSaveContent() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1/items"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        let page = try decodeFixture("native.landing-page-detail.default").landingPage
        viewModel.setActivePage(page)

        await viewModel.saveContent()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PUT"])
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies[0])
        XCTAssertTrue(body.contains(#""type":"profile_link""#))
        XCTAssertTrue(body.contains(#""profile_link_id":"profile-link-1""#))
        XCTAssertTrue(body.contains(#""type":"review""#))
        XCTAssertTrue(body.contains(#""review_id":"review-1""#))
        XCTAssertTrue(body.contains(#""type":"referral_link""#))
        XCTAssertTrue(body.contains(#""referral_link_id":"referral-link-1""#))
        XCTAssertTrue(body.contains(#""type":"topic_group""#))
        XCTAssertTrue(body.contains(#""topic_id":"topic-1""#))
        XCTAssertTrue(body.contains(#""type":"link""#))
        XCTAssertTrue(body.contains(#""url":"https:\/\/example.com\/newsletter""#))
    }

    func testDeleteReloadsPagesAndSelectsNextPage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            Data(
                #"{"results":[{"id":"landing-page-2","user_id":"user-abc","title":"Travel Stack","subtitle":null,"slug":"travel","is_default":true,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z"}],"page_info":{"has_next_page":false,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                #"{"landing_page":{"id":"landing-page-2","user_id":"user-abc","title":"Travel Stack","subtitle":null,"slug":"travel","is_default":true,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z","items":[]}}"#
                    .utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.pages = try decodeListFixture().results
        try viewModel.setActivePage(decodeFixture("native.landing-page-detail.default").landingPage)

        await viewModel.deleteSelectedPage()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE", "GET", "GET"])
        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
    }

    func testDeleteFromSlugRouteClearsSelectionWithoutSelectingFirstPage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            Data(
                #"{"results":[{"id":"landing-page-1","user_id":"user-abc","title":"Home","subtitle":null,"slug":"home","is_default":true,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z"}],"page_info":{"has_next_page":false,"end_cursor":null}}"#
                    .utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient(), initialSlug: "travel")
        viewModel.pages = try decodeListFixture().results
        viewModel.setActivePage(
            makeDetail(
                id: "landing-page-2",
                title: "Travel Stack",
                subtitle: nil,
                slug: "travel",
                isDefault: false,
                items: []
            )
        )

        await viewModel.deleteSelectedPage()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE", "GET"])
        XCTAssertNil(viewModel.selectedPage)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/landing-pages/landing-page-1"
        })
    }

    func testHelpersNormalizeMetadataAndSortPages() {
        let metadata = trimmedMetadata(
            title: "  Featured Links  ",
            subtitle: "  Curated picks  ",
            slug: "  My-Landing  "
        )
        XCTAssertEqual(metadata.title, "Featured Links")
        XCTAssertEqual(metadata.subtitlePatch, .value("Curated picks"))
        XCTAssertEqual(metadata.slug, "my-landing")
        let defaultPage = makePage(id: "page-1", title: "Home", subtitle: nil, slug: "home", isDefault: true)
        let olderPage = makePage(
            id: "page-2",
            title: "Docs",
            subtitle: nil,
            slug: "docs",
            isDefault: false,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000)
        )
        let newerPage = makePage(
            id: "page-3",
            title: "Travel",
            subtitle: nil,
            slug: "travel",
            isDefault: false,
            createdAt: Date(timeIntervalSince1970: 1_717_000_100)
        )

        let replaced = sortedReplacing(page: newerPage, in: [olderPage, defaultPage])
        XCTAssertEqual(replaced.map(\.id), ["page-1", "page-2", "page-3"])

        let updated = sortedReplacing(
            page: makePage(id: "page-2", title: "Docs 2", subtitle: nil, slug: "docs-2", isDefault: false),
            in: [olderPage, defaultPage]
        )
        XCTAssertEqual(updated.map(\.title), ["Home", "Docs 2"])

        let toggled = pageWithDefault(olderPage, isDefault: true)
        XCTAssertTrue(toggled.isDefault)
        XCTAssertFalse(olderPage.isDefault)
    }

    func testLoadClearsStaleSelectionAndReloadSelectsDefaultPage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(
            makeDetail(
                id: "existing-page",
                title: "Existing",
                subtitle: nil,
                slug: "existing",
                isDefault: false,
                items: []
            )
        )

        await viewModel.load()

        XCTAssertNil(viewModel.selectedPage)
        XCTAssertEqual(viewModel.pages.map(\.id), ["landing-page-1", "landing-page-2"])
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/my/landing-pages/landing-page-1" })

        await viewModel.reload()

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-1")
        XCTAssertFalse(viewModel.draftItems.isEmpty)
    }

    func testSelectPageLoadsSpecifiedDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                """
                {
                  "landing_page": {
                    "id": "landing-page-2",
                    "user_id": "user-abc",
                    "title": "Travel Stack",
                    "subtitle": null,
                    "slug": "travel",
                    "is_default": false,
                    "created_at": "2026-06-28T11:00:00Z",
                    "updated_at": "2026-06-29T11:00:00Z",
                    "items": []
                  }
                }
                """.utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.pages = try decodeListFixture().results

        await viewModel.selectPage(id: "landing-page-2")

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
        XCTAssertEqual(viewModel.title, "Travel Stack")
        XCTAssertEqual(viewModel.slug, "travel")
    }

    func testMutationsSurfaceVouchaAndGenericErrors() async throws {
        let page = try decodeFixture("native.landing-page-detail.default").landingPage

        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (Data(#"{}"#.utf8), 401)
        let createViewModel = try LandingPagesViewModel(client: makeClient())
        await createViewModel.createPage(title: "Links", slug: "links", subtitle: nil)
        if case .error = createViewModel.state {} else {
            XCTFail("Expected createPage to surface an error state")
        }
        XCTAssertNotNil(createViewModel.errorMessage)

        CannedFeedURLProtocol.handlers = [
            "/api/v1/my/landing-pages/\(page.id)": (Data(#"{"landing_page":{"id":"page-1"}}"#.utf8), 200)
        ]
        let saveDetailsViewModel = try LandingPagesViewModel(client: makeClient())
        saveDetailsViewModel.setActivePage(page)
        saveDetailsViewModel.title = "Updated"
        saveDetailsViewModel.slug = "updated"
        await saveDetailsViewModel.saveDetails()
        if case .error = saveDetailsViewModel.state {} else {
            XCTFail("Expected saveDetails to surface an error state")
        }
        XCTAssertFalse(
            saveDetailsViewModel.errorMessage.map { UiMessages.string($0, locale: .english).isEmpty } ?? true
        )

        CannedFeedURLProtocol.handlers = [
            "/api/v1/my/landing-pages/\(page.id)": (Data(#"{}"#.utf8), 500)
        ]
        let setDefaultViewModel = try LandingPagesViewModel(client: makeClient())
        setDefaultViewModel.setActivePage(page)
        await setDefaultViewModel.setDefault()
        if case .error = setDefaultViewModel.state {} else {
            XCTFail("Expected setDefault to surface an error state")
        }
        XCTAssertNotNil(setDefaultViewModel.errorMessage)

        CannedFeedURLProtocol.handlers = [
            "/api/v1/my/landing-pages/\(page.id)": (Data(#"{}"#.utf8), 500)
        ]
        let deleteViewModel = try LandingPagesViewModel(client: makeClient())
        deleteViewModel.setActivePage(page)
        await deleteViewModel.deleteSelectedPage()
        if case .error = deleteViewModel.state {} else {
            XCTFail("Expected deleteSelectedPage to surface an error state")
        }
        XCTAssertNotNil(deleteViewModel.errorMessage)

        CannedFeedURLProtocol.handlers = [
            "/api/v1/my/landing-pages/\(page.id)/items": (Data("not-json".utf8), 200)
        ]
        let saveContentViewModel = try LandingPagesViewModel(client: makeClient())
        saveContentViewModel.setActivePage(page)
        await saveContentViewModel.saveContent()
        if case .error = saveContentViewModel.state {} else {
            XCTFail("Expected saveContent to surface an error state")
        }
        XCTAssertFalse(
            saveContentViewModel.errorMessage.map { UiMessages.string($0, locale: .english).isEmpty } ?? true
        )
    }

    private func decodeFixture(_ fixtureId: String) throws -> VouchaFeatures.LandingPageWithItemsResponse {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            VouchaFeatures.LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data(fixtureId)
        )
    }

    private func decodeListFixture() throws -> LandingPagesResponse {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            LandingPagesResponse.self,
            from: ApiFixtureLoader.data("native.landing-pages.default")
        )
    }

    private func makePage(
        id: String,
        title: String,
        subtitle: String?,
        slug: String,
        isDefault: Bool,
        createdAt: Date = Date(timeIntervalSince1970: 1_717_000_000)
    ) -> LandingPage {
        LandingPage(
            id: id,
            userId: "user-abc",
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: createdAt,
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100)
        )
    }

    private func makeDetail(
        id: String,
        title: String,
        subtitle: String?,
        slug: String,
        isDefault: Bool,
        items: [VouchaFeatures.LandingPageItem]
    ) -> LandingPageWithItems {
        LandingPageWithItems(
            id: id,
            userId: "user-abc",
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100),
            items: items
        )
    }

    private func XCTAssertBodyContains(
        _ body: String?,
        _ expected: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertTrue(body?.contains(expected) == true, file: file, line: line)
    }
}
