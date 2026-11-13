public extension PostsListViewModel {
    var isHackerNewsFilterVisible: Bool {
        supportsHackerNewsFilter && hackerNewsReferencesVisible
    }

    /// Refresh the backend flag. A failed request keeps the last known value (hidden initially).
    func refreshHackerNewsReferencesVisibility() async {
        // Overlapping refreshes (initial load and pull-to-refresh) can finish out of order.
        hackerNewsConfigGeneration += 1
        let generation = hackerNewsConfigGeneration
        guard let visible = await HackerNewsReferencesVisibility.fetch(client: client) else { return }
        guard generation == hackerNewsConfigGeneration else { return }
        let wasVisible = hackerNewsReferencesVisible
        hackerNewsReferencesVisible = visible
        if !visible, hasHackerNewsReferences { hasHackerNewsReferences = false }
        // A selected filter (for example from a route query) now changes the effective query:
        // restart from the first page so the unfiltered cursor is never reused.
        if visible, !wasVisible, hasHackerNewsReferences, supportsHackerNewsFilter {
            filterReloadTask = Task { await reload() }
        }
    }

    /// User-initiated first-page load (pull-to-refresh or retry). Also retries the Hacker News
    /// flag so a failed initial fetch does not hide the filter forever; the two run independently,
    /// so an unavailable config never blocks the list.
    func refresh() async {
        async let config: Void = refreshHackerNewsReferencesVisibility()
        await reload()
        await config
    }
}
