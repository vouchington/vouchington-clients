import VouchaLocalization
import VouchaModels

struct NativeTagRelationTab: Identifiable, Hashable {
    let label: UiMessage
    let value: String
    let predicate: String
    let objectType: String

    var id: UiMessageKey {
        label.key
    }

    static func == (lhs: Self, rhs: Self) -> Bool {
        lhs.id == rhs.id
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
}

enum NativeTagManagementSubjectKind: String {
    case post
    case topic
    case rssFeedItem = "rss_feed_item"
    case user
}

let nativePostTagTabs: [NativeTagRelationTab] = [
    .init(
        label: UiMessage(.nativeSwiftTagManagementCategoryTopics),
        value: "topic",
        predicate: "category",
        objectType: "topic"
    ),
    .init(
        label: UiMessage(.nativeSwiftTagManagementRelatedPosts),
        value: "post",
        predicate: "related",
        objectType: "post"
    ),
    .init(
        label: UiMessage(.nativeSwiftTagManagementRelatedLinks),
        value: "url",
        predicate: "related",
        objectType: "url"
    )
]

let nativeTopicTagTabs: [NativeTagRelationTab] = [
    .init(
        label: UiMessage(.nativeSwiftTagManagementRelatedTopics),
        value: "topic",
        predicate: "related",
        objectType: "topic"
    ),
    .init(
        label: UiMessage(.nativeSwiftTagManagementCategories),
        value: "category",
        predicate: "category",
        objectType: "topic"
    ),
    .init(
        label: UiMessage(.nativeSwiftTagManagementPublisherType),
        value: "publisher_type",
        predicate: "publisher_type",
        objectType: "topic"
    ),
    .init(label: UiMessage(.nativeSwiftTagManagementFaqPosts), value: "post", predicate: "faq", objectType: "post"),
    .init(
        label: UiMessage(.nativeSwiftTagManagementLandingPage),
        value: "landing_page",
        predicate: "landing_page",
        objectType: "url"
    ),
    .init(
        label: UiMessage(.nativeSwiftTagManagementTermsOfService),
        value: "terms_of_service",
        predicate: "terms_of_service",
        objectType: "url"
    )
]

let nativeRssFeedItemTagTabs: [NativeTagRelationTab] = [
    .init(
        label: UiMessage(.nativeSwiftTagManagementCategoryTopics),
        value: "topic",
        predicate: "category",
        objectType: "topic"
    )
]

let nativeUserTagTabs: [NativeTagRelationTab] = [
    .init(
        label: UiMessage(.nativeSwiftTagManagementUserTags),
        value: "topic",
        predicate: "category",
        objectType: "topic"
    )
]

func nativeTopicTagTabs(for topicType: String?) -> [NativeTagRelationTab] {
    if topicType == "rss_feed" {
        return nativeTopicTagTabs
    }
    return nativeTopicTagTabs.filter { $0.value != "publisher_type" }
}

extension NativeTagManagementViewModel {
    var managementTitle: UiMessageKey {
        subjectKind == .user ? .nativeSwiftTagManagementManageUserTags : .nativeSwiftTagManagementManageTags
    }

    var tagPickerTitle: UiMessageKey {
        subjectKind == .user ? .nativeSwiftTagManagementUserTag : .nativeSwiftTagManagementPublisherType
    }

    var tagPickerPlaceholder: UiMessageKey {
        subjectKind == .user
            ? .nativeSwiftTagManagementSelectUserTag
            : .nativeSwiftTagManagementSelectPublisherType
    }

    var availablePublisherTypes: [PublisherTypeTopic] {
        let appliedIds = Set(relations.compactMap(\.objectId))
        return publisherTypes.filter { !appliedIds.contains($0.id) }
    }
}
