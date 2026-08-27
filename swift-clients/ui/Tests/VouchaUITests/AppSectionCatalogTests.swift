import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

// MARK: - AppSection

final class AppSectionTests: XCTestCase {
    func testEngineeringIsVisibleToEveryDynamicConfigViewerRole() {
        for role in ["administrator", "moderator", "developer", "customer_support", "investor"] {
            XCTAssertTrue(AppSection.engineering.isVisible(isSignedIn: true, userRoles: [role]))
        }
        XCTAssertFalse(AppSection.engineering.isVisible(isSignedIn: true, userRoles: []))
        XCTAssertFalse(AppSection.engineering.isVisible(isSignedIn: false, userRoles: ["developer"]))
    }

    func testEngineeringSeparatesAdministratorOperationsFromDynamicConfig() {
        let developerGroups = AppSection.engineering.nativeParityGroups(
            isSignedIn: true,
            userRoles: ["developer"]
        )
        XCTAssertEqual(
            developerGroups.map { UiMessages.string($0.title, locale: .english) },
            ["Dynamic config"]
        )
        XCTAssertEqual(developerGroups.first?.entries.first?.destinationIdentifier, .engineeringDynamicConfig)

        let administratorGroups = AppSection.engineering.nativeParityGroups(
            isSignedIn: true,
            userRoles: ["administrator"]
        )
        XCTAssertEqual(
            administratorGroups.map { UiMessages.string($0.title, locale: .english) },
            ["Operations", "Dynamic config"]
        )
    }

    func testRequiresAuth() {
        XCTAssertTrue(AppSection.notifications.requiresAuth)
        XCTAssertTrue(AppSection.friends.requiresAuth)
        XCTAssertTrue(AppSection.profile.requiresAuth)
        XCTAssertTrue(AppSection.messages.requiresAuth)
        XCTAssertTrue(AppSection.settings.requiresAuth)
        XCTAssertTrue(AppSection.library.requiresAuth)
        XCTAssertTrue(AppSection.actions.requiresAuth)
        XCTAssertTrue(AppSection.moderation.requiresAuth)
        XCTAssertTrue(AppSection.crm.requiresAuth)
        XCTAssertTrue(AppSection.engineering.requiresAuth)
        XCTAssertTrue(AppSection.growth.requiresAuth)
        XCTAssertFalse(AppSection.news.requiresAuth)
        XCTAssertFalse(AppSection.videos.requiresAuth)
        XCTAssertFalse(AppSection.podcasts.requiresAuth)
        XCTAssertFalse(AppSection.posts.requiresAuth)
        XCTAssertFalse(AppSection.discover.requiresAuth)
        XCTAssertFalse(AppSection.topics.requiresAuth)
        XCTAssertFalse(AppSection.communities.requiresAuth)
    }

    func testAllCasesCount() {
        XCTAssertEqual(AppSection.allCases.count, 18)
    }

    func testIdEqualsRawValue() {
        for section in AppSection.allCases {
            XCTAssertEqual(section.id, section.rawValue)
        }
    }

    func testTitles() {
        XCTAssertEqual(
            AppSection.allCases.map { UiMessages.string($0.titleKey, locale: .english) },
            [
                "News", "Videos", "Podcasts", "Posts", "Discover", "Topics", "Communities",
                "Messages", "Settings", "Library", "Actions", "Moderation", "CRM", "Engineering",
                "Growth", "Notifications", "Friends", "Profile"
            ]
        )
    }

    @MainActor
    func testRootOwnedTitleUsesSavedLocaleInsteadOfEnglishDeviceLocale() {
        let controller = UiLocaleController(
            savedUiLocale: "fr",
            preferredLanguages: ["en-US"]
        )

        XCTAssertEqual(AppSection.settings.title(using: controller), "Réglages")
        XCTAssertEqual(controller.locale, .french)
    }

