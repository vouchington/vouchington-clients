import Foundation
import VouchaLocalization
import VouchaModels

struct IntegrityTarget {
    let kind: IntegrityTargetKind
    let id: String
    let path: String?
}

enum IntegrityTargetKind {
    case post
    case postComment
    case user
    case domain
    case topic
    case rssItem
    case entityRelation
    case agentModeration
    case flag
}

extension IntegrityTargetKind {
    var messageKey: UiMessageKey {
        switch self {
        case .post: .nativeSwiftIntegrityPostId
        case .postComment: .nativeSwiftIntegrityPostCommentId
        case .user: .nativeSwiftIntegrityUserId
        case .domain: .nativeSwiftIntegrityDomainId
        case .topic: .nativeSwiftIntegrityTopicId
        case .rssItem: .nativeSwiftIntegrityRssItemId
        case .entityRelation: .nativeSwiftIntegrityEntityRelationId
        case .agentModeration: .nativeSwiftIntegrityAgentModerationId
        case .flag: .nativeSwiftIntegrityFlagId
        }
    }
}

func reportIntegrityTarget(_ flag: ReportIntegrityFlag) -> IntegrityTarget {
    if let id = flag.postId {
        return .init(kind: .postComment, id: id, path: nil)
    }
    if let id = flag.reportedUserId {
        return .init(kind: .user, id: id, path: "/user/\(id)")
    }
    if let id = flag.hostnameId {
        return .init(kind: .domain, id: id, path: "/domain/\(id)")
    }
    if let id = flag.rssFeedItemId {
        return .init(kind: .rssItem, id: id, path: nil)
    }
    return .init(kind: .flag, id: flag.id, path: nil)
}

func voteIntegrityTarget(_ flag: VoteIntegrityFlag) -> IntegrityTarget {
    if let id = flag.postId {
        return .init(kind: .post, id: id, path: nil)
    }
    if let id = flag.topicId {
        return .init(kind: .topic, id: id, path: "/topic/\(id)")
    }
    if let id = flag.hostnameId {
        return .init(kind: .domain, id: id, path: "/domain/\(id)")
    }
    if let id = flag.rssFeedItemId {
        return .init(kind: .rssItem, id: id, path: nil)
    }
    if let id = flag.entityRelationId {
        return .init(kind: .entityRelation, id: id, path: nil)
    }
    if let id = flag.agentModerationId {
        return .init(kind: .agentModeration, id: id, path: nil)
    }
    return .init(kind: .flag, id: flag.id, path: nil)
}

func integrityDetails(_ details: [String: DecodedJSONValue], locale: Locale) -> [String] {
    details.keys.sorted().map { "\($0): \(integrityValue(details[$0] ?? .null, locale: locale))" }
}

private func integrityValue(_ value: DecodedJSONValue, locale: Locale) -> String {
    switch value {
    case .null:
        "null"
    case let .bool(value):
        value ? "true" : "false"
    case let .number(value):
        UiMessages.number(value, locale: locale)
    case let .string(value):
        value
    case let .array(values):
        "[\(values.map { integrityValue($0, locale: locale) }.joined(separator: ", "))]"
    case let .object(object):
        object.keys.sorted()
            .map { "\($0): \(integrityValue(object[$0] ?? .null, locale: locale))" }
            .joined(separator: ", ")
            .applyingBraces()
    }
}

private extension String {
    func applyingBraces() -> String {
        "{\(self)}"
    }
}
