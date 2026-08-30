import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

@Observable
@MainActor
final class NativeTopHashtagsViewModel {
    private struct CursorProvenance: Equatable {
        let query: String?
        let mapping: TopHashtagMapping
    }

    var query = ""
    var mapping: TopHashtagMapping = .all
    var results: [TopHashtag] = []
    var topics: [String: Topic] = [:]
    var pageInfo = Page<TopHashtag>.PageInfo(hasNextPage: false)
    var isLoading = false
    var error: VouchaError?

    let client: APIClient?
    private var activeFetch: Task<TopHashtagsResponse, Error>?
    private var fetchGeneration = 0

    init(client: APIClient?) {
        self.client = client
    }

    func reload() async {
        guard let client else { return }
        await load(client: client, after: nil, append: false, provenance: currentCursorProvenance)
    }

    func loadMore() async {
        guard let client,
              pageInfo.hasNextPage,
              let after = pageInfo.endCursor,
              let provenance = pageCursorProvenance
        else { return }
        await load(client: client, after: after, append: true, provenance: provenance)
    }

    private func load(
        client: APIClient,
        after: String?,
        append: Bool,
        provenance: CursorProvenance
    ) async {
        activeFetch?.cancel()
        fetchGeneration += 1
        let generation = fetchGeneration
        isLoading = true
        error = nil
        let request = Task<TopHashtagsResponse, Error> {
            try await client.send(.topHashtags(
                query: provenance.query,
                mapping: provenance.mapping,
                after: after
            ))
        }
        activeFetch = request
        do {
            let response = try await request.value
            guard generation == fetchGeneration else { return }
            if append {
                let existingIds = Set(results.map(\.id))
                results += response.results.filter { !existingIds.contains($0.id) }
                topics.merge(response.topics) { _, replacement in replacement }
            } else {
                results = response.results
                topics = response.topics
            }
            pageInfo = response.pageInfo
            pageCursorProvenance = provenance
            error = nil
        } catch is CancellationError {
        } catch let error as VouchaError {
            if generation == fetchGeneration {
                self.error = error
            }
        } catch {
            if generation == fetchGeneration {
                self.error = .api(statusCode: 0, preconditionCode: nil)
            }
        }
        if generation == fetchGeneration {
            isLoading = false
            activeFetch = nil
        }
    }

    private var currentCursorProvenance: CursorProvenance {
        CursorProvenance(query: query.trimmedOrNil, mapping: mapping)
    }

    @ObservationIgnored
    private var pageCursorProvenance: CursorProvenance?

    func link(alias: TopHashtag, to topicIdentifier: String) async {
        guard let client, !topicIdentifier.trimmed.isEmpty else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(
                .linkTopicAlias(topicId: topicIdentifier.trimmed, aliasId: alias.topicAliasId)
            )
        }
    }

    func unlink(alias: TopHashtag) async {
        guard let client, let topicId = alias.topicId else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(
                .deleteTopicAlias(topicId: topicId, aliasId: alias.topicAliasId)
            )
        }
    }

    func createTopic(alias: TopHashtag, name: String) async {
        guard let client, !name.trimmed.isEmpty,
              let slug = CanonicalHashtagSlug(rawValue: alias.hashtag)
        else { return }
        await mutate {
            let _: TopicEnvelope = try await client.send(.createTopic(body: .init(
                name: name.trimmed,
                slug: slug.value,
                sourceTopicAliasId: alias.topicAliasId
            )))
        }
    }

    private func mutate(operation: () async throws -> Void) async {
        guard !isLoading else { return }
        isLoading = true
        defer { isLoading = false }
        do {
            try await operation()
            await reloadAfterMutation()
        } catch let error as VouchaError {
            self.error = error
        } catch {
            self.error = .api(statusCode: 0, preconditionCode: nil)
        }
    }

    private func reloadAfterMutation() async {
        isLoading = false
        await reload()
        isLoading = true
    }
}
