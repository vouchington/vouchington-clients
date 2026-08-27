import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeUserProfileViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testProfileHeaderSurvivesEmptyCollection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/followers"] = (
            page(results: [], hasNextPage: false), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/users/followers"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.userProfile.header?.user.username, "alice")
        XCTAssertTrue(viewModel.rows.isEmpty)
        XCTAssertEqual(viewModel.userProfile.scope, .usersFollowers)
    }

    func testPaginationDeduplicatesRowsAndPreservesHeaderOnAppendFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let path = "/api/v1/users/user-abc/topics/following"
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("native.users.profile.topics-following.first-page"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/topics/following"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()

        await viewModel.loadMoreUserProfileRows()
        XCTAssertEqual(viewModel.userProfile.collection.count, 1)

        CannedFeedURLProtocol.errors[path] = URLError(.cannotConnectToHost)
        await viewModel.loadMoreUserProfileRows()
        XCTAssertNotNil(viewModel.userProfile.appendError)
        XCTAssertEqual(viewModel.userProfile.header?.user.id, "user-abc")
        XCTAssertEqual(viewModel.userProfile.collection.count, 1)

        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )
        XCTAssertFalse(surface.canCastPublicVotes)
        let inspected = try surface.inspect()
        XCTAssertNoThrow(try inspected.find(button: "Try Again"))
        XCTAssertThrowsError(try inspected.find(button: "Load more"))
        XCTAssertNoThrow(
            try inspected.find(viewWithAccessibilityIdentifier: "public-profile-pagination")
        )
    }

    func testConcurrentPaginationStartsOneRequestAndAppendsOnce() async throws {
        let (viewModel, path) = try await loadedTopicsProfile()
        CannedFeedURLProtocol.queuedHandlers[path] = try [(
            topicPage(id: "topic-2", slug: "second-topic", hasNextPage: false), 200, 0
        )]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let first = Task { await viewModel.loadMoreUserProfileRows() }
        await waitUntil { CannedFeedURLProtocol.hasSuspendedResponse(path: path) }
        let duplicate = Task { await viewModel.loadMoreUserProfileRows() }
        await Task.yield()

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await first.value
        await duplicate.value

        XCTAssertEqual(viewModel.userProfile.collection.count, 2)
        XCTAssertFalse(viewModel.userProfile.pagination.hasMore)
    }

    func testPaginationResetRejectsLateContinuation() async throws {
        let (viewModel, path) = try await loadedTopicsProfile()
        CannedFeedURLProtocol.queuedHandlers[path] = try [(
            topicPage(id: "stale-topic", slug: "stale-topic", hasNextPage: false), 200, 0
        )]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let stale = Task { await viewModel.loadMoreUserProfileRows() }
        await waitUntil { CannedFeedURLProtocol.hasSuspendedResponse(path: path) }

        viewModel.userProfile.pagination.reset()
        viewModel.userProfile.collection = .none
        CannedFeedURLProtocol.releaseResponse(path: path)
        await stale.value

        XCTAssertTrue(viewModel.userProfile.collection.isEmpty)
        XCTAssertFalse(viewModel.userProfile.pagination.hasLoadedPage)
        XCTAssertNil(viewModel.userProfile.appendError)
    }

    func testPaginationRetryReusesCursorAndAppendsOnce() async throws {
        let (viewModel, path) = try await loadedTopicsProfile()
        let expectedCursor = try XCTUnwrap(viewModel.userProfile.pagination.endCursor)
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (Data(#"{"message":"offline"}"#.utf8), 500, 0),
            (topicPage(id: "topic-2", slug: "second-topic", hasNextPage: false), 200, 0)
        ]
        await viewModel.loadMoreUserProfileRows()
        XCTAssertNotNil(viewModel.userProfile.appendError)
        let surface = try NativeUserProfileSurface(
            entry: XCTUnwrap(NativeRouteCatalog.matchingRoute(
                for: "/user/alice/topics/following"
            )).entry,
            viewModel: viewModel,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        try surface.inspect().find(button: "Try Again").tap()
        await waitUntil {
            viewModel.userProfile.appendError == nil
                && viewModel.userProfile.collection.count == 2
        }

        let continuationRequests = Array(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.dropFirst()
        )
        XCTAssertEqual(continuationRequests.count, 2)
        XCTAssertEqual(
            continuationRequests.compactMap { query($0, "after") },
            [expectedCursor, expectedCursor]
        )
        XCTAssertEqual(viewModel.userProfile.collection.count, 2)
    }

    func testEveryExplicitScopeLoadsItsExactFilterIntoTypedCollection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/topics/following"] = (
            ApiFixtureLoader.data("native.users.profile.topics-following.first-page"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            ApiFixtureLoader.data("swift.users.following.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/followers"] = (
            ApiFixtureLoader.data("swift.users.followers.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            ApiFixtureLoader.data("native.users.profile.sources-following.article"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/communities/member"] = (
            ApiFixtureLoader.data("native.users.profile.communities-member.first-page"), 200
        )
        let cases: [(String, String?, String?, String, (NativeUserProfileCollection) -> Bool)] = [
            (
                "/user/alice/posts",
                "review,discussion,comment",
                "native.users.profile.posts.all",
                "/review/profile-review",
                {
                    if case .posts = $0 {
                        true
                    } else {
                        false
                    }
                }
            ),
            ("/user/alice/reviews", "review", "native.users.profile.posts.reviews", "/review/profile-review", {
                if case .posts = $0 {
                    true
                } else {
                    false
                }
            }),
            (
                "/user/alice/discussions",
                "discussion",
                "native.users.profile.posts.discussions",
                "/discussion/profile-discussion",
                {
                    if case .posts = $0 {
                        true
                    } else {
                        false
                    }
                }
            ),
            (
                "/user/alice/comments",
                "comment",
                "native.users.profile.posts.comments",
                "/review/profile-comment-root-1/comment/profile-comment-1",
                {
                    if case .posts = $0 {
                        true
                    } else {
                        false
                    }
                }
            ),
            ("/user/alice/topics/following", nil, nil, "/topic/test-topic", {
                if case .topics = $0 {
                    true
                } else {
                    false
                }
            }),
            ("/user/alice/users/following", nil, nil, "/user/friend", {
                if case .users = $0 {
                    true
                } else {
                    false
                }
            }),
            ("/user/alice/users/followers", nil, nil, "/user/friend", {
                if case .users = $0 {
                    true
                } else {
                    false
                }
            }),
            ("/user/alice/rss-feeds/following", nil, nil, "/source/test-topic", {
                if case .sources = $0 {
                    true
                } else {
                    false
                }
            }),
            (
                "/user/alice/rss-feeds/following?feed_type=article",
                nil,
                nil,
                "/source/test-topic",
                {
                    if case .sources = $0 {
                        true
                    } else {
                        false
                    }
                }
            ),
            (
                "/user/alice/rss-feeds/following?feed_type=podcast",
                nil,
                nil,
                "/source/test-topic",
                {
                    if case .sources = $0 {
                        true
                    } else {
                        false
                    }
                }
            ),
            ("/user/alice/rss-feeds/following?feed_type=video", nil, nil, "/source/test-topic", {
                if case .sources = $0 {
                    true
                } else {
                    false
                }
            }),
            ("/user/alice/communities/member", nil, nil, "/communities/test-community", {
                if case .communities = $0 {
                    true
                } else {
                    false
                }
            })
        ]

        for (path, postTypes, postFixture, expectedTarget, isExpectedType) in cases {
            CannedFeedURLProtocol.capturedURLs = []
            if let postFixture {
                CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
                    ApiFixtureLoader.data(postFixture), 200
                )
            }
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: route.entry, client: makeClient(), routeMatch: route.match
            )
            await viewModel.load()

            XCTAssertTrue(isExpectedType(viewModel.userProfile.collection), path)
            XCTAssertFalse(viewModel.userProfile.collection.isEmpty, path)
            let target = try XCTUnwrap(firstNavigationTarget(viewModel.userProfile.collection), path)
            XCTAssertEqual(target, expectedTarget, path)
            XCTAssertNotNil(
                NativeRouteCatalog.matchingRoute(for: target),
                "\(path) produced non-native target \(target)"
            )
            if let postTypes {
                let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first { $0.path == "/api/v1/posts" })
                XCTAssertEqual(query(request, "post_types"), postTypes, path)
            } else if path.contains("rss-feeds") {
                let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first { $0.path.contains("rss-feeds") })
                XCTAssertEqual(query(request, "feed_type"), route.match.queryValue("feed_type"), path)
            }
        }
    }

    func testCommentRowRetainsItsRootForCanonicalNavigation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.users.profile.posts.comments"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/comments"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case let .posts(rows) = viewModel.userProfile.collection,
              rows.count == 1,
              case let .comment(comment: comment, root: root) = rows[0]
        else {
            return XCTFail("Expected one hydrated comment row")
        }
        XCTAssertEqual(comment.id, "profile-comment-1")
        XCTAssertEqual(root.id, "profile-comment-root-1")
        XCTAssertEqual(root.postType, .review)
        XCTAssertEqual(comment.html, "<p>Rendered profile-comment-1</p>")
        XCTAssertEqual(comment.metrics?.count.descendants, 1)
        XCTAssertEqual(comment.election?.votesScoreNet, 2)
        XCTAssertEqual(comment.election?.myVote, .dislike)
        XCTAssertEqual(root.html, "")
        XCTAssertNil(root.metrics)
        XCTAssertNil(root.election)
        let target = NativeUserProfileNavigationTarget.post(.comment(comment: comment, root: root))
        XCTAssertEqual(target, "/review/profile-comment-root-1/comment/profile-comment-1")
        XCTAssertNotNil(NativeRouteCatalog.matchingRoute(for: target))
    }

    func testTopicRecommendationRowsUsePluralNativeRoute() {
        let root = Post(
            id: "recommendation-root",
            slug: "ignored-recommendation-slug",
            postType: .topicRecommendation,
            title: "Recommendation",
            markdown: nil,
            html: nil,
            parentId: nil,
            rootId: nil,
            createdById: nil,
            createdAt: Date(timeIntervalSince1970: 0),
            broadcast: nil,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil
        )
        let comment = Post(
            id: "recommendation-comment",
            slug: nil,
            postType: .comment,
            title: nil,
            markdown: "Comment",
            html: nil,
            parentId: root.id,
            rootId: root.id,
            createdById: nil,
            createdAt: Date(timeIntervalSince1970: 0),
            broadcast: nil,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil
        )

        let rootTarget = NativeUserProfileNavigationTarget.post(.root(root))
        let commentTarget = NativeUserProfileNavigationTarget.post(.comment(comment: comment, root: root))

        XCTAssertEqual(rootTarget, "/topic-recommendations/recommendation-root")
        XCTAssertEqual(
            commentTarget,
            "/topic-recommendations/recommendation-root/comment/recommendation-comment"
        )
        XCTAssertEqual(
            NativeUserProfileNavigationTarget.post(.root(comment)),
            "/posts"
        )
        XCTAssertNotNil(
            NativeRouteCatalog.matchingRoute(
                for: NativeUserProfileNavigationTarget.post(.root(comment))
            )
        )
        XCTAssertNotNil(NativeRouteCatalog.matchingRoute(for: rootTarget))
        XCTAssertEqual(
            NativeRouteCatalog.matchingRoute(for: commentTarget)?.entry.destinationIdentifier,
            .postDetail
        )
    }

    private func query(_ url: URL, _ name: String) -> String? {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == name }?.value
    }

    private func loadedTopicsProfile() async throws -> (NativeRouteSurfaceViewModel, String) {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let path = "/api/v1/users/user-abc/topics/following"
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("native.users.profile.topics-following.first-page"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/user/alice/topics/following"
        ))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        return (viewModel, path)
    }

    private func topicPage(
        id: String,
        slug: String,
        hasNextPage: Bool
    ) throws -> Data {
        var object = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.users.profile.topics-following.first-page")
            ) as? [String: Any]
        )
        var results = try XCTUnwrap(object["results"] as? [[String: Any]])
        results[0]["id"] = id
        results[0]["slug"] = slug
        results[0]["name"] = slug
        object["results"] = results
        object["page_info"] = [
            "has_next_page": hasNextPage,
            "end_cursor": hasNextPage ? "next-\(id)" : NSNull(),
            "start_cursor": NSNull()
        ]
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        while !condition() {
            await Task.yield()
        }
    }

    private func firstNavigationTarget(_ collection: NativeUserProfileCollection) -> String? {
        switch collection {
        case let .posts(items): items.first.map(NativeUserProfileNavigationTarget.post)
        case let .users(items): items.first.map(NativeUserProfileNavigationTarget.user)
        case let .topics(items): items.first.map(NativeUserProfileNavigationTarget.topic)
        case let .sources(items): items.first.map(NativeUserProfileNavigationTarget.source)
        case let .communities(items): items.first.map(NativeUserProfileNavigationTarget.community)
        case .none: nil
        }
    }

    private func page(results: [String], hasNextPage: Bool) -> Data {
        Data("""
        {"results":\(results),"page_info":{"has_next_page":\(hasNextPage),"end_cursor":null,"start_cursor":null}}
        """.utf8)
    }
}
