import Foundation
import ViewInspector
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FriendRecommendationsViewTests: NativeRouteSurfaceViewModelTestCase {
    func testSignedOutRoutePromptsForSignIn() throws {
        var didRequestSignIn = false
        let surface = FriendRecommendationsRouteSurface(
            client: nil,
            isSignedIn: false,
            onNavigateToTargetPath: { _ in },
            showSignIn: { didRequestSignIn = true }
        )

        XCTAssertNoThrow(try surface.inspect().find(text: "Sign in required"))
        try surface.inspect().find(button: "Sign in").tap()
        XCTAssertTrue(didRequestSignIn)
    }

    func testLoadedRecommendationsRenderRowsMutationErrorPaginationAndDismissedRoute() async throws {
        CannedFeedURLProtocol.handlers[recommendationsPath] = (
            page(
                recommendations: [
                    recommendation(id: "user-1", provider: "github", name: "Alice")
                ],
                users: [user(id: "user-1", username: "alice", profileImageId: "image-1")],
                cursor: "cursor-1",
                hasMore: true
            ),
            200
        )
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
        let viewModel = try FriendRecommendationsViewModel(
            client: makeClient(),
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "https://voucha.example")),
                imageBaseURL: XCTUnwrap(URL(string: "https://images.example")),
                turnstileSiteKey: "test"
            )
        )
        await viewModel.load()
        viewModel.mutationErrorMessage = .verbatim("Mutation failed")
        var routes: [String] = []
        let view = FriendRecommendationsView(
            viewModel: viewModel,
            onNavigateToTargetPath: { routes.append($0) }
        )
        let inspection = try view.inspect()

        XCTAssertNoThrow(try inspection.find(text: "alice"))
        XCTAssertNoThrow(try inspection.find(text: "GitHub"))
        XCTAssertNoThrow(try inspection.find(text: "Mutation failed"))
        XCTAssertNoThrow(try inspection.find(button: "Follow"))
        XCTAssertNoThrow(try inspection.find(button: "Dismiss"))
        XCTAssertNoThrow(try inspection.find(button: "Load more"))
        XCTAssertEqual(
            try viewModel.avatarURL(for: XCTUnwrap(viewModel.recommendations.first)),
            "https://images.example/images/image-1?w=96"
        )

        try inspection.find(button: "Dismissed").tap()
        XCTAssertEqual(routes, ["/my/friend-recommendations/dismissed"])
    }

    func testPaginationRemainsReachableAfterFollowingLastVisibleRecommendation() async throws {
        try await assertPaginationRemainsReachableAfterLastMutation(
            predicate: "follow",
            mutate: { viewModel, recommendation in
                await viewModel.follow(recommendation)
            }
        )
    }

    func testPaginationRemainsReachableAfterDismissingLastVisibleRecommendation() async throws {
        try await assertPaginationRemainsReachableAfterLastMutation(
            predicate: "dismiss_recommendation",
            mutate: { viewModel, recommendation in
                await viewModel.dismiss(recommendation)
            }
        )
    }

    func testRowFallsBackToProviderFriendNameAndGenericMember() throws {
        var followed = false
        var dismissed = false
        let named = try FriendRecommendationRow(
            recommendation: recommendationModel(name: "  Provider Alice  "),
            user: nil,
            avatarURL: nil,
            isMutating: false,
            onFollow: { followed = true },
            onDismiss: { dismissed = true }
        )
        let namedInspection = try named.inspect()

        XCTAssertNoThrow(try namedInspection.find(text: "  Provider Alice  "))
        XCTAssertNoThrow(try namedInspection.find(text: "GitHub"))
        try namedInspection.find(button: "Follow").tap()
        try namedInspection.find(button: "Dismiss").tap()
        XCTAssertTrue(followed)
        XCTAssertTrue(dismissed)

        let generic = try FriendRecommendationRow(
            recommendation: recommendationModel(name: " \n "),
            user: nil,
            avatarURL: nil,
            isMutating: true,
            onFollow: {},
            onDismiss: {}
        )
        let genericInspection = try generic.inspect()
        XCTAssertNoThrow(try genericInspection.find(text: "Voucha"))
        XCTAssertNoThrow(try genericInspection.find(text: "Member"))
        XCTAssertTrue(try genericInspection.find(button: "Follow").isDisabled())
        XCTAssertTrue(try genericInspection.find(button: "Dismiss").isDisabled())
    }

    func testEmptyLoadedViewRoutesToIdentityOrRetriesBasedOnConnectedProvider() async throws {
        CannedFeedURLProtocol.handlers[recommendationsPath] = (page(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        let disconnected = try FriendRecommendationsViewModel(client: makeClient())
        await disconnected.load()
        var routes: [String] = []
        let disconnectedView = FriendRecommendationsView(
            viewModel: disconnected,
            onNavigateToTargetPath: { routes.append($0) }
        )
        try disconnectedView.inspect().find(button: "Account").tap()
        XCTAssertEqual(routes, ["/my/identity"])

        CannedFeedURLProtocol.handlers[recommendationsPath] = (page(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: [
                "facebook_account": [
                    "id": "facebook-1",
                    "name": "Alice",
                    "email_address": NSNull()
                ]
            ]),
            200
        )
        let connected = try FriendRecommendationsViewModel(client: makeClient())
        await connected.load()
        let connectedView = FriendRecommendationsView(viewModel: connected)
        XCTAssertNoThrow(try connectedView.inspect().find(button: "Try Again"))
        try connectedView.inspect().find(button: "Try Again").tap()
        await waitUntil { CannedFeedURLProtocol.capturedPathCount(self.recommendationsPath) >= 3 }
    }

    func testInitialAndPaginationErrorsExposeRetryState() async throws {
        CannedFeedURLProtocol.handlers[recommendationsPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (Data("{}".utf8), 500)
        let viewModel = try FriendRecommendationsViewModel(client: makeClient())
        await viewModel.load()

        XCTAssertTrue(viewModel.recommendations.isEmpty)
        XCTAssertFalse(viewModel.hasConnectedProvider)
        XCTAssertNoThrow(
            try FriendRecommendationsView(viewModel: viewModel)
                .inspect()
                .find(button: "Try Again")
        )

        await viewModel.loadMore()
        XCTAssertFalse(viewModel.isLoadingMore)
    }

    private var recommendationsPath: String {
        "/api/v1/my/friend-recommendations"
    }

    private func assertPaginationRemainsReachableAfterLastMutation(
        predicate: String,
        mutate: @escaping (FriendRecommendationsViewModel, FriendRecommendation) async -> Void
    ) async throws {
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
                recommendations: [recommendation(id: "user-2", provider: "x", name: "Bob")],
                users: [user(id: "user-2", username: "bob")]
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/user-1/\(predicate)"] = (
            Data("{}".utf8),
            200
        )
        let viewModel = try FriendRecommendationsViewModel(client: makeClient())
        await viewModel.load()
        try await mutate(viewModel, XCTUnwrap(viewModel.recommendations.first))

        XCTAssertTrue(viewModel.recommendations.isEmpty)
        XCTAssertTrue(viewModel.hasMore)
        let view = FriendRecommendationsView(viewModel: viewModel)
        try view.inspect().find(button: "Load more").tap()
        await waitUntil { viewModel.recommendations.map(\.id) == ["user-2"] }

        XCTAssertEqual(viewModel.recommendations.map(\.id), ["user-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(recommendationsPath), 2)
    }

    private func recommendationModel(name: String) throws -> FriendRecommendation {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(
            FriendRecommendation.self,
            from: JSONSerialization.data(withJSONObject: recommendation(
                id: "user-1",
                provider: "github",
                name: name
            ))
        )
    }

    private func recommendation(id: String, provider: String, name: String) -> [String: Any] {
        [
            "__entity_type": "user",
            "id": id,
            "provider": provider,
            "provider_friend_name": name
        ]
    }

    private func user(id: String, username: String, profileImageId: String? = nil) -> [String: Any] {
        [
            "__entity_type": "user",
            "id": id,
            "username": username,
            "roles": [],
            "profile_image_id": profileImageId.map { $0 as Any } ?? NSNull()
        ]
    }

    private func page(
        recommendations: [[String: Any]] = [],
        users: [[String: Any]] = [],
        cursor: String? = nil,
        hasMore: Bool = false
    ) -> Data {
        let usersById = Dictionary(uniqueKeysWithValues: users.compactMap { value in
            (value["id"] as? String).map { ($0, value) }
        })
        return try! JSONSerialization.data(withJSONObject: [
            "results": recommendations,
            "users": usersById,
            "page_info": [
                "has_next_page": hasMore,
                "end_cursor": cursor.map { $0 as Any } ?? NSNull(),
                "start_cursor": NSNull()
            ]
        ])
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 300 where !condition() {
            await Task.yield()
        }
    }
}