    func testSystemImages() {
        XCTAssertEqual(AppSection.news.systemImage, "newspaper")
        XCTAssertEqual(AppSection.videos.systemImage, "play.rectangle")
        XCTAssertEqual(AppSection.podcasts.systemImage, "headphones")
        XCTAssertEqual(AppSection.posts.systemImage, "bubble.left.and.bubble.right")
        XCTAssertEqual(AppSection.discover.systemImage, "magnifyingglass")
        XCTAssertEqual(AppSection.topics.systemImage, "tag")
        XCTAssertEqual(AppSection.communities.systemImage, "person.3")
        XCTAssertEqual(AppSection.messages.systemImage, "message")
        XCTAssertEqual(AppSection.settings.systemImage, "gearshape")
        XCTAssertEqual(AppSection.library.systemImage, "bookmark")
        XCTAssertEqual(AppSection.actions.systemImage, "slider.horizontal.3")
        XCTAssertEqual(AppSection.moderation.systemImage, "shield.lefthalf.filled")
        XCTAssertEqual(AppSection.crm.systemImage, "person.text.rectangle")
        XCTAssertEqual(AppSection.engineering.systemImage, "wrench.and.screwdriver")
        XCTAssertEqual(AppSection.growth.systemImage, "chart.line.uptrend.xyaxis")
        XCTAssertEqual(AppSection.notifications.systemImage, "bell")
        XCTAssertEqual(AppSection.friends.systemImage, "person.2")
        XCTAssertEqual(AppSection.profile.systemImage, "person.circle")
    }

    func testStaffSectionsRequireRoles() {
        XCTAssertEqual(AppSection.moderation.requiredRoles, [])
        XCTAssertEqual(AppSection.crm.requiredRoles, ["administrator"])
        XCTAssertEqual(
            AppSection.engineering.requiredRoles,
            ["administrator", "moderator", "developer", "customer_support", "investor"]
        )
        XCTAssertEqual(AppSection.growth.requiredRoles, ["administrator", "investor"])
        XCTAssertTrue(AppSection.moderation.isVisible(isSignedIn: true, userRoles: []))
        XCTAssertFalse(AppSection.moderation.isVisible(isSignedIn: false, userRoles: []))
        XCTAssertTrue(AppSection.growth.isVisible(isSignedIn: true, userRoles: ["investor"]))
        XCTAssertFalse(AppSection.crm.isVisible(isSignedIn: true, userRoles: ["investor"]))
        XCTAssertFalse(AppSection.moderation.isVisible(isSignedIn: false, userRoles: ["administrator"]))
    }

    func testMembershipGrantOwnerSectionIsAdministratorOnly() {
        XCTAssertTrue(AppSection.crm.isVisible(isSignedIn: true, userRoles: ["administrator"]))
        XCTAssertFalse(AppSection.crm.isVisible(isSignedIn: true, userRoles: ["moderator"]))
        XCTAssertFalse(AppSection.crm.isVisible(isSignedIn: false, userRoles: ["administrator"]))
    }
}

// MARK: - ApiFixtureLoader

final class ApiFixtureLoaderTests: XCTestCase {
    func testMissingFixtureReportsFailureAndReturnsFallbackData() {
        XCTExpectFailure("Missing API fixtures should report XCTest failures and return fallback data.") {
            XCTAssertEqual(
                ApiFixtureLoader.data("missing.fixture"),
                Data("{}".utf8)
            )
        }
    }
}

// MARK: - ContentType

final class ContentTypeTests: XCTestCase {
    func testFeedTypeIsAny() {
        XCTAssertEqual(ContentType.news.feedType, "any")
        XCTAssertEqual(ContentType.video.feedType, "any")
        XCTAssertEqual(ContentType.podcast.feedType, "any")
    }

    func testMediaTypes() {
        XCTAssertEqual(ContentType.news.mediaType, "article")
        XCTAssertEqual(ContentType.video.mediaType, "video")
        XCTAssertEqual(ContentType.podcast.mediaType, "audio")
    }

