@testable import VouchaFeatures
import XCTest

final class NativeUserProfileScopeTests: XCTestCase {
    func testEverySupportedProfileRouteParsesToTypedScope() {
        let expected: [(String, NativeUserProfileScope)] = [
            ("/user/alice", .overview),
            ("/user/alice/posts", .posts(.all)),
            ("/user/alice/reviews", .posts(.reviews)),
            ("/user/alice/discussions", .posts(.discussions)),
            ("/user/alice/comments", .posts(.comments)),
            ("/user/alice/topics/following", .topicsFollowing),
            ("/user/alice/users/following", .usersFollowing),
            ("/user/alice/users/followers", .usersFollowers),
            ("/user/alice/rss-feeds/following", .sourcesFollowing(nil)),
            ("/user/alice/communities/member", .communitiesMember)
        ]
        for (path, scope) in expected {
            XCTAssertEqual(NativeUserProfileScope(match: match(path)), scope, path)
        }
    }

    func testSourcesParseAllowedFiltersAndRejectUnknownFilter() {
        for filter in NativeUserProfileScope.SourceFilter.allCases {
            let route = match("/user/alice/rss-feeds/following", query: ["feed_type": filter.rawValue])
            XCTAssertEqual(NativeUserProfileScope(match: route), .sourcesFollowing(filter))
        }
        XCTAssertNil(NativeUserProfileScope(match: match(
            "/user/alice/rss-feeds/following",
            query: ["feed_type": "mixed"]
        )))
    }

    func testUnknownProfileSubpathIsRejected() {
        XCTAssertNil(NativeUserProfileScope(match: match("/user/alice/recommendations")))
    }

    func testNavigationEntityTypesUseCanonicalHyphenatedRouteSegments() {
        XCTAssertEqual(NativeUserProfileNavigationTarget.routeSegment("data_point"), "data-point")
        XCTAssertEqual(NativeUserProfileNavigationTarget.routeSegment("referral_program"), "referral-program")
    }

    private func match(_ path: String, query: [String: String] = [:]) -> NativeRouteMatch {
        NativeRouteMatch(
            path: path,
            template: "/user/:idOrUsername",
            params: ["idOrUsername": "alice"],
            queryItems: query
        )
    }
}
