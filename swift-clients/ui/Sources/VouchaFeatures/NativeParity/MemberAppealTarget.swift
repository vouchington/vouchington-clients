import Foundation
import VouchaModels

enum MemberAppealsRoute: Hashable {
    case tracking
    case warnings
    case bans
    case removedPosts
    case suspension

    init?(path: String?) {
        switch path {
        case "/my/appeals": self = .tracking
        case "/my/warnings": self = .warnings
        case "/my/bans": self = .bans
        case "/my/removed-posts": self = .removedPosts
        case "/my/account-status": self = .suspension
        default: return nil
        }
    }
}

enum MemberAppealTarget: Identifiable, Hashable {
    case warning(id: String, message: String?, community: String?, createdAt: Date)
    case ban(id: String, reason: String?, community: String?, createdAt: Date)
    case removal(id: String, title: String?, community: String?, kind: ModerationAppealPostRemovalKind, date: Date)
    case suspension(date: Date)

    var id: String {
        switch self {
        case let .warning(id, _, _, _): "warning:\(id)"
        case let .ban(id, _, _, _): "ban:\(id)"
        case let .removal(id, _, _, kind, _): "removal:\(kind.rawValue):\(id)"
        case let .suspension(date): "suspension:\(date.timeIntervalSinceReferenceDate.bitPattern)"
        }
    }

    var targetType: ModerationAppealTargetType {
        switch self {
        case .warning: .warning
        case .ban: .ban
        case .removal: .removal
        case .suspension: .suspension
        }
    }

    var targetId: String? {
        switch self {
        case let .warning(id, _, _, _), let .ban(id, _, _, _), let .removal(id, _, _, _, _): id
        case .suspension: nil
        }
    }

    var postRemovalKind: ModerationAppealPostRemovalKind? {
        guard case let .removal(_, _, _, kind, _) = self else { return nil }
        return kind
    }

    var date: Date {
        switch self {
        case let .warning(_, _, _, date), let .ban(_, _, _, date), let .removal(_, _, _, _, date),
             let .suspension(date):
            date
        }
    }
}

extension MemberWarningNotice {
    var appealTarget: MemberAppealTarget {
        .warning(id: id, message: publicMessage, community: communitySlug, createdAt: createdAt)
    }
}

extension MemberCommunityBanNotice {
    var appealTarget: MemberAppealTarget {
        .ban(id: id, reason: reason, community: communitySlug, createdAt: createdAt)
    }
}

extension MemberRemovedPostNotice {
    var appealTarget: MemberAppealTarget {
        .removal(
            id: postId,
            title: postTitle,
            community: communitySlug,
            kind: postRemovalKind,
            date: unpublishedAt
        )
    }
}
