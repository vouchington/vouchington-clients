import VouchaModels

extension NativeChatMCPFact {
    static func make(tool name: String, option: DecodedJSONValue?, content: DecodedJSONValue) -> Self? {
        switch (name, option) {
        case ("read_my_preferences", .string("email")): makeEmailPreferences(content)
        case ("read_my_preferences", .string("general")): makeGeneralPreferences(content)
        case ("read_my_profile", .string("bio")): makeProfileBio(content)
        case ("read_my_profile", .string("links")): makeProfileLinks(content)
        case ("read_feed", .string("posts")): makeFeedPostIds(content)
        case ("read_feed", .string("rss_items")): makeFeedRssItemIds(content)
        case ("read_feed", .string("referral_links")): makeFeedReferralLinks(content)
        default: makeContent(tool: name, option: option, content: content)
        }
    }

    private static func makeContent(tool name: String, option: DecodedJSONValue?, content: DecodedJSONValue) -> Self? {
        switch (name, option) {
        case ("read_posts", .string("search")): makePostSearch(content)
        case ("read_posts", .string("details")): makePostDetails(content)
        case ("read_rss_feed_item", .string("list")): makeRssItemList(content)
        case ("read_rss_feed_item", .string("details")): makeRssItemDetails(content)
        case ("get_story", nil): makeStoryItems(content)
        default: nil
        }
    }
}
