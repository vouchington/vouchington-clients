import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class LandingPagesViewModelRegressionTests: NativeRouteSurfaceViewModelTestCase {
    func testAddLinkItemRejectsNonHttpUrlsAndFragments() throws {
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.linkLabel = "Newsletter"

        viewModel.linkUrl = "httpx://example.com"
        viewModel.addLinkItem()
        XCTAssertTrue(viewModel.draftItems.isEmpty)

        viewModel.linkUrl = "https://example.com/#section"
        viewModel.addLinkItem()
        XCTAssertTrue(viewModel.draftItems.isEmpty)

        viewModel.linkUrl = "https://example.com/path"
        viewModel.addLinkItem()

        XCTAssertEqual(viewModel.draftItems.count, 1)
        guard case let .link(label, url) = viewModel.draftItems.first?.input else {
            return XCTFail("Expected a link input")
        }
        XCTAssertEqual(label, "Newsletter")
        XCTAssertEqual(url, "https://example.com/path")
        XCTAssertTrue(viewModel.linkLabel.isEmpty)
        XCTAssertTrue(viewModel.linkUrl.isEmpty)
    }

    func testAddLinkItemEnforcesBackendLengthLimits() throws {
        let viewModel = try LandingPagesViewModel(client: makeClient())

        viewModel.linkLabel = String(repeating: "a", count: 100)
        viewModel.linkUrl = makeHttpsUrl(totalLength: 2_048)
        viewModel.addLinkItem()

        XCTAssertEqual(viewModel.draftItems.count, 1)
        XCTAssertTrue(viewModel.linkLabel.isEmpty)
        XCTAssertTrue(viewModel.linkUrl.isEmpty)

        viewModel.linkLabel = String(repeating: "b", count: 101)
        viewModel.linkUrl = makeHttpsUrl(totalLength: 2_048)
        viewModel.addLinkItem()
        XCTAssertEqual(viewModel.draftItems.count, 1)

        viewModel.linkLabel = String(repeating: "c", count: 100)
        viewModel.linkUrl = makeHttpsUrl(totalLength: 2_049)
        viewModel.addLinkItem()
        XCTAssertEqual(viewModel.draftItems.count, 1)
    }

    func testSaveDetailsUpdatesRouteSlugBeforeReloading() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                """
                {
                  "landing_page": {
                    "id": "landing-page-2",
                    "user_id": "user-abc",
                    "title": "Travel Stack",
                    "subtitle": null,
                    "slug": "travel-guide",
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
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            Data(
                """
                {
                  "results": [
                    {
                      "id": "landing-page-2",
                      "user_id": "user-abc",
                      "title": "Travel Stack",
                      "subtitle": null,
                      "slug": "travel-guide",
                      "is_default": false,
                      "created_at": "2026-06-28T11:00:00Z",
                      "updated_at": "2026-06-29T11:00:00Z"
                    }
                  ],
                  "page_info": { "has_next_page": false, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient(), initialSlug: "travel")
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
        viewModel.slug = "travel-guide"

        await viewModel.saveDetails()
        await viewModel.reload()

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
        XCTAssertEqual(viewModel.selectedPage?.slug, "travel-guide")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/landing-pages/landing-page-2"
        })
    }

    func testCreatePageUpdatesRouteSlugBeforeReloading() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/landing-pages"] = [
            (
                Data(
                    """
                    {
                      "landing_page": {
                        "id": "landing-page-2",
                        "user_id": "user-abc",
                        "title": "Travel Stack",
                        "subtitle": null,
                        "slug": "travel-guide",
                        "is_default": false,
                        "created_at": "2026-06-28T11:00:00Z",
                        "updated_at": "2026-06-29T11:00:00Z"
                      }
                    }
                    """.utf8
                ),
                201,
                0
            ),
            (
                Data(
                    """
                    {
                      "results": [
                        {
                          "id": "landing-page-2",
                          "user_id": "user-abc",
                          "title": "Travel Stack",
                          "subtitle": null,
                          "slug": "travel-guide",
                          "is_default": false,
                          "created_at": "2026-06-28T11:00:00Z",
                          "updated_at": "2026-06-29T11:00:00Z"
                        }
                      ],
                      "page_info": { "has_next_page": false, "end_cursor": null }
                    }
                    """.utf8
                ),
                200,
                0
            )
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                """
                {
                  "landing_page": {
                    "id": "landing-page-2",
                    "user_id": "user-abc",
                    "title": "Travel Stack",
                    "subtitle": null,
                    "slug": "travel-guide",
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
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient(), initialSlug: "travel")

        await viewModel.createPage(title: "Travel Stack", slug: "travel-guide", subtitle: nil)
        await viewModel.reload()

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-2")
        XCTAssertEqual(viewModel.selectedPage?.slug, "travel-guide")
    }

    func testSaveContentPreservesDraftMetadataFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2/items"] = (
            Data(
                """
                {"landing_page":{"id":"landing-page-2","user_id":"user-abc","title":"Server Title","subtitle":"Server Subtitle","slug":"server-slug","is_default":false,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z","items":[{"id":"draft-link-1","type":"link","label":"Newsletter","url":"https://example.com/newsletter"}]}}
                """.utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(
            makeDetail(
                id: "landing-page-2",
                title: "Travel Stack",
                subtitle: nil,
                slug: "travel",
                isDefault: false,
                items: [.link(id: "draft-link-1", label: "Draft Link", url: "https://example.com/draft")]
            )
        )
        viewModel.title = "Draft Title"
        viewModel.subtitle = "Draft Subtitle"
        viewModel.slug = "draft-slug"

        await viewModel.saveContent()

        XCTAssertEqual(viewModel.title, "Draft Title")
        XCTAssertEqual(viewModel.subtitle, "Draft Subtitle")
        XCTAssertEqual(viewModel.slug, "draft-slug")
        XCTAssertEqual(viewModel.selectedPage?.title, "Draft Title")
        XCTAssertEqual(viewModel.selectedPage?.subtitle, "Draft Subtitle")
        XCTAssertEqual(viewModel.selectedPage?.slug, "draft-slug")
        XCTAssertEqual(viewModel.draftItems, viewModel.selectedPage?.items)
    }

    func testSetDefaultPreservesDraftMetadataFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                """
                {"landing_page":{"id":"landing-page-2","user_id":"user-abc","title":"Server Title","subtitle":"Server Subtitle","slug":"server-slug","is_default":true,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z"}}
                """.utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.pages = [
            LandingPage(
                id: "landing-page-2",
                userId: "user-abc",
                title: "Travel Stack",
                subtitle: nil,
                slug: "travel",
                isDefault: false,
                createdAt: Date(timeIntervalSince1970: 1_717_000_000),
                updatedAt: Date(timeIntervalSince1970: 1_717_000_100)
            )
        ]
        viewModel.setActivePage(
            makeDetail(
                id: "landing-page-2",
                title: "Travel Stack",
                subtitle: nil,
                slug: "travel",
                isDefault: false,
                items: [.link(id: "draft-link-1", label: "Draft Link", url: "https://example.com/draft")]
            )
        )
        viewModel.title = "Draft Title"
        viewModel.subtitle = "Draft Subtitle"
        viewModel.slug = "draft-slug"

        await viewModel.setDefault()

        XCTAssertEqual(viewModel.title, "Draft Title")
        XCTAssertEqual(viewModel.subtitle, "Draft Subtitle")
        XCTAssertEqual(viewModel.slug, "draft-slug")
        XCTAssertEqual(viewModel.selectedPage?.title, "Draft Title")
        XCTAssertEqual(viewModel.selectedPage?.subtitle, "Draft Subtitle")
        XCTAssertEqual(viewModel.selectedPage?.slug, "draft-slug")
        XCTAssertTrue(viewModel.selectedPage?.isDefault == true)
        XCTAssertEqual(viewModel.draftItems, viewModel.selectedPage?.items)
    }

    func testSelectPageIgnoresSelectionWhileLoading() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/landing-pages/landing-page-1"] = [
            (ApiFixtureLoader.data("native.landing-page-detail.default"), 200, 0.05)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-2"] = (
            Data(
                """
                {"landing_page":{"id":"landing-page-2","user_id":"user-abc","title":"Travel Stack","subtitle":null,"slug":"travel","is_default":false,"created_at":"2026-06-28T11:00:00Z","updated_at":"2026-06-29T11:00:00Z","items":[]}}
                """.utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())

        let firstSelection = Task { await viewModel.selectPage(id: "landing-page-1") }
        while !viewModel.isLoading {
            await Task.yield()
        }
        await viewModel.selectPage(id: "landing-page-2")
        await firstSelection.value

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-1")
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/landing-pages/landing-page-2"
        })
    }

    func testDeleteClearsSelectionWhenRefreshFailsAfterDelete() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (Data(), 204)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (Data(#"{}"#.utf8), 500)
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(
            makeDetail(
                id: "landing-page-1",
                title: "Featured Links",
                subtitle: "Draft links",
                slug: "featured",
                isDefault: true,
                items: [.link(id: "item-link-1", label: "Newsletter", url: "https://example.com/newsletter")]
            )
        )

        await viewModel.deleteSelectedPage()

        XCTAssertNil(viewModel.selectedPage)
        XCTAssertTrue(viewModel.draftItems.isEmpty)
        XCTAssertEqual(viewModel.title, "")
        XCTAssertEqual(viewModel.slug, "")
        if case .error = viewModel.state {} else {
            XCTFail("Expected refresh failure to surface an error state")
        }
    }

    func testLoadClearsStaleSelectionWhenRefreshedPagesNoLongerContainIt() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(
            makeDetail(
                id: "missing-page",
                title: "Missing",
                subtitle: nil,
                slug: "missing",
                isDefault: false,
                items: [.link(id: "item-link-1", label: "Newsletter", url: "https://example.com/newsletter")]
            )
        )

        await viewModel.load()

        XCTAssertNil(viewModel.selectedPage)
        XCTAssertTrue(viewModel.draftItems.isEmpty)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/landing-pages/missing-page"
        })
    }

    func testLoadRefreshesSelectedDetailWhenSelectionStillExists() async throws {
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
                    "title": "Fresh Travel Stack",
                    "subtitle": "Updated",
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
        viewModel.setActivePage(
            makeDetail(
                id: "landing-page-2",
                title: "Stale Travel Stack",
                subtitle: nil,
                slug: "travel",
                isDefault: false,
                items: [.link(id: "item-link-1", label: "Old", url: "https://example.com/old")]
            )
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedPage?.title, "Fresh Travel Stack")
        XCTAssertEqual(viewModel.subtitle, "Updated")
        XCTAssertTrue(viewModel.draftItems.isEmpty)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/landing-pages/landing-page-2"
        })
    }

    func testLoadIgnoresReentryWhileLoading() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/landing-pages"] = [
            (ApiFixtureLoader.data("native.landing-pages.default"), 200, 0.05)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())

        let firstLoad = Task { await viewModel.load() }
        while !viewModel.isLoading {
            await Task.yield()
        }

        await viewModel.load()
        await firstLoad.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/landing-pages" }.count,
            1
        )
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/landing-pages/candidates" }.count,
            1
        )
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/landing-pages/landing-page-1" }.count,
            1
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

    private func makeHttpsUrl(totalLength: Int) -> String {
        let prefix = "https://example.com/"
        precondition(totalLength >= prefix.utf16.count)
        return prefix + String(repeating: "a", count: totalLength - prefix.utf16.count)
    }
}
