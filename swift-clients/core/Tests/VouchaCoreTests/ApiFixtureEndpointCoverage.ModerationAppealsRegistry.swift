@testable import VouchaAPI
import VouchaModels

let pendingAppealsPageCursor =
    "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiIsInNjb3BlIjoiYXBwZWFsczpwZW5kaW5nOnN0YWZmLWFsbDppZC1kZXNjIn0"
let pendingAppealId = "00000000-0000-7000-8000-000000000102"

let moderationAppealEndpointRegistry: [String: Endpoint] = [
    "native.moderation.appeals.member.default": .appeals(status: .pending, limit: 25),
    "native.moderation.appeals.member.page-2": .appeals(
        status: .pending, limit: 25,
        after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiIsInNjb3BlIjoiYXBwZWFsczpwZW5kaW5nOnVzZXItMTppZC1kZXNjIn0"
    ),
    "native.moderation.appeals.member.resolved": .appeals(status: .resolved, limit: 25),
    "native.moderation.appeals.detail.member.default": .appeal(id: pendingAppealId),
    "native.moderation.appeals.detail.member.sent": .appeal(id: pendingAppealId),
    "native.moderation.appeals.create.member.default": .submitAppeal(ModerationAppealSubmissionRequest(
        targetType: .suspension, reason: .other, details: "Please review this again."
    )),
    "native.moderation.appeals.default": Endpoint.appeals(status: .pending, limit: 25),
    "native.moderation.appeals.page-2": Endpoint.appeals(
        status: .pending,
        limit: 25,
        after: pendingAppealsPageCursor
    ),
    "native.moderation.appeals.update.default": Endpoint.updateAppeal(
        id: pendingAppealId,
        publicResponse: "We reviewed your appeal and reduced the action."
    ),
    "native.moderation.appeals.approval.default": Endpoint.appealApproval(id: pendingAppealId),
    "native.moderation.appeals.delivery.default": Endpoint.appealDelivery(id: pendingAppealId),
    "native.moderation.appeals.resolution.accept": Endpoint.appealResolution(
        id: pendingAppealId,
        action: .accept
    ),
    "native.moderation.appeals.resolution.reduce": Endpoint.appealResolution(
        id: pendingAppealId,
        action: .reduce
    ),
    "native.moderation.appeals.resolution.deny": Endpoint.appealResolution(
        id: pendingAppealId,
        action: .deny
    ),
    "native.moderation.appeals.resolution-drafts.default": Endpoint.appealResolutionDrafts(id: pendingAppealId)
]
