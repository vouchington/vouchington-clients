import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func voteUserTag(relationId: String, choice: ElectionVoteChoice?) async {
        guard let client, !detailRelationIsSelfProfile else { return }
        guard inFlightUserTagVoteIds.insert(relationId).inserted else { return }
        defer { inFlightUserTagVoteIds.remove(relationId) }
        do {
            if let choice {
                let _: EmptyResponse = try await client.send(.voteEntityRelation(
                    relationId: relationId,
                    choice: choice
                ))
            } else {
                let _: EmptyResponse = try await client.send(.clearEntityRelationVote(relationId: relationId))
            }
            try await loadUserTags(client: client)
        } catch {
            // Preserve the last successfully loaded profile tags.
        }
    }

    func isVotingUserTag(relationId: String) -> Bool {
        inFlightUserTagVoteIds.contains(relationId)
    }

    func reloadUserTags() async {
        guard let client else { return }
        try? await loadUserTags(client: client)
    }

    func loadUserTags(client: APIClient) async throws {
        guard let userId = detailRelationEntityId, !detailRelationIsSelfProfile else { return }
        detailUserTagPagination.reset()
        guard let request = detailUserTagPagination.beginInitialPageIfNeeded() else { return }
        let response: EntityRelationsResponse
        do {
            response = try await client.send(.entityRelations(
                entityType: "user",
                entityId: userId,
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 25,
                after: request.cursor,
                positiveNetVoteScore: true
            ))
        } catch {
            if error is CancellationError || Task.isCancelled {
                detailUserTagPagination.cancel(request)
            } else {
                detailUserTagPagination.fail(
                    request,
                    error: error as? VouchaError ?? .unexpected(error.localizedDescription)
                )
            }
            throw error
        }
        guard detailUserTagPagination.isCurrent(request) else { return }
        let page = response.results.compactMap { response.entityRelations[$0.id] }
        detailUserTagVotes = response.electionVotes ?? [:]
        detailUserTagPagination.complete(
            request,
            items: page,
            endCursor: response.pageInfo.endCursor,
            hasNextPage: response.pageInfo.hasNextPage
        )
        detailUserTags = detailUserTagPagination.items
    }

    func loadMoreUserTags() async {
        guard let client, let userId = detailRelationEntityId,
              let request = detailUserTagPagination.beginNextPage() else { return }
        do {
            let response: EntityRelationsResponse = try await client.send(.entityRelations(
                entityType: "user",
                entityId: userId,
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 25,
                after: request.cursor,
                positiveNetVoteScore: true
            ))
            guard detailUserTagPagination.isCurrent(request) else { return }
            let page = response.results.compactMap { response.entityRelations[$0.id] }
            detailUserTagVotes.merge(response.electionVotes ?? [:]) { _, new in new }
            detailUserTagPagination.complete(
                request,
                items: page,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            detailUserTags = detailUserTagPagination.items
        } catch is CancellationError {
            detailUserTagPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                detailUserTagPagination.cancel(request)
                return
            }
            detailUserTagPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                detailUserTagPagination.cancel(request)
                return
            }
            detailUserTagPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }
}
