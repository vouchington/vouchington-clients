import Foundation

public extension ModerationAppealReadProjection {
    var id: String {
        switch self {
        case let .member(appeal): appeal.id
        case let .staff(appeal): appeal.id
        }
    }

    var userWarningId: String? {
        switch self {
        case let .member(appeal): appeal.userWarningId
        case let .staff(appeal): appeal.userWarningId
        }
    }

    var userSuspensionId: String? {
        switch self {
        case let .member(appeal): appeal.userSuspensionId
        case let .staff(appeal): appeal.userSuspensionId
        }
    }

    var communityBanId: String? {
        switch self {
        case let .member(appeal): appeal.communityBanId
        case let .staff(appeal): appeal.communityBanId
        }
    }

    var postId: String? {
        switch self {
        case let .member(appeal): appeal.postId
        case let .staff(appeal): appeal.postId
        }
    }

    var postRemovalKind: ModerationAppealPostRemovalKind? {
        switch self {
        case let .member(appeal): appeal.postRemovalKind
        case let .staff(appeal): appeal.postRemovalKind
        }
    }

    var status: ModerationAppealStatus {
        switch self {
        case let .member(appeal): appeal.status
        case let .staff(appeal): appeal.status
        }
    }

    var publicResponse: String? {
        switch self {
        case let .member(appeal): appeal.publicResponse
        case let .staff(appeal): appeal.publicResponse
        }
    }

    var approvedAt: Date? {
        switch self {
        case let .member(appeal): appeal.approvedAt
        case let .staff(appeal): appeal.approvedAt
        }
    }

    var sentAt: Date? {
        switch self {
        case let .member(appeal): appeal.sentAt
        case let .staff(appeal): appeal.sentAt
        }
    }

    var resolvedAt: Date? {
        switch self {
        case let .member(appeal): appeal.resolvedAt
        case let .staff(appeal): appeal.resolvedAt
        }
    }

    var resolutionAction: ModerationAppealAction? {
        switch self {
        case let .member(appeal): appeal.resolutionAction
        case let .staff(appeal): appeal.resolutionAction
        }
    }

    var createdAt: Date {
        switch self {
        case let .member(appeal): appeal.createdAt
        case let .staff(appeal): appeal.createdAt
        }
    }

    var targetContext: ModerationAppealTargetContext? {
        switch self {
        case let .member(appeal): appeal.targetContext
        case let .staff(appeal): appeal.targetContext
        }
    }

    var caseId: String? {
        switch self {
        case .member: nil
        case let .staff(appeal): appeal.caseId
        }
    }
}
