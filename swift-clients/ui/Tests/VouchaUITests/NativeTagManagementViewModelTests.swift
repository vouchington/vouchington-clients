import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeTagManagementViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testSourceTagManagementLoadsPublisherTypesAndCurrentRelations() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/source-1"] = (sourceDetailData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/publisher-types"] = (publisherTypesData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/entity-relations/topic/source-1/publisher_type/topic"] = (
            entityRelationsData,
            200
        )

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/source-1/tags/publisher_type"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .topic,
            subjectId: "source-1",
            subjectTitle: "Source One"
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.subjectId, "source-1")
        XCTAssertEqual(uiEnglish(viewModel.subjectTitle), "Source One")
        XCTAssertEqual(
            viewModel.tabs.map(\.value),
            ["topic", "category", "publisher_type", "post", "landing_page", "terms_of_service"]
        )
        XCTAssertEqual(viewModel.publisherTypes.first?.slug, "blog")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/topics/publisher-types" }.count,
            1
        )
        XCTAssertEqual(viewModel.relations.first?.objectData.displayTitle, "Blog")
    }

    func testPostTagManagementSearchesAddsAndVotesOnRelations() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-1"] = (postDetailData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (topicSearchData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (postSearchData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/post/post-1/category/topic"] = [
            (emptyRelationsData, 200, 0),
            (entityRelationResponseData, 201, 0),
            (relationData(choice: .confirm), 200, 0),
            (relationData(choice: .dispute), 200, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/discussion/post-1/tags/topic"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .post,
            subjectId: "post-1",
            subjectTitle: "Native Post"
        )

        await viewModel.load()
        viewModel.searchQuery = "swift"
        await viewModel.search()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/topics" }, true)
        XCTAssertEqual(viewModel.searchResults.first?.displayTitle(fallback: "missing"), "Swift")

        viewModel.activeTab = "post"
        viewModel.searchQuery = "native"
        await viewModel.search()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/posts" }, true)
        if case let .error(error) = viewModel.searchState {
            XCTFail("Expected post search to load, got \(error)")
        }
        XCTAssertEqual(viewModel.searchResults.first?.id, "post-2")
        XCTAssertEqual(viewModel.searchResults.first?.displayTitle(fallback: "missing"), "Related Native Post")

        viewModel.clearSearch()
        XCTAssertEqual(viewModel.searchQuery, "")
        XCTAssertTrue(viewModel.searchResults.isEmpty)
        viewModel.activeTab = "topic"

        await viewModel.addSelectedResult(id: "topic-2")
        await viewModel.vote(relationId: "relation-1", choice: .dispute)

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/entity-relations/post/post-1/category/topic" })
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.contains(#"{"objectId":"topic-2"}"#), true)
        XCTAssertEqual(viewModel.relations.first?.objectData.displayTitle, "Swift")
    }

    func testRssFeedItemSubjectAndSearchGuardsCoverLocalBranches() async throws {
        let rssRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/rss-feed-items/item-1/tags/topic"))
        let rssViewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: rssRoute.match,
            subjectKind: .rssFeedItem
        )

        try await rssViewModel.loadSubject(client: makeClient())
        try await rssViewModel.loadTabs(client: makeClient())

        XCTAssertEqual(rssViewModel.subjectEntityType, "rss_feed_item")
        XCTAssertEqual(rssViewModel.subjectId, "item-1")
        XCTAssertEqual(uiEnglish(rssViewModel.subjectTitle), "RSS item")
        XCTAssertEqual(uiEnglish(rssViewModel.subjectDetail), "Categories")
        XCTAssertEqual(rssViewModel.tabs.map(\.value), ["topic"])
        XCTAssertEqual(rssViewModel.activeTab, "topic")
        XCTAssertEqual(rssViewModel.activeTabConfig?.objectType, "topic")

        let urlViewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: nil,
            subjectKind: .post,
            subjectId: "post-1"
        )
        urlViewModel.tabs = [
            NativeTagRelationTab(
                label: UiMessage(.nativeSwiftTagManagementRelatedLinks),
                value: "url",
                predicate: "related",
                objectType: "url"
            )
        ]
        urlViewModel.activeTab = "url"
        urlViewModel.searchQuery = "ab"

        await urlViewModel.search()

        XCTAssertTrue(urlViewModel.searchResults.isEmpty)
        if case .idle = urlViewModel.searchState {
        } else {
            XCTFail("Expected URL search guard to reset search state")
        }

        let publisherViewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: nil,
            subjectKind: .topic,
            subjectId: "topic-1"
        )
        publisherViewModel.tabs = [
            NativeTagRelationTab(
                label: UiMessage(.nativeSwiftTagManagementPublisherType),
                value: "publisher_type",
                predicate: "publisher_type",
                objectType: "topic"
            )
        ]
        publisherViewModel.activeTab = "publisher_type"
        publisherViewModel.searchQuery = "native"

        await publisherViewModel.search()

        XCTAssertTrue(publisherViewModel.searchResults.isEmpty)
        if case .idle = publisherViewModel.searchState {
        } else {
            XCTFail("Expected publisher type search to reset state")
        }
    }

    func testAddSelectedPublisherTypeReloadsRelationsAndClearsSelection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/source-1"] = (sourceDetailData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/publisher-types"] = (publisherTypesData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/topic/source-1/publisher_type/topic"] = [
            (publisherRelationData, 200, 0),
            (entityRelationResponseData, 201, 0),
            (publisherRelationData, 200, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/source-1/tags/publisher_type"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .topic,
            subjectId: "source-1",
            subjectTitle: "Source One"
        )

        await viewModel.load()
        viewModel.publisherTypeSelection = "publisher-blog"
        await viewModel.addSelectedPublisherType(id: "publisher-blog")

        XCTAssertEqual(viewModel.publisherTypeSelection, "")
        XCTAssertEqual(viewModel.relations.first?.objectData.displayTitle, "Blog")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains(#"{"objectId":"publisher-blog"}"#))
    }

    func testAddSelectedPublisherTypePreservesSpecificErrorAndResetsSelection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/source-1"] = (sourceDetailData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/publisher-types"] = (publisherTypesData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/topic/source-1/publisher_type/topic"] = [
            (entityRelationsData, 200, 0),
            (Data("{}".utf8), 200, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/source-1/tags/publisher_type"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .topic,
            subjectId: "source-1",
            subjectTitle: "Source One"
        )

        await viewModel.load()
        viewModel.publisherTypeSelection = "publisher-blog"
        await viewModel.addSelectedPublisherType(id: "publisher-blog")

        if case let .error(error) = viewModel.state {
            if case .decodingFailed = error {
            } else {
                XCTFail("Expected decodingFailed error, got \(error)")
            }
        } else {
            XCTFail("Expected error state")
        }
        XCTAssertEqual(viewModel.publisherTypeSelection, "")
    }

    func testNonSourcePublisherTypeRouteIsRejected() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/foo"] = (topicDetailData(topicType: "topic"), 200)

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/card/foo/tags/publisher_type"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .topic
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.tabs.map(\.value), ["topic", "category", "post", "landing_page", "terms_of_service"])
        XCTAssertEqual(viewModel.activeTab, "")
        XCTAssertNil(viewModel.activeTabConfig)
        XCTAssertTrue(viewModel.relations.isEmpty)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/entity-relations/topic/foo/publisher_type/topic"
        })
        if case .error(.notFound) = viewModel.state {
        } else {
            XCTFail("Expected notFound state for invalid publisher_type route, got \(viewModel.state)")
        }
    }

    func testReloadRelationsClearsStaleRowsOnFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/source-1"] = (sourceDetailData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/publisher-types"] = (publisherTypesData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/topic/source-1/publisher_type/topic"] = [
            (publisherRelationData, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/topic/source-1/related/topic"] = [
            (Data("{}".utf8), 500, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/source-1/tags/publisher_type"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .topic,
            subjectId: "source-1",
            subjectTitle: "Source One"
        )

        await viewModel.load()
        XCTAssertFalse(viewModel.relations.isEmpty)
        viewModel.activeTab = "topic"

        await viewModel.reloadRelations()

        XCTAssertTrue(viewModel.relations.isEmpty)
        XCTAssertTrue(viewModel.electionVotes.isEmpty)
        if case .error = viewModel.state {
        } else {
            XCTFail("Expected reload failure to surface an error, got \(viewModel.state)")
        }
    }

    private var sourceDetailData: Data {
        Data("""
        {
          "topic": {
            "id": "source-1",
            "name": "Source One",
            "slug": "source-one",
            "topic_type": "rss_feed"
          },
          "html": "",
          "topic_categories": []
        }
        """.utf8)
    }

    private func topicDetailData(topicType: String) -> Data {
        Data("""
        {
          "topic": {
            "id": "topic-1",
            "name": "Topic One",
            "slug": "topic-one",
            "topic_type": "\(topicType)"
          },
          "html": "",
          "topic_categories": []
        }
        """.utf8)
    }

    private var publisherTypesData: Data {
        Data("""
        {
          "publisher_types": [
            { "id": "publisher-blog", "slug": "blog", "label": "Blog" }
          ]
        }
        """.utf8)
    }

    private var entityRelationsData: Data {
        publisherRelationData
    }

    private var postDetailData: Data {
        Data("""
        {
          "post": {
            "id": "post-1",
            "slug": "native-post",
            "post_type": "discussion",
            "title": "Native Post",
            "markdown": "Body",
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "privacy": "public",
            "is_anonymous": false
          }
        }
        """.utf8)
    }

    private var topicSearchData: Data {
        Data("""
        {
          "results": [
            { "id": "topic-2", "name": "Swift", "slug": "swift", "topic_type": "topic" }
          ],
          "topics": {
            "topic-2": { "id": "topic-2", "name": "Swift", "slug": "swift", "topic_type": "topic" }
          },
          "topics_metrics": {}
        }
        """.utf8)
    }

    private var postSearchData: Data {
        Data("""
        {
          "results": [
            { "entity_id": "post-2" }
          ],
          "posts": {
            "post-2": {
              "id": "post-2",
              "slug": "related-native-post",
              "post_type": "discussion",
              "title": "Related Native Post",
              "markdown": "Related body",
              "created_by_id": "user-2",
              "created_at": "2026-01-02T00:00:00Z",
              "privacy": "public",
              "is_anonymous": false
            }
          },
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }

    private var emptyRelationsData: Data {
        Data("""
        {
          "results": [],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "entity_relations": {}
        }
        """.utf8)
    }

    private func relationData(choice: ElectionVoteChoice) -> Data {
        Data("""
        {
          "results": [{ "id": "relation-1" }],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "entity_relations": {
            "relation-1": {
              "id": "relation-1",
              "subject_id": "post-1",
              "object_id": "topic-2",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "deleted_at": null,
              "deleted_by_id": null,
              "votes_count_up": 1,
              "votes_count_down": 0,
              "votes_score_net": 1,
              "votes_score_sort": 1,
              "object_data": {
                "id": "topic-2",
                "name": "Swift",
                "slug": "swift",
                "topic_type": "topic"
              }
            }
          },
          "election_votes": {
            "relation-1": {
              "__entity_type": "election_vote",
              "entity_id": "relation-1",
              "user_id": "user-1",
              "choice": "\(choice.rawValue)",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """.utf8)
    }

    private var entityRelationResponseData: Data {
        Data("""
        {
          "relation": {
            "id": "relation-1",
            "subject_id": "post-1",
            "object_id": "topic-2",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "deleted_at": null,
            "deleted_by_id": null,
            "votes_count_up": 1,
            "votes_count_down": 0,
            "votes_score_net": 1,
            "votes_score_sort": 1,
            "object_data": {
              "id": "topic-2",
              "name": "Swift",
              "slug": "swift",
              "topic_type": "topic"
            }
          }
        }
        """.utf8)
    }

    private var publisherRelationData: Data {
        Data("""
        {
          "results": [{ "id": "relation-1" }],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "entity_relations": {
            "relation-1": {
              "id": "relation-1",
              "subject_id": "source-1",
              "object_id": "publisher-blog",
              "created_at": "2026-01-01T00:00:00Z",
              "created_by_id": "user-1",
              "deleted_at": null,
              "deleted_by_id": null,
              "votes_count_up": 1,
              "votes_count_down": 0,
              "votes_score_net": 1,
              "votes_score_sort": 1,
              "object_data": {
                "id": "publisher-blog",
                "name": "Blog",
                "slug": "blog",
                "topic_type": "topic"
              }
            }
          }
        }
        """.utf8)
    }
}
