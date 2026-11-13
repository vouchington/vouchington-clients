public extension RSSFeedListViewModel {
    var isHackerNewsFilterVisible: Bool {
        hackerNewsReferencesVisible
    }

    /// Refresh the backend flag. A failed request keeps the last known value (hidden initially).
    func refreshHackerNewsReferencesVisibility() async {
        guard let visible = await HackerNewsReferencesVisibility.fetch(client: client) else { return }
        let wasVisible = hackerNewsReferencesVisible
        hackerNewsReferencesVisible = visible
        if !visible, hasHackerNewsReferences { hasHackerNewsReferences = false }
        // A selected filter now changes the effective query: restart from the first page.
        if visible, !wasVisible, hasHackerNewsReferences {
            hackerNewsFilterTask = Task { await reload() }
        }
    }
}