    func testEmptyTitlesNonEmpty() {
        XCTAssertFalse(UiMessages.string(ContentType.news.emptyTitle, locale: .english).isEmpty)
        XCTAssertFalse(UiMessages.string(ContentType.video.emptyTitle, locale: .english).isEmpty)
        XCTAssertFalse(UiMessages.string(ContentType.podcast.emptyTitle, locale: .english).isEmpty)
    }

    func testEmptyMessagesNonEmpty() {
        XCTAssertFalse(UiMessages.string(ContentType.news.emptyMessage, locale: .english).isEmpty)
        XCTAssertFalse(UiMessages.string(ContentType.video.emptyMessage, locale: .english).isEmpty)
        XCTAssertFalse(UiMessages.string(ContentType.podcast.emptyMessage, locale: .english).isEmpty)
    }
}

// MARK: - PostFilter

final class PostFilterTests: XCTestCase {
    func testAllCasesCount() {
        XCTAssertEqual(PostFilter.allCases.count, 3)
    }

    func testIdEqualsRawValue() {
        for filter in PostFilter.allCases {
            XCTAssertEqual(filter.id, filter.rawValue)
        }
    }

    func testPostTypes() {
        XCTAssertNil(PostFilter.all.postTypes)
        XCTAssertEqual(PostFilter.discussions.postTypes, "discussion")
        XCTAssertEqual(PostFilter.reviews.postTypes, "review")
    }

    func testTitlesNonEmpty() {
        for filter in PostFilter.allCases {
            XCTAssertFalse(filter.titleKey.rawValue.isEmpty)
        }
    }
}

// MARK: - VerticalSubsection

final class VerticalSubsectionTests: XCTestCase {
    func testFeedScopeAllCasesCount() {
        XCTAssertEqual(FeedScope.allCases.count, 2)
    }

    func testSourceScopeAllCasesCount() {
        XCTAssertEqual(SourceScope.allCases.count, 2)
    }

    func testVerticalSubsectionIds() {
        XCTAssertEqual(VerticalSubsection.feed(.your).id, "feed-your")
        XCTAssertEqual(VerticalSubsection.feed(.all).id, "feed-all")
        XCTAssertEqual(VerticalSubsection.sources(.your).id, "sources-your")
        XCTAssertEqual(VerticalSubsection.sources(.all).id, "sources-all")
    }

    func testVerticalSubsectionHashableEquality() {
        XCTAssertEqual(VerticalSubsection.feed(.your), VerticalSubsection.feed(.your))
        XCTAssertNotEqual(VerticalSubsection.feed(.your), VerticalSubsection.feed(.all))
        XCTAssertNotEqual(VerticalSubsection.feed(.your), VerticalSubsection.sources(.your))
    }
}

// MARK: - AppSection+Vertical

final class AppSectionVerticalTests: XCTestCase {
    func testIsVertical() {
        XCTAssertTrue(AppSection.news.isVertical)
        XCTAssertTrue(AppSection.podcasts.isVertical)
        XCTAssertTrue(AppSection.videos.isVertical)
        XCTAssertTrue(AppSection.posts.isVertical)
        XCTAssertFalse(AppSection.notifications.isVertical)
        XCTAssertFalse(AppSection.friends.isVertical)
        XCTAssertFalse(AppSection.profile.isVertical)
        XCTAssertFalse(AppSection.discover.isVertical)
        XCTAssertFalse(AppSection.topics.isVertical)
        XCTAssertFalse(AppSection.communities.isVertical)
        XCTAssertFalse(AppSection.messages.isVertical)
        XCTAssertFalse(AppSection.settings.isVertical)
        XCTAssertFalse(AppSection.library.isVertical)
        XCTAssertFalse(AppSection.actions.isVertical)
    }

    func testSubsectionCountForVerticals() {
        XCTAssertEqual(AppSection.news.subsections.count, 4)
        XCTAssertEqual(AppSection.podcasts.subsections.count, 4)
        XCTAssertEqual(AppSection.videos.subsections.count, 4)
        XCTAssertEqual(AppSection.posts.subsections.count, 2)
    }

