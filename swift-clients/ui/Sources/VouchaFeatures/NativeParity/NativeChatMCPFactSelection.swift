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
        default: nil
        }
    }
}
