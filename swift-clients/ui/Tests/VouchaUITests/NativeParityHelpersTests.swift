import Foundation
import ViewInspector
@testable import VouchaCore
@testable import VouchaDesignSystem
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeParityHelpersTests: NativeRouteSurfaceViewModelTestCase {
    func testNativeGenericEntityUsesExpectedFormattingAndHostnameDecoding() throws {
        let entity = NativeGenericEntity(
            id: "entity-1",
            slug: "entity-slug",
            name: "Entity Name",
            title: nil,
            username: nil,
            subject: nil,
            status: nil,
            postType: nil,
            topicType: nil,
            feedType: nil,
            pathname: nil,
            hostname: .object("example.com"),
            url: nil,
            description: "Native description",
            summary: "Native summary"
        )

        XCTAssertEqual(entity.displayTitle(fallback: "fallback"), "Entity Name")
        XCTAssertEqual(entity.displayDetail, "Native description")

        let fallbackEntity = NativeGenericEntity(id: "entity-2")
        XCTAssertEqual(fallbackEntity.displayTitle(fallback: "fallback"), "entity-2")
        XCTAssertEqual(fallbackEntity.displayDetail, "entity-2")

        let decoder = JSONDecoder()
        let stringHostname = try decoder.decode(
            NativeGenericHostname.self,
            from: Data(#""example.org""#.utf8)
        )
        let objectHostname = try decoder.decode(
            NativeGenericHostname.self,
            from: Data(#"{"hostname":"example.net"}"#.utf8)
        )
        XCTAssertEqual(stringHostname.displayName, "example.org")
        XCTAssertEqual(objectHostname.displayName, "example.net")
    }

    func testNativeRouteDestinationSurfaceHelpersCoverQueryParsingAndRouteKinds() throws {
        let webSearch = try NativeRouteDestinationSurface(
            entry: entry(for: .webSearch),
            client: nil,
            routeMatch: nil,
            routeQuery: "q=native%20swift",
            isSignedIn: false,
            showSignIn: {}
        )
        XCTAssertEqual(webSearch.routeSearchQuery, "native swift")
        XCTAssertEqual(webSearch.authLoadTaskId, "web-search||q=native%20swift|false")

        let referrals = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/referrals"))
        let referralLinks = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/referral-links"))
        let referralsSurface = NativeRouteDestinationSurface(
            entry: referrals.entry,
            client: nil,
            routeMatch: referrals.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        let referralLinksSurface = NativeRouteDestinationSurface(
            entry: referralLinks.entry,
            client: nil,
            routeMatch: referralLinks.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertFalse(referralsSurface.shouldLoadRouteSurfaceContent)
        XCTAssertFalse(referralLinksSurface.shouldLoadRouteSurfaceContent)
        XCTAssertNotEqual(
            referralsSurface.authLoadTaskId,
            referralLinksSurface.authLoadTaskId
        )

        let communityBrowseSurface = try NativeRouteDestinationSurface(
            entry: entry(for: .communitiesBrowse),
            client: makeClient(),
            routeMatch: NativeRouteCatalog.matchingRoute(for: "/communities")?.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        let communityDetailRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders"))
        let communityDetailSurface = try NativeRouteDestinationSurface(
            entry: communityDetailRoute.entry,
            client: makeClient(),
            routeMatch: communityDetailRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertFalse(communityBrowseSurface.shouldLoadRouteSurfaceContent)
        XCTAssertFalse(communityDetailSurface.shouldLoadRouteSurfaceContent)

        let compose = try NativeRouteDestinationSurface(
            entry: entry(for: .postCompose),
            client: nil,
            routeMatch: nil,
            routeQuery: "community=builders",
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertEqual(compose.composeCommunityIdOrSlug, "builders")

        let topicRecommendationsCreate = try XCTUnwrap(NativeRouteCatalog
            .matchingRoute(for: "/topic-recommendations/create"))
        let topicRecommendationsEdit = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/topic-1/edit")
        )
        let createSurface = NativeRouteDestinationSurface(
            entry: topicRecommendationsCreate.entry,
            client: nil,
            routeMatch: topicRecommendationsCreate.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        let editSurface = NativeRouteDestinationSurface(
            entry: topicRecommendationsEdit.entry,
            client: nil,
            routeMatch: topicRecommendationsEdit.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertEqual(createSurface.topicRecommendationFormMode, "create")
        XCTAssertEqual(editSurface.topicRecommendationFormMode, "edit")

        let landingPageRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/landing-page/analytics"))
        let landingPages = try NativeRouteDestinationSurface(
            entry: entry(for: .landingPages),
            client: nil,
            routeMatch: landingPageRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertTrue(landingPages.isOwnerLandingPageManagementRoute)
        XCTAssertEqual(landingPages.ownerLandingPageSlug, "analytics")

        XCTAssertNil(
            try NativeRouteDestinationSurface.resolvedClient(
                entry: entry(for: .urlDetail),
                client: nil,
                isSignedIn: false
            )
        )
        XCTAssertNil(
            try NativeRouteDestinationSurface.resolvedClient(
                entry: entry(for: .messages),
                client: makeClient(),
                isSignedIn: false
            )
        )
        XCTAssertNotNil(
            try NativeRouteDestinationSurface.resolvedClient(
                entry: entry(for: .urlDetail),
                client: makeClient(),
                isSignedIn: true
            )
        )

        let signedInMessagesRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        let signedInMessagesSurface = try NativeRouteDestinationSurface(
            entry: signedInMessagesRoute.entry,
            client: makeClient(),
            routeMatch: signedInMessagesRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            currentUserId: "user-1",
            showSignIn: {}
        )
        XCTAssertFalse(signedInMessagesSurface.shouldLoadRouteSurfaceContent)

        let signedOutMessagesSurface = try NativeRouteDestinationSurface(
            entry: signedInMessagesRoute.entry,
            client: makeClient(),
            routeMatch: signedInMessagesRoute.match,
            routeQuery: nil,
            isSignedIn: false,
            showSignIn: {}
        )
        XCTAssertTrue(signedOutMessagesSurface.shouldLoadRouteSurfaceContent)
    }

    func testNativeRouteSurfaceHelpersCoverTagManagementAndPostTypes() throws {
        let tagRoutes: [(String, NativeRouteDestinationIdentifier, NativeTagManagementSubjectKind)] = [
            ("/rss-feed-items/item-1/tags/topic", .rssFeedItemDetail, .rssFeedItem),
            ("/discussion/post-1/tags/topic", .postDetail, .post),
            ("/topic/topic-1/tags/post", .topicDetail, .topic),
            ("/source/source-1/tags/publisher_type", .sourceDetail, .topic)
        ]

        for (path, destination, expectedKind) in tagRoutes {
            let route = NativeRouteMatch(path: path, template: path, params: ["objectType": "topic"])
            let surface = try NativeRouteDestinationSurface(
                entry: entry(for: destination),
                client: nil,
                routeMatch: route,
                routeQuery: nil,
                isSignedIn: true,
                showSignIn: {}
            )

            XCTAssertTrue(surface.isTagManagementRoute)
            XCTAssertEqual(surface.tagManagementSubjectKind, expectedKind)
        }

        let postFeeds = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedPosts),
            client: nil,
            routeMatch: NativeRouteMatch(
                path: "/feed/posts/friends",
                template: "/feed/posts/:scope",
                params: ["scope": "friends"]
            )
        )
        XCTAssertEqual(postFeeds.postFeedType, "follow_users")

        let topicFeeds = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedPosts),
            client: nil,
            routeMatch: NativeRouteMatch(
                path: "/feed/posts/topics",
                template: "/feed/posts/:scope",
                params: ["scope": "topics"]
            )
        )
        XCTAssertEqual(topicFeeds.postFeedType, "follow_topics")

        let rssFeeds = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedNews),
            client: nil,
            routeMatch: NativeRouteMatch(
                path: "/feed/news/sources",
                template: "/feed/news/:scope",
                params: ["scope": "sources"]
            )
        )
        XCTAssertEqual(rssFeeds.rssFeedItemFeedType, "follow_rss_feeds")

        let myPodcasts = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedNews),
            client: nil,
            routeMatch: NativeRouteMatch(path: "/my/podcasts", template: "/my/podcasts")
        )
        XCTAssertEqual(myPodcasts.mySourceFeedType, "podcast")

        let browsePodcasts = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedNews),
            client: nil,
            routeMatch: NativeRouteMatch(
                path: "/podcasts/business",
                template: "/podcasts/:category",
                params: ["category": "business"]
            )
        )
        XCTAssertEqual(browsePodcasts.sourceBrowseFeedType, "podcast")
        XCTAssertEqual(browsePodcasts.sourceBrowseCategory, "business")

        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: "/reviews/create"), .review)
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: "/articles/create"), .article)
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: "/blog/create"), .blogPost)
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: "/data-points/create"), .dataPoint)
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: "/links/create"), .link)
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: nil), .discussion)
    }

    func testNativeRouteSurfaceEntityModelsHydrateAndExposeFirstEntity() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601

        let post = try decoder.decode(
            NativePostSummary.self,
            from: Data(
                """
                {
                  "id": "post-1",
                  "slug": "native-post",
                  "post_type": "review",
                  "title": "Native Post",
                  "markdown": "Body",
                  "created_at": "2026-01-01T00:00:00Z",
                  "created_by_id": "user-1"
                }
                """.utf8
            )
        )
        let feedItem = try decoder.decode(
            NativeRssFeedItemSummary.self,
            from: Data(
                """
                {
                  "id": "item-1",
                  "title": "Native Item",
                  "media_type": "video",
                  "published_at": "2026-01-01T00:00:00Z",
                  "rss_feed": {
                    "id": "feed-1",
                    "title": "Example Feed",
                    "feed_type": "podcast",
                    "rss_feed_url": { "url": "https://example.com/feed.xml" },
                    "hostname": { "hostname": "example.com" }
                  },
                  "data": { "title": "Fallback Item", "link": "https://example.com/item" }
                }
                """.utf8
            )
        )

        let response = NativeGenericListResponse(
            results: [
                .init(id: "post-1"),
                .init(id: "item-1"),
                .init(id: "topic-1")
            ],
            pageInfo: nil,
            posts: ["post-1": post],
            rssFeedItems: ["item-1": feedItem],
            hostnames: ["topic-1": .init(
                id: "topic-1",
                slug: "swift",
                name: "Swift",
                title: nil,
                username: nil,
                subject: nil,
                status: nil,
                postType: nil,
                topicType: "topic",
                feedType: nil,
                pathname: nil,
                hostname: nil,
                url: nil,
                description: nil,
                summary: nil
            )],
            topics: nil,
            communities: nil,
            agents: nil,
            supportThreads: nil,
            users: nil,
            topicElections: nil,
            electionVotes: nil
        )

        XCTAssertEqual(
            response.hydratedEntity(for: NativeGenericEntity(id: "post-1")).displayTitle(fallback: "fallback"),
            "Native Post"
        )
        XCTAssertEqual(response.hydratedEntity(for: NativeGenericEntity(id: "post-1")).displayDetail, "Body")
        XCTAssertEqual(
            response.hydratedEntity(for: NativeGenericEntity(id: "item-1")).displayTitle(fallback: "fallback"),
            "Native Item"
        )
        XCTAssertEqual(response.hydratedEntity(for: NativeGenericEntity(id: "item-1")).displayDetail, "Example Feed")
        XCTAssertEqual(
            response.hydratedEntity(for: NativeGenericEntity(id: "topic-1")).displayTitle(fallback: "fallback"),
            "Swift"
        )

        let envelope = NativeGenericEntityEnvelope(
            topic: nil,
            domain: nil,
            hostname: nil,
            url: nil,
            user: nil,
            community: nil,
            agent: nil,
            supportThread: nil,
            entity: .init(id: "entity-1")
        )
        XCTAssertEqual(envelope.firstEntity?.id, "entity-1")
    }

    func testNativeTagManagementSurfaceSectionsAndSubviewsRenderExpectedStates() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601

        let signedOut = NativeTagManagementSurface(
            client: nil,
            routeMatch: nil,
            subjectKind: .post,
            isSignedIn: false,
            showSignIn: {}
        )
        XCTAssertEqual(try signedOut.inspect().find(text: "Sign in required").string(), "Sign in required")
        XCTAssertNoThrow(try signedOut.inspect().find(button: "Sign In"))

        let surface = NativeTagManagementSurface(
            client: nil,
            routeMatch: nil,
            subjectKind: .post,
            isSignedIn: true,
            showSignIn: {}
        )
        let viewModel = NativeTagManagementViewModel(client: nil, routeMatch: nil, subjectKind: .post)
        viewModel.subjectTitle = .verbatim("Native Post")
        viewModel.subjectDetail = .verbatim("discussion")
        viewModel.tabs = [nativePostTagTabs[0], nativeTopicTagTabs[2]]
        viewModel.activeTab = "topic"

        XCTAssertEqual(
            try surface.header(viewModel: viewModel).inspect().find(text: "Manage tags").string(),
            "Manage tags"
        )
        XCTAssertNoThrow(try NativeTagSearchForm(viewModel: viewModel, config: viewModel.tabs[0]).inspect()
            .find(ViewType.TextField.self))

        viewModel.publisherTypes = try decoder.decode(
            [PublisherTypeTopic].self,
            from: Data(#"[{"id":"publisher-blog","slug":"blog","label":"Blog"}]"#.utf8)
        )
        viewModel.activeTab = "publisher_type"
        XCTAssertNoThrow(try NativeTagPublisherPicker(viewModel: viewModel).inspect().find(ViewType.Picker.self))

        viewModel.relations = []
        XCTAssertEqual(
            try surface.currentTags(viewModel: viewModel).inspect().find(text: "No tags yet").string(),
            "No tags yet"
        )

        let errorView = ErrorStateView(error: VouchaError.api(statusCode: 500, preconditionCode: nil)) {}
        XCTAssertEqual(try errorView.inspect().find(text: "Something went wrong").string(), "Something went wrong")
        XCTAssertEqual(try errorView.inspect().find(text: "An error occurred.").string(), "An error occurred.")
        XCTAssertNoThrow(try errorView.inspect().find(button: "Try Again"))

        viewModel.relations = try [
            decoder.decode(
                EntityRelation.self,
                from: Data(
                    """
                    {
                      "id": "relation-1",
                      "subject_id": "post-1",
                      "object_id": "topic-1",
                      "created_at": "2026-01-01T00:00:00Z",
                      "created_by_id": "user-1",
                      "deleted_at": null,
                      "deleted_by_id": null,
                      "order_index": null,
                      "votes_count_up": 1,
                      "votes_count_down": 0,
                      "votes_score_net": 1,
                      "votes_score_sort": 1,
                      "object_data": {
                        "id": "topic-1",
                        "name": "Swift",
                        "slug": "swift",
                        "topic_type": "topic"
                      }
                    }
                    """.utf8
                )
            )
        ]
        XCTAssertNoThrow(try surface.currentTags(viewModel: viewModel).inspect().find(text: "Swift"))
        XCTAssertNoThrow(try NativeTagSearchResultRow(
            result: .init(
                id: "topic-1",
                slug: nil,
                name: "Swift",
                title: nil,
                username: nil,
                subject: nil,
                status: nil,
                postType: nil,
                topicType: "topic",
                feedType: nil,
                pathname: nil,
                hostname: nil,
                url: nil,
                description: nil,
                summary: nil
            )
        ).inspect().find(text: "Swift"))
    }
}