    func testSubsectionCountForNonVerticals() {
        XCTAssertTrue(AppSection.notifications.subsections.isEmpty)
        XCTAssertTrue(AppSection.friends.subsections.isEmpty)
        XCTAssertTrue(AppSection.profile.subsections.isEmpty)
        XCTAssertTrue(AppSection.discover.subsections.isEmpty)
        XCTAssertTrue(AppSection.topics.subsections.isEmpty)
        XCTAssertTrue(AppSection.communities.subsections.isEmpty)
        XCTAssertTrue(AppSection.messages.subsections.isEmpty)
        XCTAssertTrue(AppSection.settings.subsections.isEmpty)
        XCTAssertTrue(AppSection.library.subsections.isEmpty)
        XCTAssertTrue(AppSection.actions.subsections.isEmpty)
    }

    func testNewsSubsectionTitles() {
        XCTAssertEqual(uiEnglish(AppSection.news.subsectionTitle(.feed(.your))), "Your News Feed")
        XCTAssertEqual(uiEnglish(AppSection.news.subsectionTitle(.feed(.all))), "All News")
        XCTAssertEqual(uiEnglish(AppSection.news.subsectionTitle(.sources(.your))), "Your Sources")
        XCTAssertEqual(uiEnglish(AppSection.news.subsectionTitle(.sources(.all))), "All Sources")
    }

    func testPodcastsSubsectionTitles() {
        XCTAssertEqual(uiEnglish(AppSection.podcasts.subsectionTitle(.feed(.your))), "Your Episodes")
        XCTAssertEqual(uiEnglish(AppSection.podcasts.subsectionTitle(.feed(.all))), "All Episodes")
        XCTAssertEqual(uiEnglish(AppSection.podcasts.subsectionTitle(.sources(.your))), "Your Podcasts")
        XCTAssertEqual(uiEnglish(AppSection.podcasts.subsectionTitle(.sources(.all))), "All Podcasts")
    }

    func testVideosSubsectionTitles() {
        XCTAssertEqual(uiEnglish(AppSection.videos.subsectionTitle(.feed(.your))), "Your Video Feed")
        XCTAssertEqual(uiEnglish(AppSection.videos.subsectionTitle(.feed(.all))), "All Videos")
        XCTAssertEqual(uiEnglish(AppSection.videos.subsectionTitle(.sources(.your))), "Your Channels")
        XCTAssertEqual(uiEnglish(AppSection.videos.subsectionTitle(.sources(.all))), "All Channels")
    }

    func testPostsSubsectionTitles() {
        XCTAssertEqual(uiEnglish(AppSection.posts.subsectionTitle(.feed(.your))), "Your Posts")
        XCTAssertEqual(uiEnglish(AppSection.posts.subsectionTitle(.feed(.all))), "All Posts")
    }

    func testMediaTypesCorrect() {
        XCTAssertEqual(AppSection.news.mediaType, "article")
        XCTAssertEqual(AppSection.videos.mediaType, "video")
        // Podcast source feed_type='podcast' but item media_type='audio'
        XCTAssertEqual(AppSection.podcasts.mediaType, "audio")
        XCTAssertNil(AppSection.posts.mediaType)
        XCTAssertNil(AppSection.notifications.mediaType)
    }

    func testSourceFeedTypesCorrect() {
        XCTAssertEqual(AppSection.news.sourceFeedType, "article")
        XCTAssertEqual(AppSection.videos.sourceFeedType, "video")
        // Podcast source uses feed_type='podcast', NOT 'audio'
        XCTAssertEqual(AppSection.podcasts.sourceFeedType, "podcast")
        XCTAssertNil(AppSection.posts.sourceFeedType)
    }

    func testContentTypes() {
        XCTAssertEqual(AppSection.news.contentType, .news)
        XCTAssertEqual(AppSection.videos.contentType, .video)
        XCTAssertEqual(AppSection.podcasts.contentType, .podcast)
        XCTAssertNil(AppSection.posts.contentType)
        XCTAssertNil(AppSection.notifications.contentType)
    }
}
