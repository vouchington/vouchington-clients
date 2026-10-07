import VouchaAPI
import VouchaModels
import XCTest

enum StoryFixture {
    static let storyId = "01950000-0000-7000-8000-000000000001"
    static let primaryItemId = "01950000-0000-7000-8000-000000000010"
    static var continuationCursor: String {
        do {
            let page = try makeVouchaDecoder().decode(
                StoryPageResponse.self,
                from: ApiFixtureLoader.data("native.stories.get.default")
            )
            guard let cursor = page.pageInfo.endCursor else {
                XCTFail("The first story page fixture must provide a continuation cursor")
                return ""
            }
            return cursor
        } catch {
            XCTFail("Cannot decode the first story page fixture: \(error)")
            return ""
        }
    }

    static var firstEndpoint: Endpoint {
        .story(storyId: storyId, excludeItemId: primaryItemId, limit: 1)
    }

    static var nextEndpoint: Endpoint {
        .story(storyId: storyId, after: continuationCursor, excludeItemId: primaryItemId)
    }
}
