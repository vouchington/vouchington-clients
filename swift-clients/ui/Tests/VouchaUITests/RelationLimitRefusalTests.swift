import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class RelationLimitRefusalTests: NativeRouteSurfaceViewModelTestCase {
    private let refusal = Data(#"{"code":"RELATION_LIMIT_REACHED"}"#.utf8)

    func testFriendsFollowRefusalRollsBackAndRendersLocalizedFeedback() async throws {
        let config = try AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999")), turnstileSiteKey: "test")
        let model = try FriendsListViewModel(client: makeClient(), userId: "me", config: config)
        model.tab = .followers
        try model.followersPagination.replaceItems([
            APIClient.makeDecoder().decode(
                PublicUser.self,
                from: Data(#"{"id":"other","username":"other","roles":[]}"#.utf8)
            )
        ])
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/other/follow"] = (refusal, 409)

        await model.toggleFollow(userId: "other")

        XCTAssertFalse(model.isFollowing(userId: "other"))
        XCTAssertTrue(model.following.isEmpty)
        XCTAssertEqual(model.statusMessage, .message(.nativeCommonSomethingWentWrong))
        let locale = Locale(identifier: "fr")
        let view = FriendsListView(viewModel: model).environment(\.locale, locale)
        XCTAssertNoThrow(try view.inspect().find(text: UiMessages.string(
            .nativeCommonSomethingWentWrong,
            locale: locale
        )))
    }

    func testDetailFollowAndSubscriptionRefusalsRollBackAndRenderFeedback() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/bob"))
        for (entityType, predicate) in [
            ("user", "follow"),
            ("topic", "follow"),
            ("rss_feed", "follow"),
            ("post", "subscribe")
        ] {
            let model = try NativeRouteSurfaceViewModel(
                entry: route.entry,
                client: makeClient(),
                routeMatch: route.match
            )
            model.detailRelationEntityType = entityType
            model.detailRelationEntityId = "other"
            CannedFeedURLProtocol.handlers["/api/v1/bookmarks/\(entityType)/other/\(predicate)"] = (refusal, 409)

            await model.toggleDetailRelation(predicate: predicate)

            XCTAssertFalse(model.isDetailRelationActive(predicate))
            XCTAssertEqual(model.detailRelationErrorMessage, .message(.nativeCommonSomethingWentWrong))
            let view = NativeDetailSurface(entry: route.entry, viewModel: model)
            XCTAssertNoThrow(try view.inspect().find(text: UiMessages.string(
                .nativeCommonSomethingWentWrong,
                locale: Locale(identifier: "en")
            )))
        }
    }

    func testSourceAndTopicFollowRefusalsRollBackWithoutSuccessFeedback() async throws {
        let model = try SourcesListViewModel(client: makeClient(), userId: "me", feedType: nil)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/feed/follow"] = (refusal, 409)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/topic/follow"] = (refusal, 409)

        await model.toggleFollow(sourceId: "feed")
        XCTAssertFalse(model.isFollowing(sourceId: "feed"))
        XCTAssertEqual(model.statusMessage, .message(.nativeSwiftSourcesUnableToFollowSource))

        await model.toggleFollowTopic(topicId: "topic")
        XCTAssertFalse(model.isFollowingTopic(topicId: "topic"))
        XCTAssertEqual(model.statusMessage, .message(.nativeCommonSomethingWentWrong))
    }
}
