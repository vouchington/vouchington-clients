import Foundation
import VouchaAPI
import VouchaModels

enum IntegrityPenaltyRow: Identifiable {
    case report(ReportIntegrityPenalty)
    case vote(VoteIntegrityPenalty)

    var id: String {
        switch self {
        case let .report(penalty): penalty.id
        case let .vote(penalty): penalty.id
        }
    }

    var userId: String {
        switch self {
        case let .report(penalty): penalty.userId
        case let .vote(penalty): penalty.userId
        }
    }

    var reason: String {
        switch self {
        case let .report(penalty): penalty.reason
        case let .vote(penalty): penalty.reason
        }
    }

    var sourceFlagId: String? {
        switch self {
        case let .report(penalty): penalty.sourceFlagId
        case let .vote(penalty): penalty.sourceFlagId
        }
    }

    var createdById: String? {
        switch self {
        case let .report(penalty): penalty.createdById
        case let .vote(penalty): penalty.createdById
        }
    }

    var createdAt: Date {
        switch self {
        case let .report(penalty): penalty.createdAt
        case let .vote(penalty): penalty.createdAt
        }
    }

    var revokedAt: Date? {
        switch self {
        case let .report(penalty): penalty.revokedAt
        case let .vote(penalty): penalty.revokedAt
        }
    }

    var revokedById: String? {
        switch self {
        case let .report(penalty): penalty.revokedById
        case let .vote(penalty): penalty.revokedById
        }
    }

    var multiplier: Double? {
        guard case let .vote(penalty) = self else { return nil }
        return penalty.penaltyMultiplier
    }
}

struct IntegrityPenaltyPage {
    let results: [IntegrityPenaltyRow]
    let pageInfo: Page<ReportIntegrityPenalty>.PageInfo
}

@MainActor
protocol IntegrityPenaltyServicing {
    func penalties(
        domain: IntegrityDomain,
        status: IntegrityPenaltyStatus?,
        after: String?,
        limit: Int
    ) async throws -> IntegrityPenaltyPage
    func penalty(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow
    func revoke(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow
}

struct APIIntegrityPenaltyService: IntegrityPenaltyServicing {
    let client: APIClient

    func penalties(
        domain: IntegrityDomain,
        status: IntegrityPenaltyStatus?,
        after: String?,
        limit: Int
    ) async throws -> IntegrityPenaltyPage {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltiesResponse = try await client.send(
                .reportIntegrityPenalties(status: status, after: after, limit: limit)
            )
            return .init(results: response.results.map(IntegrityPenaltyRow.report), pageInfo: response.pageInfo)
        case .vote:
            let response: VoteIntegrityPenaltiesResponse = try await client.send(
                .voteIntegrityPenalties(status: status, after: after, limit: limit)
            )
            guard response.filterScope?.confirmsFlagOnly == true,
                  response.filterScope?.sourceFlagId == nil else {
                throw IntegrityPenaltyServiceError.unconfirmedVoteScope
            }
            return .init(results: response.results.map(IntegrityPenaltyRow.vote), pageInfo: .init(
                hasNextPage: response.pageInfo.hasNextPage,
                endCursor: response.pageInfo.endCursor,
                startCursor: response.pageInfo.startCursor
            ))
        }
    }

    func penalty(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltyEnvelope = try await client.send(.reportIntegrityPenalty(id: id))
            return .report(response.penalty)
        case .vote:
            let response: VoteIntegrityPenaltyEnvelope = try await client.send(.voteIntegrityPenalty(id: id))
            return .vote(response.penalty)
        }
    }

    func revoke(domain: IntegrityDomain, id: String) async throws -> IntegrityPenaltyRow {
        switch domain {
        case .report:
            let response: ReportIntegrityPenaltyRevokeResponse = try await client.send(
                .revokeReportIntegrityPenalty(id: id)
            )
            return .report(response.penalty)
        case .vote:
            let response: VoteIntegrityPenaltyEnvelope = try await client.send(.revokeVoteIntegrityPenalty(id: id))
            return .vote(response.penalty)
        }
    }
}

enum IntegrityPenaltyServiceError: Error {
    case unconfirmedVoteScope
}
