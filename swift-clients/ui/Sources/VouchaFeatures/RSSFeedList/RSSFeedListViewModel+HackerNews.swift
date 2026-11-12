public extension RSSFeedListViewModel {
    var isHackerNewsFilterVisible: Bool {
        hackerNewsReferencesVisible
    }

    /// Refresh the backend flag. A failed request keeps the last known value (hidden initially).
    func refreshHackerNewsReferencesVisibility() async {
        guard let visible = await HackerNewsReferencesVisibility.fetch(client: client) else { return }
        hackerNewsReferencesVisible = visible
        if !visible, hasHackerNewsReferences { hasHackerNewsReferences = false }
    }
}
