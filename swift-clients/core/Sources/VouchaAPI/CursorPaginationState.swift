import Foundation
import VouchaCore

public enum LoadState: Sendable {
    case idle
    case loading
    case loaded
    case error(VouchaError)
}

public enum CursorPagePosition: Sendable {
    case append
    case prepend
}

public struct CursorPageRequest: Equatable, Sendable {
    public let cursor: String?
    fileprivate let generation: Int
    fileprivate let sequence: Int
}

/// Foundation-only state machine for a forward cursor list.
///
/// The caller owns transport and page-local sidecars. It should merge sidecars only when
/// `isCurrent(_:)` is true, then commit the page with `complete(...)` without suspending.
public struct CursorPaginationState<Item: Identifiable & Sendable>: Sendable
    where Item.ID: Hashable & Sendable {
    public private(set) var items: [Item]
    public private(set) var state: LoadState
    public private(set) var hasMore: Bool
    public private(set) var endCursor: String?

    private var generation = 0
    private var sequence = 0
    private var inFlightRequest: CursorPageRequest?
    private var loadedPage: Bool

    public init(items: [Item] = []) {
        self.items = Self.unique(items)
        state = .idle
        hasMore = !items.isEmpty
        loadedPage = !items.isEmpty
    }

    public var isLoading: Bool {
        inFlightRequest != nil
    }

    public var hasLoadedPage: Bool {
        loadedPage
    }

    public var lastError: VouchaError? {
        guard case let .error(error) = state else { return nil }
        return error
    }

    public var canAutomaticallyLoad: Bool {
        hasMore && !isLoading && lastError == nil
    }

    public mutating func beginInitialPageIfNeeded() -> CursorPageRequest? {
        guard case .idle = state else { return nil }
        return beginNextPage()
    }

    public mutating func beginNextPage() -> CursorPageRequest? {
        guard inFlightRequest == nil, hasMore || !loadedPage else { return nil }
        let requestSequence = sequence + 1
        sequence = requestSequence
        let request = CursorPageRequest(cursor: endCursor, generation: generation, sequence: requestSequence)
        inFlightRequest = request
        state = .loading
        return request
    }

    public func isCurrent(_ request: CursorPageRequest) -> Bool {
        guard let inFlightRequest else { return false }
        return inFlightRequest.cursor == request.cursor
            && inFlightRequest.generation == request.generation
            && inFlightRequest.sequence == request.sequence
            && request.generation == generation
    }

    @discardableResult
    public mutating func complete(
        _ request: CursorPageRequest,
        items newItems: [Item],
        endCursor: String?,
        hasNextPage: Bool,
        position: CursorPagePosition = .append
    ) -> Bool {
        guard isCurrent(request) else { return false }
        items = Self.merge(existing: items, incoming: newItems, position: position)
        self.endCursor = endCursor
        hasMore = hasNextPage
        loadedPage = true
        state = .loaded
        inFlightRequest = nil
        return true
    }

    @discardableResult
    public mutating func fail(_ request: CursorPageRequest, error: VouchaError) -> Bool {
        guard isCurrent(request) else { return false }
        state = .error(error)
        inFlightRequest = nil
        return true
    }

    @discardableResult
    public mutating func cancel(_ request: CursorPageRequest) -> Bool {
        guard isCurrent(request) else { return false }
        state = items.isEmpty ? .idle : .loaded
        inFlightRequest = nil
        return true
    }

    public mutating func reset(items: [Item] = []) {
        generation += 1
        self.items = Self.unique(items)
        state = .idle
        hasMore = !items.isEmpty
        endCursor = nil
        inFlightRequest = nil
        loadedPage = !items.isEmpty
    }

    public mutating func invalidateRequestsPreservingPage() {
        generation += 1
        inFlightRequest = nil
        state = loadedPage ? .loaded : .idle
    }

    public mutating func remove(where shouldRemove: (Item) -> Bool) {
        items.removeAll(where: shouldRemove)
    }

    public mutating func replaceItems(_ items: [Item]) {
        self.items = Self.unique(items)
    }

    /// Restores page metadata that was hydrated separately from the result rows.
    public mutating func restoreContinuation(endCursor: String?, hasMore: Bool) {
        self.endCursor = endCursor
        self.hasMore = hasMore
        loadedPage = true
        if inFlightRequest == nil {
            state = .loaded
        }
    }

    private static func unique(_ values: [Item]) -> [Item] {
        var seen = Set<Item.ID>()
        return values.filter { seen.insert($0.id).inserted }
    }

    private static func merge(
        existing: [Item],
        incoming: [Item],
        position: CursorPagePosition
    ) -> [Item] {
        let incoming = unique(incoming)
        let incomingIds = Set(incoming.map(\.id))
        switch position {
        case .append:
            let existingIds = Set(existing.map(\.id))
            return existing + incoming.filter { !existingIds.contains($0.id) }
        case .prepend:
            return incoming + existing.filter { !incomingIds.contains($0.id) }
        }
    }
}
