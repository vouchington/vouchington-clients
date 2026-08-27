import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeEntityDetailPathTests: XCTestCase {
    func testBookmarkTopicsUseCanonicalSpecialOrdinaryEscapedAndFallbackPaths() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/topics/muted"))
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: nil,
            routeMatch: route.match
        )
        let cases: [(String, String?, String)] = [
            ("topic", "topic slug", "/topic/topic%20slug"),
            ("rss_feed", "source/slug", "/source/source%2Fslug"),
            ("fediverse_instance", "instance slug", "/instance/instance%20slug"),
            ("rewards_program", "rewards", "/rewards-program/rewards"),
            ("topic", nil, "/topic/topic-1")
        ]

        for (topicType, slug, expected) in cases {
            let entity = genericTopic(id: "topic-1", slug: slug, topicType: topicType)
            XCTAssertEqual(viewModel.genericBookmarkPath(entity, entityType: "topic"), expected)
        }
    }

    func testProfileTopicsUseCanonicalSpecialOrdinaryEscapedPaths() {
        let cases: [(String, String, String)] = [
            ("topic", "topic slug", "/topic/topic%20slug"),
            ("rss_feed", "source/slug", "/source/source%2Fslug"),
            ("fediverse_instance", "instance slug", "/instance/instance%20slug"),
            ("referral_program", "program", "/referral-program/program")
        ]

        for (topicType, slug, expected) in cases {
            let topic = Topic(
                id: "topic-1",
                name: "Topic",
                slug: slug,
                markdown: nil,
                topicType: topicType,
                createdAt: .distantPast
            )
            XCTAssertEqual(NativeUserProfileNavigationTarget.topic(topic), expected)
        }
        XCTAssertEqual(
            NativeEntityDetailPath.topic(id: "topic-1", slug: nil, topicType: "topic"),
            "/topic/topic-1"
        )
    }

    func testBookmarkSourcesUseTopicSlugThenTopicIdThenFeedId() {
        let cases: [(String?, String?, String)] = [
            ("topic slug", "topic-id", "/source/topic%20slug"),
            (nil, "topic/id", "/source/topic%2Fid"),
            (nil, nil, "/source/feed%20id")
        ]

        for (slug, topicId, expected) in cases {
            let topic: NativeRssFeedTopicSummary? = if slug != nil || topicId != nil {
                NativeRssFeedTopicSummary(id: topicId, slug: slug)
            } else {
                nil
            }
            let feed = NativeRssFeedSummary(
                id: "feed id",
                title: "Source",
                feedType: "article",
                rssFeedUrl: .init(url: "https://example.test/feed"),
                hostname: nil,
                topic: topic
            )
            XCTAssertEqual(NativeRouteSurfaceViewModel.bookmarkedFeedPath(feed), expected)
        }
    }

    func testProfileSourcesUseTopicSlugThenTopicIdThenFeedId() {
        let cases: [(String?, String?, String)] = [
            ("topic slug", "topic-id", "/source/topic%20slug"),
            (nil, "topic/id", "/source/topic%2Fid"),
            (nil, nil, "/source/feed%20id")
        ]

        for (slug, topicId, expected) in cases {
            let topic = topicId.map {
                RssFeedTopic(id: $0, name: "Topic", slug: slug, topicType: "rss_feed")
            }
            let source = RssFeedSource(
                id: "feed id",
                title: "Source",
                feedType: "article",
                rssFeedUrl: .init(url: "https://example.test/feed"),
                hostname: nil,
                topic: topic,
                publisherType: nil,
                podcastShow: nil
            )
            XCTAssertEqual(NativeUserProfileNavigationTarget.source(source), expected)
        }
    }

    private func genericTopic(id: String, slug: String?, topicType: String) -> NativeGenericEntity {
        NativeGenericEntity(
            id: id,
            slug: slug,
            name: nil,
            title: nil,
            username: nil,
            subject: nil,
            status: nil,
            postType: nil,
            topicType: topicType,
            feedType: nil,
            pathname: nil,
            hostname: nil,
            url: nil,
            description: nil,
            summary: nil
        )
    }
}
