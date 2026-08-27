@testable import VouchaFeatures
import VouchaModels
import XCTest

final class NativeTagManagementModelsTests: XCTestCase {
    func testPostTagTabsMatchWebConfig() {
        XCTAssertEqual(nativePostTagTabs.map(\.value), ["topic", "post", "url"])
        XCTAssertEqual(nativePostTagTabs[0].predicate, "category")
        XCTAssertEqual(nativePostTagTabs[1].predicate, "related")
        XCTAssertEqual(nativePostTagTabs[2].predicate, "related")
    }

    func testTopicTagTabsIncludePublisherTypeOnlyForSources() {
        XCTAssertEqual(
            nativeTopicTagTabs(for: nil).map(\.value),
            ["topic", "category", "post", "landing_page", "terms_of_service"]
        )
        XCTAssertEqual(
            nativeTopicTagTabs(for: "rss_feed").map(\.value),
            ["topic", "category", "publisher_type", "post", "landing_page", "terms_of_service"]
        )
    }

    func testRssFeedItemTagTabsUseCategoryTopics() {
        XCTAssertEqual(nativeRssFeedItemTagTabs.map(\.value), ["topic"])
        XCTAssertEqual(nativeRssFeedItemTagTabs.first?.predicate, "category")
        XCTAssertEqual(nativeRssFeedItemTagTabs.first?.objectType, "topic")
    }

    @MainActor
    func testUserTagManagementUsesUserCopyAndExcludesAppliedOptions() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let viewModel = NativeTagManagementViewModel(client: nil, routeMatch: nil, subjectKind: .user)
        viewModel.publisherTypes = try decoder.decode(
            [PublisherTypeTopic].self,
            from: Data(
                #"[{"id":"user-tag-bot","slug":"bot","label":"Bot"},{"id":"user-tag-spammer","slug":"spammer","label":"Spammer"}]"#
                    .utf8
            )
        )
        viewModel.relations = try decoder.decode(
            [EntityRelation].self,
            from: Data(
                #"[{"id":"relation-1","object_id":"user-tag-bot","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","object_data":{"name":"Bot"}}]"#
                    .utf8
            )
        )

        XCTAssertEqual(uiEnglish(viewModel.managementTitle), "Manage user tags")
        XCTAssertEqual(uiEnglish(viewModel.tagPickerTitle), "User tag")
        XCTAssertEqual(uiEnglish(viewModel.tagPickerPlaceholder), "Select user tag")
        XCTAssertEqual(viewModel.availablePublisherTypes.map(\.id), ["user-tag-spammer"])
    }
}
