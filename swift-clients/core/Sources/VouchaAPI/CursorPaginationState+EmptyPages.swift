/// Limits shared by every native cursor list.
public enum CursorPaginationLimits {
    /// Consecutive pages that add no rows a list follows on its own while the server keeps
    /// answering `has_next_page: true`. After this many, the list stops and shows a manual
    /// "Load more" affordance, so a server that only returns empty pages cannot loop a client.
    public static let maxConsecutiveEmptyPages = 5
}

extension CursorPaginationState {
    /// True once a run of empty pages used the whole continuation budget while more remain.
    public var needsManualContinuation: Bool {
        hasMore && lastError == nil
            && consecutiveEmptyPages >= CursorPaginationLimits.maxConsecutiveEmptyPages
    }

    /// Starts the next page on its own when the last committed page added no rows but the server
    /// reported `has_next_page: true`. Returns nil on rows, errors, the end of the list, or once
    /// `CursorPaginationLimits.maxConsecutiveEmptyPages` empty pages have run in a row.
    public mutating func continueThroughEmptyPage() -> CursorPageRequest? {
        guard canAutomaticallyLoad, consecutiveEmptyPages > 0, !needsManualContinuation else { return nil }
        return beginPage()
    }
}
