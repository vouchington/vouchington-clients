import VouchaModels

extension NativeChatMCPFact {
    static func make(
        tool name: String, option: DecodedJSONValue?, arguments: DecodedJSONValue? = nil,
        content: DecodedJSONValue
    ) -> Self? {
        switch (name, option) {
        case ("read_my_preferences", .string("email")): makeEmailPreferences(content)
        case ("read_my_preferences", .string("general")): makeGeneralPreferences(content)
        case ("read_my_profile", .string("bio")): makeProfileBio(content)
        case ("read_my_profile", .string("links")): makeProfileLinks(content)
        case ("read_feed", .string("posts")): makeFeedPostIds(content)
        case ("read_feed", .string("rss_items")): makeFeedRssItemIds(content)
        case ("read_feed", .string("referral_links")): makeFeedReferralLinks(content)
        default: makeContent(tool: name, option: option, arguments: arguments, content: content)
        }
    }

    private static func makeContent(
        tool name: String, option: DecodedJSONValue?, arguments: DecodedJSONValue?, content: DecodedJSONValue
    ) -> Self? {
        switch (name, option) {
        case ("read_posts", .string("search")): makePostSearch(content)
        case ("read_posts", .string("details")): makePostDetails(content)
        case ("read_rss_feed_item", .string("list")): makeRssItemList(content)
        case ("read_rss_feed_item", .string("details")): makeRssItemDetails(content)
        case ("get_story", nil): makeStoryItems(content)
        default: makeAccount(tool: name, option: option, arguments: arguments, content: content)
        }
    }

    private static func makeAccount(
        tool name: String, option: DecodedJSONValue?, arguments: DecodedJSONValue?, content: DecodedJSONValue
    ) -> Self? {
        switch (name, option) {
        case ("get_my_financial_profile", nil): makeFinancialProfile(content)
        case ("manage_bookmark", .string("set")): makeBookmarkSet(arguments: arguments, content: content)
        case ("manage_bookmark", .string("remove")): makeBookmarkRemove(arguments: arguments, content: content)
        case ("search_copyright_notices_for_association", nil): makeCopyrightNotices(content)
        case ("list_repeat_infringer_accounts", nil): makeRepeatInfringers(content)
        default: nil
        }
    }
}
