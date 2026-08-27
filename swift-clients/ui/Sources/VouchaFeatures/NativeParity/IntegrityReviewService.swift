import VouchaAPI
import VouchaModels

enum IntegrityStatusFilter: String, CaseIterable {
    case pending, resolved, all

    var apiStatus: IntegrityFlagStatus? {
        switch self {
        case .pending: .pending
        case .resolved: .resolved
        case .all: nil
        }
    }

}

@MainActor
protocol ReportIntegrityServicing {
    func flags(
        status: IntegrityFlagStatus?,
        after: String?,
        limit: Int
    ) async throws -> ReportIntegrityFlagsResponse
    func flag(id: String) async throws -> ReportIntegrityFlag
    func dismiss(flagId: String) async throws -> ReportIntegrityFlag
    func penalizeReporters(flagId: String) async throws -> ReportIntegrityPenaltyResponse
}

@MainActor
protocol VoteIntegrityServicing {
    func flags(
        status: IntegrityFlagStatus?,
        after: String?,
        limit: Int
    ) async throws -> VoteIntegrityFlagsResponse
    func flag(id: String) async throws -> VoteIntegrityFlag
    func resolve(flagId: String, resolution: VoteIntegrityResolution) async throws -> VoteIntegrityFlag
    func applyPenalty(flagId: String) async throws -> VoteIntegrityPenaltyResponse
    func penaltyIds(flagId: String) async throws -> Set<String>
}

enum IntegrityReviewServiceError: Error {
    case unconfirmedVotePenaltyScope
    case incompleteVotePenaltySnapshot
}

struct APIReportIntegrityService: ReportIntegrityServicing {
    let client: APIClient

    func flags(
        status: IntegrityFlagStatus?,
        after: String?,
        limit: Int
    ) async throws -> ReportIntegrityFlagsResponse {
        try await client.send(.reportIntegrityFlags(status: status, after: after, limit: limit))
    }

    func flag(id: String) async throws -> ReportIntegrityFlag {
        let response: ReportIntegrityFlagEnvelope = try await client.send(.reportIntegrityFlag(id: id))
        return response.flag
    }

    func dismiss(flagId: String) async throws -> ReportIntegrityFlag {
        let response: ReportIntegrityFlagEnvelope = try await client.send(
            .dismissReportIntegrityFlag(id: flagId)
        )
        return response.flag
    }

    func penalizeReporters(flagId: String) async throws -> ReportIntegrityPenaltyResponse {
        try await client.send(.applyReportIntegrityPenalty(flagId: flagId))
    }
}

struct APIVoteIntegrityService: VoteIntegrityServicing {
    let client: APIClient

    func flags(
        status: IntegrityFlagStatus?,
        after: String?,
        limit: Int
    ) async throws -> VoteIntegrityFlagsResponse {
        try await client.send(.voteIntegrityFlags(status: status, after: after, limit: limit))
    }

    func flag(id: String) async throws -> VoteIntegrityFlag {
        let response: VoteIntegrityFlagEnvelope = try await client.send(.voteIntegrityFlag(id: id))
        return response.flag
    }

    func resolve(flagId: String, resolution: VoteIntegrityResolution) async throws -> VoteIntegrityFlag {
        let response: VoteIntegrityFlagEnvelope = try await client.send(
            .resolveVoteIntegrityFlag(id: flagId, resolution: resolution)
        )
        return response.flag
    }

    func applyPenalty(flagId: String) async throws -> VoteIntegrityPenaltyResponse {
        try await client.send(.applyVoteIntegrityPenalty(flagId: flagId))
    }

    func penaltyIds(flagId: String) async throws -> Set<String> {
        var ids: Set<String> = []
        var after: String?
        repeat {
            let response: VoteIntegrityPenaltiesResponse = try await client.send(
                .voteIntegrityPenalties(sourceFlagId: flagId, after: after, limit: 100)
            )
            guard response.filterScope?.confirmsFlagOnly == true,
                  response.filterScope?.sourceFlagId == flagId else {
                throw IntegrityReviewServiceError.unconfirmedVotePenaltyScope
            }
            ids.formUnion(response.results.map(\.id))
            guard response.pageInfo.hasNextPage else { return ids }
            guard let next = response.pageInfo.endCursor, next != after else {
                throw IntegrityReviewServiceError.incompleteVotePenaltySnapshot
            }
            after = next
        } while true
    }
}
