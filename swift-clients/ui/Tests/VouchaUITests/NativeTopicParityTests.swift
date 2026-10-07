import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeTopicParityTests: NativeRouteSurfaceViewModelTestCase {
    func testTopicCreateRouteMapsToNativeManagementDestination() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create"))

        XCTAssertEqual(route.entry.destinationIdentifier, .topicManagement)
        XCTAssertEqual(route.match.path, "/topics/create")
    }

    func testTopicSettingsRoutesMapToNativeManagementDestination() throws {
        for path in [
            "/topic/topic-123/settings/about",
            "/topic/topic-123/settings/behavior",
            "/topic/topic-123/settings/domains",
            "/topic/topic-123/settings/source",
            "/topic/topic-123/settings/aliases",
            "/topic/topic-123/settings/merge"
        ] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path), "Missing route for \(path)")
            XCTAssertEqual(route.entry.destinationIdentifier, .topicManagement)
            XCTAssertEqual(route.match.param("idOrSlug"), "topic-123")
        }

        XCTAssertNil(NativeRouteCatalog.matchingRoute(for: "/topics/aliases")?.entry.destinationIdentifier)
        let tagRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-123/tags/post"))
        XCTAssertEqual(tagRoute.entry.destinationIdentifier, .topicDetail)
        XCTAssertEqual(tagRoute.match.param("idOrSlug"), "topic-123")
        XCTAssertNil(NativeRouteCatalog.matchingRoute(for: "/topic/topic-123/settings/validations")?.entry
            .destinationIdentifier)
    }

    func testTopicManagementGroupIsAdminOnly() {
        XCTAssertFalse(
            AppSection.topics.nativeParityGroups(isSignedIn: true, userRoles: [])
                .flatMap(\.entries)
                .compactMap(\.destinationIdentifier)
                .contains(.topicManagement)
        )
        XCTAssertTrue(
            AppSection.topics.nativeParityGroups(isSignedIn: true, userRoles: ["administrator"])
                .flatMap(\.entries)
                .compactMap(\.destinationIdentifier)
                .contains(.topicManagement)
        )
    }

    func testTopicManagementSurfaceRendersCreateAndSettingsSections() throws {
        let createMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create")?.match)
        let create = NativeTopicManagementSurface(client: nil, routeMatch: createMatch)

        XCTAssertEqual(try create.inspect().find(text: "Create Topic").string(), "Create Topic")
        XCTAssertNoThrow(try create.inspect().find(button: "Create Topic"))
        XCTAssertNoThrow(try create.inspect().find(text: "Logo image"))
        XCTAssertNoThrow(try create.inspect().find(text: "Hero image"))

        for (path, expectedText) in [
            ("/topic/topic-123/settings/behavior", "Topic type"),
            ("/topic/topic-123/settings/domains", "Primary hostname"),
            ("/topic/topic-123/settings/source", "No source found"),
            ("/topic/topic-123/settings/aliases", "No aliases"),
            ("/topic/topic-123/settings/merge", "Destination topic id or slug")
        ] {
            let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path)?.match)
            let sut = NativeTopicManagementSurface(client: nil, routeMatch: match)
            XCTAssertNoThrow(try sut.inspect().find(text: expectedText), "Expected \(expectedText) in \(path)")
        }
    }

    func testTopicManagementDestinationViewRendersNativeSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create"))
        let entry = try entry(for: .topicManagement)
        let sut = NativeRouteDestinationView(
            entry: entry,
            routeMatch: route.match
        )

        XCTAssertEqual(try sut.inspect().find(text: "Create Topic").string(), "Create Topic")
        XCTAssertNoThrow(try sut.inspect().find(button: "Create Topic"))
    }

    func testTopicManagementStatusRendersEachLoadState() throws {
        let surface = NativeTopicManagementSurface(client: nil, routeMatch: nil)
        let viewModel = NativeTopicManagementViewModel(client: nil, routeMatch: nil)

        viewModel.state = .loading
        XCTAssertNoThrow(try surface.status(viewModel: viewModel).inspect().find(ViewType.ProgressView.self))

        viewModel.state = .error(.api(statusCode: 500, preconditionCode: nil))
        XCTAssertNoThrow(try surface.status(viewModel: viewModel).inspect().find(ViewType.Text.self))

        viewModel.state = .loaded
        try viewModel.apply(topic: Self.topicModel(name: "Managed Topic"))
        XCTAssertEqual(try surface.status(viewModel: viewModel).inspect().find(text: "topic-1").string(), "topic-1")
    }

    func testTopicImageFieldRendersEmptyAndPopulatedStates() throws {
        let empty = NativeTopicImageField(
            title: .nativeSwiftTopicManagementFieldsLogoImage,
            previewWidth: 64,
            imageId: .constant(""),
            client: nil
        )

        XCTAssertEqual(try empty.inspect().find(text: "Logo image").string(), "Logo image")
        XCTAssertNoThrow(try empty.inspect().find(button: "Upload"))
        XCTAssertThrowsError(try empty.inspect().find(button: "Remove"))

        let populated = NativeTopicImageField(
            title: .nativeSwiftTopicManagementFieldsHeroImage,
            previewWidth: 96,
            imageId: .constant("hero-1"),
            client: nil
        )

        XCTAssertEqual(try populated.inspect().find(text: "Hero image").string(), "Hero image")
        XCTAssertNoThrow(try populated.inspect().find(button: "Replace"))
        XCTAssertNoThrow(try populated.inspect().find(button: "Remove"))
    }

    func testTopicManagementFieldsRenderPopulatedStates() throws {
        let viewModel = NativeTopicManagementViewModel(client: nil, routeMatch: nil)
        try viewModel.apply(topic: Self.topicModel(name: "Managed Topic"))
        viewModel.additionalHostname = "news.example.com"
        viewModel.additionalHostnames = [
            TopicAdditionalHostname(id: "host-1", hostname: "news.example.com", topicId: "topic-1")
        ]
        viewModel.aliasDraft = "alias"
        viewModel.aliasesPagination.reset(items: [
            TopicAlias(id: "alias-primary", alias: "primary", topicId: "topic-1"),
            TopicAlias(id: "alias-secondary", alias: "secondary", topicId: "topic-1")
        ])

        XCTAssertNoThrow(try NativeTopicManagementAboutFields(viewModel: viewModel, client: nil)
            .inspect().find(text: "Logo image"))
        XCTAssertNoThrow(try NativeTopicManagementBehaviorFields(viewModel: viewModel)
            .inspect().find(text: "No index"))

        let domains = NativeTopicManagementDomainFields(viewModel: viewModel)
        XCTAssertNoThrow(try domains.inspect().find(button: "Add Hostname"))
        XCTAssertEqual(try domains.inspect().find(text: "news.example.com").string(), "news.example.com")

        let aliases = NativeTopicManagementAliasFields(viewModel: viewModel)
        XCTAssertNoThrow(try aliases.inspect().find(button: "Add Alias"))
        XCTAssertEqual(try aliases.inspect().find(text: "secondary").string(), "secondary")

        let merge = NativeTopicManagementMergeFields(viewModel: viewModel)
        XCTAssertNoThrow(try merge.inspect().find(text: "Destination topic id or slug"))
    }

    func testTopicManagementAliasFieldsHideDeleteActionForActiveTopicSlug() throws {
        let viewModel = NativeTopicManagementViewModel(client: nil, routeMatch: nil)
        try viewModel.apply(topic: Self.topicModel(name: "Managed Topic"))
        XCTAssertEqual(viewModel.topic?.slug, "topic-1")
        viewModel.aliasesPagination.reset(items: [
            TopicAlias(id: "alias-active", alias: "topic-1", topicId: "topic-1"),
            TopicAlias(id: "alias-secondary", alias: "secondary", topicId: "topic-1")
        ])

        let fields = NativeTopicManagementAliasFields(viewModel: viewModel)
        XCTAssertFalse(fields.canRemove(viewModel.aliases[0]))
        XCTAssertTrue(fields.canRemove(viewModel.aliases[1]))
    }

    func testTopicManagementAliasFieldsRenderEmptyStateAndDisableBlankDraft() throws {
        let viewModel = NativeTopicManagementViewModel(client: nil, routeMatch: nil)
        let emptyFields = NativeTopicManagementAliasFields(viewModel: viewModel)

        XCTAssertNoThrow(try emptyFields.inspect().find(text: "No aliases"))
        XCTAssertTrue(try emptyFields.inspect().find(button: "Add Alias").isDisabled())
    }

    func testTopicManagementSourceFieldsRenderLoadedSource() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1"] = (Self.topicEnvelope(name: "Source Topic"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            Data(
                #"{"results":[{"id":"feed-1","title":"Feed","feed_type":"article","rss_feed_url":{"url":"https://example.com/feed.xml","canonical_url_id":null},"hostname":{"hostname":"example.com"},"etag":null,"home_page_url":null,"last_modified_at":null,"topic":null,"publisher_type":null,"podcast_show":null}]}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/feed-1"] = (
            Self.rssFeedDetail(enabled: true, discoverable: true),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/source")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match)

        await viewModel.load()

        let sut = NativeTopicManagementSourceFields(viewModel: viewModel)
        XCTAssertEqual(try sut.inspect().find(text: "Feed").string(), "Feed")
        XCTAssertEqual(
            try sut.inspect().find(text: "https://example.com/feed.xml").string(),
            "https://example.com/feed.xml"
        )
        XCTAssertEqual(
            try sut.inspect().find(text: "https://example.com").string(),
            "https://example.com"
        )
        XCTAssertNoThrow(try sut.inspect().find(button: "Disable"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Hide from discovery"))
    }

    func testTopicManagementViewModelCreateAndUpdateBodiesTrimAndNullFields() throws {
        let create = NativeTopicManagementViewModel(client: nil, routeMatch: nil)
        create.name = " Created "
        create.slug = " created "
        create.topicType = " "
        create.markdown = " Markdown "
        create.hostname = " "

        XCTAssertEqual(create.createBody.name, "Created")
        XCTAssertEqual(create.createBody.slug, "created")
        XCTAssertNil(create.createBody.topicType)
        XCTAssertEqual(create.createBody.markdown, "Markdown")
        XCTAssertNil(create.createBody.hostname)

        let update = NativeTopicManagementViewModel(client: nil, routeMatch: nil)
        update.name = " "
        update.slug = " topic "
        update.markdown = " "
        update.topicType = " rss_feed "
        update.hostname = ""
        update.logoImageId = " logo-1 "
        update.heroImageId = ""
        update.noindex = true
        update.allowReviews = true

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let body = try String(data: encoder.encode(update.updateBody), encoding: .utf8)

        XCTAssertTrue(body?.contains(#""slug":"topic""#) == true)
        XCTAssertTrue(body?.contains(#""topic_type":"rss_feed""#) == true)
        XCTAssertTrue(body?.contains(#""hostname":null"#) == true)
        XCTAssertTrue(body?.contains(#""logo_image_id":"logo-1""#) == true)
        XCTAssertTrue(body?.contains(#""hero_image_id":null"#) == true)
        XCTAssertTrue(body?.contains(#""is_noindexed":true"#) == true)
        XCTAssertTrue(body?.contains(#""should_allow_reviews":true"#) == true)
    }

    func testTopicManagementViewModelCreateLoadAndGuardBranches() async throws {
        let createMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create")?.match)
        let create = NativeTopicManagementViewModel(client: nil, routeMatch: createMatch)

        await create.load()
        await create.save()
        await create.addAlias()
        await create.addAdditionalHostname()
        await create.removeAlias(TopicAlias(id: "alias-1", alias: "alias", topicId: "topic-1"))
        await create.removeAdditionalHostname("host-1")
        await create.updateSource(enabled: true)

        XCTAssertFalse(create.isLoading)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)

        create.sourceDetail = try Self.rssFeedDetailModel(enabled: false, discoverable: false)
        await create.toggleSourceEnabled()
        await create.toggleSourceDiscoverable()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testTopicAdditionalHostnameInitializerProvidesStableId() {
        let createdAt = Date(timeIntervalSince1970: 1_764_195_200)
        let hostname = TopicAdditionalHostname(
            id: "host-1",
            hostname: "example.com",
            topicId: "topic-1",
            createdAt: createdAt
        )

        XCTAssertEqual(hostname.id, "host-1")
        XCTAssertEqual(hostname.hostnameId, "host-1")
        XCTAssertEqual(hostname.hostname, "example.com")
        XCTAssertEqual(hostname.topicId, "topic-1")
        XCTAssertEqual(hostname.createdAt, createdAt)
    }

    func testTopicManagementViewModelMergesTopicAliases() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/merges"] = (Self.topicEnvelope(name: "Merged"), 200)
        let mergeMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/merge")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: mergeMatch)
        viewModel.destinationIdOrSlug = " topic-2 "

        await viewModel.save()

        XCTAssertEqual(viewModel.topic?.name, "Merged")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topics/topic-1/merges")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains("destination_id_or_slug") == true)
    }

    func testTopicManagementViewModelCreatesAndUpdatesTopic() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (Self.topicEnvelope(name: "Created"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1"] = (Self.topicEnvelope(name: "Updated"), 200)

        let createMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topics/create")?.match)
        let create = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: createMatch)
        create.name = " Created "
        create.slug = " created "
        create.topicType = " rss_feed "
        create.markdown = " Body "
        create.hostname = " example.com "

        await create.save()

        XCTAssertEqual(create.topic?.name, "Updated")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "PATCH"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topics")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains(#""name":"Created""#) == true)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.last??.contains(#""markdown":"Body""#) == true)

        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
        let updateMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/about")?.match)
        let update = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: updateMatch)
        try update.apply(topic: Self.topicModel(name: "Original"))
        update.hostname = ""
        update.logoImageId = ""
        update.heroImageId = " hero-1 "
        update.noindex = true
        update.allowReviews = true

        await update.save()

        XCTAssertEqual(update.topic?.name, "Updated")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.first, "PATCH")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topics/topic-1")
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.first ?? nil)
        XCTAssertTrue(body.contains(#""hostname":null"#))
        XCTAssertTrue(body.contains(#""logo_image_id":null"#))
        XCTAssertTrue(body.contains(#""hero_image_id":"hero-1""#))
        XCTAssertTrue(body.contains(#""is_noindexed":true"#))
        XCTAssertTrue(body.contains(#""should_allow_reviews":true"#))
    }

    func testTopicManagementViewModelManagesAliasesAndHostnames() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/aliases"] = (
            Data(
                #"{"results":[{"id":"alias-primary","alias":"primary","topic_id":"topic-1"},{"id":"alias-alt","alias":"alt","topic_id":"topic-1"}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/aliases/alias-alt"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/additional-hostnames"] = (
            Data(
                #"""
                {"results":[{"hostname_id":"host-1","hostname":"example.com","topic_id":"topic-1"}],
                "page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
                """#.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/additional-hostnames/host-1"] = (Data("{}".utf8), 200)
        let aliasMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/aliases")?.match)
        let aliases = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: aliasMatch)
        aliases.aliasDraft = " alt "

        await aliases.addAlias()
        try await aliases.removeAlias(XCTUnwrap(aliases.aliases.first { $0.alias == "alt" }))

        XCTAssertEqual(aliases.aliasDraft, "")
        XCTAssertEqual(aliases.aliases.map(\.alias), ["primary", "alt"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.prefix(3), ["POST", "GET", "DELETE"])

        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        let domainMatch = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/domains")?.match)
        let domains = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: domainMatch)
        domains.additionalHostname = " example.com "

        await domains.addAdditionalHostname()
        await domains.removeAdditionalHostname("host-1")

        XCTAssertEqual(domains.additionalHostname, "")
        XCTAssertEqual(domains.additionalHostnames.first?.hostname, "example.com")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.prefix(3), ["POST", "GET", "DELETE"])
    }

    func testTopicManagementViewModelLoadsAndTogglesSource() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1"] = (Self.topicEnvelope(name: "Source Topic"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            Data(
                #"{"results":[{"id":"feed-1","title":"Feed","feed_type":"article","rss_feed_url":{"url":"https://example.com/feed.xml"},"hostname":{"hostname":"example.com"}}]}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/feed-1"] = (
            Self.rssFeedDetail(enabled: true, discoverable: false),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/topic-1/settings/source")?.match)
        let viewModel = try NativeTopicManagementViewModel(client: makeClient(), routeMatch: match)

        await viewModel.load()
        await viewModel.toggleSourceEnabled()
        await viewModel.toggleSourceDiscoverable()

        XCTAssertEqual(viewModel.topic?.name, "Source Topic")
        XCTAssertEqual(viewModel.sourceDetail?.id, "feed-1")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/rss-feeds" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.query?.contains("enabled=null") == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/rss-feeds/feed-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""enabled":false"#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""discoverable":true"#) == true })
    }

    private static func topicEnvelope(name: String) -> Data {
        let topic = String(data: topicJson(name: name), encoding: .utf8)!
        return Data(#"{"topic":\#(topic)}"#.utf8)
    }

    private static func topicJson(name: String) -> Data {
        Data("""
        {
          "id": "topic-1",
          "name": "\(name)",
          "slug": "topic-1",
          "markdown": "Body",
          "topic_type": "rss_feed",
          "aliases": ["alias"],
          "should_allow_reviews": true,
          "is_noindexed": false,
          "hostname_id": "hostname-1",
          "hostname": { "id": "hostname-1", "hostname": "example.com", "topic_id": "topic-1" },
          "logo_image_id": "logo-1",
          "hero_image_id": "hero-1",
          "homepage_url_id": null,
          "lingua_rs_detected_language": null,
          "referral_program_id": null,
          "referral_program_slug": null,
          "rewards_program_id": null,
          "created_at": "2026-01-01T00:00:00Z"
        }
        """.utf8)
    }

    private static func topicModel(name: String) throws -> Topic {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(Topic.self, from: topicJson(name: name))
    }

    private static func rssFeedDetail(enabled: Bool, discoverable: Bool) -> Data {
        Data("""
        {
            "rss_feed": {
            "id": "feed-1",
            "title": "Feed",
            "rss_feed_url": { "url": "https://example.com/feed.xml" },
            "home_page_url": { "url": "https://example.com" },
            "is_enabled": \(enabled),
            "is_discoverable": \(discoverable),
            "last_fetched_at": null,
            "etag": null,
            "last_modified_at": null,
            "topic_id": "topic-1",
            "publisher_type": { "id": "topic-1", "name": "Topic", "slug": "topic", "topic_type": "rss_feed" }
          },
          "latest_crawl": null,
          "can_view_latest_crawl": true
        }
        """.utf8)
    }

    private static func rssFeedDetailModel(enabled: Bool, discoverable: Bool) throws -> RssFeedDetail {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(RssFeedDetailResponse.self, from: rssFeedDetail(
            enabled: enabled,
            discoverable: discoverable
        )).rssFeed
    }

}
