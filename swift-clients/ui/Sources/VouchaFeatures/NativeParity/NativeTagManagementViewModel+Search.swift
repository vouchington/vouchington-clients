import VouchaAPI
import VouchaCore

extension NativeTagManagementViewModel {
    func search() async {
        guard let client, let config = activeTabConfig else {
            resetSearch()
            return
        }
        if config.predicate == "publisher_type" {
            resetSearch()
            return
        }
        let query = searchQuery.trimmed
        guard !query.isEmpty else {
            resetSearch()
            return
        }
        if config.objectType == "url", query.count < 3 {
            resetSearch()
            return
        }

        searchState = .loading
        do {
            let response: NativeGenericListResponse = try await client.send(
                searchEndpoint(query: query, objectType: config.objectType)
            )
            let excludedIds = Set(relations.compactMap(\.objectId))
            searchResults = response.results
                .map(response.hydratedEntity(for:))
                .filter { !excludedIds.contains($0.id) }
            searchState = .loaded
        } catch {
            searchResults = []
            searchState = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func resetSearch() {
        searchResults = []
        searchState = .idle
    }

    func clearSearch() {
        searchQuery = ""
        resetSearch()
    }

    func searchEndpoint(query: String, objectType: String) -> Endpoint {
        switch objectType {
        case "topic":
            .topics(query: query, limit: 10)
        case "post":
            .posts(query: query, limit: 10)
        case "url":
            .urls(query: query, limit: 10)
        default:
            .topics(query: query, limit: 10)
        }
    }
}
