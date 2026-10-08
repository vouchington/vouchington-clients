import VouchaAPI
import VouchaModels

/// Mirrors `DirectMessagesViewModel`'s bounded page-following: caps fan-out from a single
/// autocomplete lookup while still following `page_info` instead of silently truncating at
/// page one.
private let userAutocompletePageLimit = 8
private let userAutocompleteMaxAdditionalPages = 4

extension NativeMarkdownEditor {
    func scheduleAutocomplete(for value: String) {
        autocompleteTask?.cancel()
        guard let token = MarkdownAutocompleteToken.parse(value) else {
            autocompleteToken = nil
            suggestions = []
            return
        }

        autocompleteToken = token
        autocompleteTask = Task {
            try? await Task.sleep(nanoseconds: 180_000_000)
            guard !Task.isCancelled else { return }
            let loaded = await loadSuggestions(for: token)
            guard !Task.isCancelled else { return }
            await MainActor.run {
                guard autocompleteToken == token else { return }
                suggestions = loaded
            }
        }
    }

    func loadSuggestions(for token: MarkdownAutocompleteToken) async -> [MarkdownAutocompleteSuggestion] {
        guard let client else { return [] }
        do {
            switch token.kind {
            case .user:
                let users = await matchingMarkdownUsers(query: token.query, client: client)
                return users.compactMap { user in
                    guard let username = user.username?.ifNotEmpty else { return nil }
                    return MarkdownAutocompleteSuggestion(
                        replacement: "@\(username)",
                        label: "@\(username)",
                        detail: user.name
                    )
                }
            case .topic:
                let response: TopicSearchResponse = try await client.send(.topics(query: token.query, limit: 8))
                return response.results.compactMap { result in
                    guard let slug = result.slug else { return nil }
                    return MarkdownAutocompleteSuggestion(
                        replacement: "#\(slug)",
                        label: "#\(slug)",
                        detail: result.name
                    )
                }
            case .post:
                let response: NativeMarkdownPostSearchResponse = try await client.send(.posts(
                    query: token.query,
                    limit: 8
                ))
                return response.results.compactMap { result in
                    guard let post = response.posts[result.entityId ?? result.id] else { return nil }
                    let key = post.slug ?? post.id
                    return MarkdownAutocompleteSuggestion(
                        replacement: "!\(key)",
                        label: "!\(key)",
                        detail: post.title
                    )
                }
            }
        } catch {
            return []
        }
    }

    /// Follows `page_info.has_next_page` until there are enough matches, the result set is
    /// exhausted, or the page-count safety bound is hit. Swallows failures so a mid-loop error
    /// still returns whatever matches were already found.
    private func matchingMarkdownUsers(query: String, client: APIClient) async -> [PublicUser] {
        var matches: [PublicUser] = []
        var after: String?
        for _ in 0 ... userAutocompleteMaxAdditionalPages {
            guard !Task.isCancelled,
                  let response: Page<PublicUser> = try? await client.send(
                      .myMessageUserSearch(query: query, after: after, limit: userAutocompletePageLimit)
                  )
            else { return matches }
            matches.append(contentsOf: response.results)
            guard matches.count < userAutocompletePageLimit,
                  response.pageInfo.hasNextPage,
                  let endCursor = response.pageInfo.endCursor else {
                break
            }
            after = endCursor
        }
        return matches
    }

    func insert(_ suggestion: MarkdownAutocompleteSuggestion) {
        guard let token = autocompleteToken else { return }
        markdown = Self.insertedMarkdown(markdown, suggestion: suggestion, token: token)
        autocompleteToken = nil
        suggestions = []
    }

    static func insertedMarkdown(
        _ markdown: String,
        suggestion: MarkdownAutocompleteSuggestion,
        token: MarkdownAutocompleteToken
    ) -> String {
        let prefix = String(markdown[..<token.range.lowerBound])
        let suffix = String(markdown[token.range.upperBound...])
        return "\(prefix)\(suggestion.replacement) \(suffix)"
    }
}
