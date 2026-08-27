import VouchaLocalization
import VouchaModels

struct NativeTopicDetailResponse: Decodable {
    let topic: NativeGenericEntity
    let topicElection: TopicElection?
    let electionVote: NativeViewerVote?
}

typealias NativeViewerVote = ElectionVote

extension NativeRouteDestinationIdentifier {
    var entityListSurface: NativeEntityListSurface? {
        switch self {
        case .topicsBrowse:
            .init(path: "/api/v1/topics", icon: "tag", title: .nativeSwiftNavigationTitlesTopics)
        case .domainsBrowse:
            .init(
                path: "/api/v1/hostnames",
                icon: "globe",
                title: .nativeSwiftRouteMetadataMainDomainsBrowseDomainsTitle
            )
        case .urlsBrowse:
            .init(path: "/api/v1/urls", icon: "link", title: .nativeSwiftRouteMetadataMainUrlsBrowseUrlsTitle)
        case .usersBrowse:
            nil
        default:
            nil
        }
    }
}
