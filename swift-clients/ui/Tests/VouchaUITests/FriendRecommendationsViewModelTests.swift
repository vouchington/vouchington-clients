import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FriendRecommendationsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testTwoPagesHydrateUsersAndDeduplicateRecommendations() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: [
                "github_account": [
                    "id": "github-1",
                    "name": "Alice",
                    "email_address": "alice@example.com"
                ]
            ]),
            200
        )
        CannedFeedURLProtocol.queuedHandlers[recommendationsPath] = [
            (page(
                recommendations: [
                    recommendation(id: "user-1", provider: "github", name: "Alice"),
                    recommendation(id: "user-2", provider: "x", name: "Bob")
                ],
                users: [
                    user(id: "user-1", username: "alice"),
                    user(id: "user-2", username: "bob")
                ],
                cursor: "cursor/opaque+value",
                hasMore: true
            ), 200, 0),
            (page(
                recommendations: [
                    recommendation(id: "user-2", provider: "x", name: "Bob"),
                    recommendation(id: "user-3", provider: "facebook", name: "Charlie")
                ],
                users: [
                    user(id: "user-2", username: "bob-updated"),
                    user(id: "user-3", username: "charlie")
                ]
            ), 200, 0)
        ]
        let viewModel = try FriendRecommendationsViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-1", "user-2", "user-3"])
        XCTAssertEqual(viewModel.users["user-1"]?.username, "alice")
        XCTAssertEqual(viewModel.users["user-2"]?.username, "bob-updated")
        XCTAssertEqual(viewModel.users["user-3"]?.username, "charlie")
        XCTAssertTrue(viewModel.hasConnectedProvider)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs
                .filter { $0.path == recommendationsPath }
                .map(\.query),
            ["limit=25", "limit=25&after=cursor/opaque+value"]
        )
    }

    func testFollowAndDismissRemoveImmediatelyAndRejectDuplicateMutation() async throws {
        let viewModel = try await loadedViewModel(ids: ["user-1", "user-2"])
        let first = try XCTUnwrap(viewModel.recommendations.first)
        let second = try XCTUnwrap(viewModel.recommendations.last)
        let followPath = "/api/v1/bookmarks/user/user-1/follow"
        CannedFeedURLProtocol.handlers[followPath] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.suspendResponse(path: followPath)

        let follow = Task { await viewModel.follow(first) }
        await waitUntil { CannedFeedURLProtocol.hasSuspendedResponse(path: followPath) }
        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-2"])
        XCTAssertTrue(viewModel.isMutating(userId: "user-1"))

        await viewModel.dismiss(first)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(followPath), 1)

        CannedFeedURLProtocol.releaseResponse(path: followPath)
        await follow.value
        XCTAssertFalse(viewModel.isMutating(userId: "user-1"))

        let dismissPath = "/api/v1/bookmarks/user/user-2/dismiss_recommendation"
        CannedFeedURLProtocol.handlers[dismissPath] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.suspendResponse(path: dismissPath)
        let dismiss = Task { await viewModel.dismiss(second) }
        await waitUntil { CannedFeedURLProtocol.hasSuspendedResponse(path: dismissPath) }

        XCTAssertTrue(viewModel.recommendations.isEmpty)
        XCTAssertTrue(viewModel.isMutating(userId: "user-2"))

        CannedFeedURLProtocol.releaseResponse(path: dismissPath)
        await dismiss.value
        XCTAssertFalse(viewModel.isMutating(userId: "user-2"))
    }

    func testFailedMutationRestoresAtClampedOriginalPosition() async throws {
        let viewModel = try await loadedViewModel(ids: ["user-1", "user-2", "user-3"])
        let first = viewModel.recommendations[0]
        let second = viewModel.recommendations[1]
        let failingPath = "/api/v1/bookmarks/user/user-2/follow"
        CannedFeedURLProtocol.handlers[failingPath] = (Data(#"{"message":"offline"}"#.utf8), 503)
        CannedFeedURLProtocol.suspendResponse(path: failingPath)
        let failingMutation = Task { await viewModel.follow(second) }
        await waitUntil { CannedFeedURLProtocol.hasSuspendedResponse(path: failingPath) }
        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-1", "user-3"])

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/user-1/follow"] = (Data("{}".utf8), 200)
        await viewModel.follow(first)
        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-3"])

        CannedFeedURLProtocol.releaseResponse(path: failingPath)
        await failingMutation.value

        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-3", "user-2"])
        XCTAssertNotNil(viewModel.mutationErrorMessage)
    }

    func testSuccessfulTombstoneFiltersLatePage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        CannedFeedURLProtocol.queuedHandlers[recommendationsPath] = [
            (page(
                recommendations: [recommendation(id: "user-1", provider: "github", name: "Alice")],
                users: [user(id: "user-1", username: "alice")],
                cursor: "cursor-2",
                hasMore: true
            ), 200, 0),
            (page(
                recommendations: [
                    recommendation(id: "user-1", provider: "github", name: "Alice"),
                    recommendation(id: "user-2", provider: "x", name: "Bob")
                ],
                users: [
                    user(id: "user-1", username: "alice"),
                    user(id: "user-2", username: "bob")
                ]
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/user-1/follow"] = (Data("{}".utf8), 200)
        let viewModel = try FriendRecommendationsViewModel(client: makeClient())

        await viewModel.load()
        let first = try XCTUnwrap(viewModel.recommendations.first)
        await viewModel.follow(first)
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-2"])
    }

    private var recommendationsPath: String {
        "/api/v1/my/friend-recommendations"
    }

    private func loadedViewModel(ids: [String]) async throws -> FriendRecommendationsViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        CannedFeedURLProtocol.handlers[recommendationsPath] = (
            page(
                recommendations: ids.enumerated().map { index, id in
                    recommendation(id: id, provider: index.isMultiple(of: 2) ? "github" : "x", name: id)
                },
                users: ids.map { user(id: $0, username: $0) }
            ),
            200
        )
        let viewModel = try FriendRecommendationsViewModel(client: makeClient())
        await viewModel.load()
        return viewModel
    }

    private func recommendation(id: String, provider: String, name: String) -> [String: Any] {
        [
            "__entity_type": "user",
            "id": id,
            "provider": provider,
            "provider_friend_name": name
        ]
    }

    private func user(id: String, username: String) -> [String: Any] {
        [
            "__entity_type": "user",
            "id": id,
            "username": username,
            "roles": [],
            "profile_image_id": NSNull()
        ]
    }

    private func page(
        recommendations: [[String: Any]],
        users: [[String: Any]],
        cursor: String? = nil,
        hasMore: Bool = false
    ) -> Data {
        let usersById: [String: [String: Any]] = Dictionary(
            uniqueKeysWithValues: users.compactMap { user -> (String, [String: Any])? in
                guard let id = user["id"] as? String else { return nil }
                return (id, user)
            }
        )
        let encodedCursor: Any = cursor.map { $0 as Any } ?? NSNull()
        return try! JSONSerialization.data(withJSONObject: [
            "results": recommendations,
            "users": usersById,
            "page_info": [
                "has_next_page": hasMore,
                "end_cursor": encodedCursor,
                "start_cursor": NSNull()
            ]
        ])
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 200 where !condition() {
            await Task.yield()
        }
    }
}
