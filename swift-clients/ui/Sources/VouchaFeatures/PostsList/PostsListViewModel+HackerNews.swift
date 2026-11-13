public extension PostsListViewModel {
    var isHackerNewsFilterVisible: Bool {
        supportsHackerNewsFilter && hackerNewsReferencesVisible
    }

    /// Refresh the backend flag. A failed request keeps the last known value (hidden initially).
    func refreshHackerNewsReferencesVisibility() async {
        guard let visible = await HackerNewsReferencesVisibility.fetch(client: client) else { return }
        let wasVisible = hackerNewsReferencesVisible
        hackerNewsReferencesVisible = visible
        if !visible, hasHackerNewsReferences { hasHackerNewsReferences = false }
        // A selected filter (for example from a route query) now changes the effective query:
        // restart from the first page so the unfiltered cursor is never reused.
        if visible, !wasVisible, hasHackerNewsReferences, supportsHackerNewsFilter {
            filterReloadTask = Task { await reload() }
        }
    }
}
