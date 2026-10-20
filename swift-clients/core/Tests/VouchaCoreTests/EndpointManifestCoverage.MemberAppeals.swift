import VouchaAPI
import VouchaModels

extension EndpointManifestCoverage {
    static let memberAppealEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.member.default") {
            Endpoint.appeals(status: .pending, limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.member.page-2") {
            Endpoint.appeals(
                status: .pending, limit: 25,
                after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiIsInNjb3BlIjoiYXBwZWFsczpwZW5kaW5nOnVzZXItMTppZC1kZXNjIn0"
            )
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.member.resolved") {
            Endpoint.appeals(status: .resolved, limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.detail.member.default") {
            Endpoint.appeal(id: pendingAppealId)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.detail.member.sent") {
            Endpoint.appeal(id: pendingAppealId)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.create.member.default") {
            Endpoint.submitAppeal(ModerationAppealSubmissionRequest(
                targetType: .suspension, reason: .other, details: "Please review this again."
            ))
        }
    ]
}
