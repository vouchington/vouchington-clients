import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeTagManagementViewModel {
    var subjectEntityType: String {
        switch subjectKind {
        case .post: "post"
        case .topic: "topic"
        case .rssFeedItem: "rss_feed_item"
        case .user: "user"
        }
    }

    func loadRelations(client: APIClient, config: NativeTagRelationTab? = nil) async throws {
        guard let config = config ?? activeTabConfig else {
            relations = []
            relationPagination.reset()
            electionVotes = [:]
            return
        }
        relationPagination.reset()
        guard let request = relationPagination.beginInitialPageIfNeeded() else { return }
        let response: EntityRelationsResponse
        do {
            response = try await client.send(
                .entityRelations(
                    entityType: subjectEntityType,
                    entityId: subjectId,
                    predicate: config.predicate,
                    objectType: config.objectType,
                    sort: "best",
                    limit: 25,
                    after: request.cursor
                )
            )
        } catch {
            if error is CancellationError || Task.isCancelled {
                relationPagination.cancel(request)
            } else {
                relationPagination.fail(
                    request,
                    error: error as? VouchaError ?? .unexpected(error.localizedDescription)
                )
            }
            throw error
        }
        guard relationPagination.isCurrent(request) else { return }
        let page = response.results.compactMap { response.entityRelations[$0.id] }
        electionVotes = response.electionVotes ?? [:]
        relationPagination.complete(
            request,
            items: page,
            endCursor: response.pageInfo.endCursor,
            hasNextPage: response.pageInfo.hasNextPage
        )
        relations = relationPagination.items
    }

    func loadMoreRelations() async {
        guard let client, let config = activeTabConfig,
              let request = relationPagination.beginNextPage() else { return }
        do {
            let response: EntityRelationsResponse = try await client.send(.entityRelations(
                entityType: subjectEntityType,
                entityId: subjectId,
                predicate: config.predicate,
                objectType: config.objectType,
                sort: "best",
                limit: 25,
                after: request.cursor
            ))
            guard relationPagination.isCurrent(request) else { return }
            let page = response.results.compactMap { response.entityRelations[$0.id] }
            electionVotes.merge(response.electionVotes ?? [:]) { _, new in new }
            relationPagination.complete(
                request,
                items: page,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            relations = relationPagination.items
        } catch is CancellationError {
            relationPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                relationPagination.cancel(request)
                return
            }
            relationPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                relationPagination.cancel(request)
                return
            }
            relationPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    func loadPublisherTypes(client: APIClient) async throws {
        let response: PublisherTypesResponse = try await client.send(.publisherTypes())
        publisherTypes = response.publisherTypes
    }

    func loadUserTags(client: APIClient) async throws {
        let response: UserTagsResponse = try await client.send(.userTags())
        publisherTypes = response.userTags
    }

    func addSelectedResult(id: String) async {
        guard let client, let config = activeTabConfig, !subjectId.isEmpty else { return }
        let mutationKey = "add:\(id)"
        guard inFlightMutationKeys.insert(mutationKey).inserted else { return }
        defer { inFlightMutationKeys.remove(mutationKey) }
        do {
            let _: EntityRelationResponse = try await client.send(
                .createEntityRelation(
                    entityType: subjectEntityType,
                    entityId: subjectId,
                    predicate: config.predicate,
                    objectType: config.objectType,
                    objectId: id
                )
            )
            try await loadRelations(client: client, config: config)
            searchQuery = ""
            searchResults = []
        } catch let error as VouchaError {
            if error.isTagLimitReached {
                tagLimitReached = true
            } else {
                state = .error(error)
            }
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func addSelectedPublisherType(id: String) async {
        guard let client, !subjectId.isEmpty else { return }
        let mutationKey = "add:\(id)"
        guard inFlightMutationKeys.insert(mutationKey).inserted else { return }
        defer { inFlightMutationKeys.remove(mutationKey) }
        do {
            let _: EntityRelationResponse = try await client.send(
                .createEntityRelation(
                    entityType: subjectEntityType,
                    entityId: subjectId,
                    predicate: activeTabConfig?.predicate ?? "publisher_type",
                    objectType: activeTabConfig?.objectType ?? "topic",
                    objectId: id
                )
            )
            if let config = activeTabConfig {
                try await loadRelations(client: client, config: config)
            }
            publisherTypeSelection = ""
        } catch let error as VouchaError {
            if error.isTagLimitReached {
                tagLimitReached = true
            } else {
                state = .error(error)
            }
            publisherTypeSelection = ""
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            publisherTypeSelection = ""
        }
    }

    func vote(relationId: String, choice: ElectionVoteChoice?) async {
        guard let client else { return }
        let mutationKey = "vote:\(relationId)"
        guard inFlightMutationKeys.insert(mutationKey).inserted else { return }
        defer { inFlightMutationKeys.remove(mutationKey) }
        do {
            if let choice {
                let _: EmptyResponse = try await client.send(.voteEntityRelation(
                    relationId: relationId,
                    choice: choice
                ))
            } else {
                let _: EmptyResponse = try await client.send(.clearEntityRelationVote(relationId: relationId))
            }
            if let config = activeTabConfig {
                try await loadRelations(client: client, config: config)
            }
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }
}
