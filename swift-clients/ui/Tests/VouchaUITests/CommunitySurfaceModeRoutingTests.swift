import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

extension CommunitySurfaceRoutingTests {
    func testCommunitySurfaceModeAndTabRouting() {
        XCTAssertEqual(CommunitySurfaceMode.mode(for: nil), .browse)
        XCTAssertEqual(
            CommunitySurfaceMode.mode(for: NativeRouteMatch(
                path: "/communities/create",
                template: "/communities/create"
            )),
            .create
        )
        XCTAssertEqual(
            CommunitySurfaceMode.mode(for: NativeRouteMatch(
                path: "/communities/invite/code-1",
                template: "/communities/invite/:code",
                params: ["code": "code-1"]
            )),
            .invite(code: "code-1")
        )
        XCTAssertEqual(
            CommunitySurfaceMode.mode(for: NativeRouteMatch(
                path: "/communities/builders/members",
                template: "/communities/:slug/members",
                params: ["slug": "builders"]
            )),
            .detail(slug: "builders", tab: .members, isApplicationFormRoute: false, modmailThreadId: nil)
        )
        XCTAssertEqual(
            CommunitySurfaceMode.mode(for: NativeRouteMatch(
                path: "/communities/builders/apply",
                template: "/communities/:slug/apply",
                params: ["slug": "builders"]
            )),
            .detail(slug: "builders", tab: .applications, isApplicationFormRoute: true, modmailThreadId: nil)
        )
        XCTAssertEqual(
            CommunitySurfaceMode.mode(for: NativeRouteMatch(
                path: "/communities/builders/settings/moderation/modmail/thread-1",
                template: "/communities/:slug/settings/moderation/modmail/:threadId",
                params: ["slug": "builders", "threadId": "thread-1"]
            )),
            .detail(slug: "builders", tab: .modmail, isApplicationFormRoute: false, modmailThreadId: "thread-1")
        )
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/news/story-1"), .news)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/lists/topics"), .listTopics)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/lists/rss-feeds"), .listSources)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/lists/posts"), .listPosts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/lists/domains"), .listDomains)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/lists/urls"), .listUrls)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/pinned-posts"), .pinnedPosts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings/pinned-posts"), .pinnedPosts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/apply"), .applications)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/invites"), .invites)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings"), .settings)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings/moderation"), .moderation)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings/modlog"), .modlog)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings/moderation/modmail"), .modmail)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings/bans"), .bans)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/settings/restrictions"), .restrictions)
        XCTAssertEqual(
            CommunitySurfaceTab(path: "/communities/builders/settings/moderation/analytics"),
            .moderationAnalytics
        )
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/moderator-vacation"), .moderatorVacation)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/ai-agents"), .moderation)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/agent-prompts"), .moderation)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/automod/recent-actions"), .moderation)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/posts/pending"), .moderation)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders"), .posts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/modern-art"), .posts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/news"), .posts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/members"), .posts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/applications"), .posts)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/modlog"), .modlog)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/modmail"), .modmail)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/bans"), .bans)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/restrictions"), .restrictions)
        XCTAssertEqual(CommunitySurfaceTab(path: "/communities/builders/moderation-analytics"), .moderationAnalytics)
        XCTAssertEqual(CommunitySurfaceTab.posts.id, "posts")
        XCTAssertEqual(
            UiMessages.string(CommunitySurfaceTab.modlog.titleKey, locale: .english),
            "Modlog"
        )
    }
}
