import Foundation
@testable import VouchaAPI

enum LifecycleScenarioPaginationAdapter {
    static func run(_ input: LifecycleScenarioInput) throws -> LifecycleObservation {
        let itemIds = try input.preconditions.strings("itemIds")
        let cursor = try input.preconditions.optionalString("nextCursor")
        let action = try input.action.string("type")
        var state = CursorPaginationState(items: itemIds.map(LifecyclePageItem.init))
        state.restoreContinuation(endCursor: cursor, hasMore: cursor != nil)

        switch action {
        case "load-more":
            let request = try require(state.beginNextPage(), "Expected a load-more request")
            if input.serverOutcome["error"] != nil {
                _ = state.fail(request, error: .network(URLError(.notConnectedToInternet)))
            } else {
                let incoming = try input.serverOutcome.strings("itemIds").map(LifecyclePageItem.init)
                let next = try input.serverOutcome.optionalString("nextCursor")
                _ = state.complete(request, items: incoming, endCursor: next, hasNextPage: next != nil)
            }
        case "load-more-twice":
            let started = [state.beginNextPage(), state.beginNextPage()].compactMap { $0 }.count
            return observation(
                visible: ["requestsStarted": .number(Double(started)), "loading": .bool(state.isLoading)],
                actions: [], strategy: "single-flight"
            )
        case "reset-during-load":
            let stale = try require(state.beginNextPage(), "Expected a stale request")
            let freshItems = try input.serverOutcome.strings("resetItemIds").map(LifecyclePageItem.init)
            let freshCursor = try input.serverOutcome.optionalString("nextCursor")
            state.reset(items: freshItems)
            state.restoreContinuation(endCursor: freshCursor, hasMore: freshCursor != nil)
            if input.serverOutcome["staleError"] != nil {
                _ = state.fail(stale, error: .network(URLError(.notConnectedToInternet)))
            } else {
                let staleItems = try input.serverOutcome.strings("staleItemIds").map(LifecyclePageItem.init)
                _ = state.complete(stale, items: staleItems, endCursor: nil, hasNextPage: false)
            }
        case "remove":
            let removedId = try input.action.string("itemId")
            state.remove { $0.id == removedId }
        case "cancel-load-more":
            let request = try require(state.beginNextPage(), "Expected a cancellable request")
            _ = state.cancel(request)
        default:
            throw LifecycleScenarioError.invalid("Unknown pagination action \(action)")
        }

        var visible: [String: LifecycleJSON] = [
            "itemIds": .array(state.items.map { .string($0.id) }),
            "nextCursor": state.endCursor.map(LifecycleJSON.string) ?? .null
        ]
        if input.serverOutcome["error"] != nil || action.contains("reset") || action.contains("cancel") {
            visible["error"] = state.lastError == nil ? .null : .string("network")
        }
        let actions = state.lastError == nil
            ? (state.canAutomaticallyLoad ? ["load-more"] : [])
            : ["retry"]
        let strategy = switch action {
        case "reset-during-load": "discard-stale-generation"
        case "remove": "preserve-continuation"
        case "cancel-load-more": "preserve-state"
        case "load-more" where input.serverOutcome["error"] != nil: "preserve-and-retry"
        default: "deduplicate-by-id"
        }
        let cancellation = action == "cancel-load-more"
            ? ["behavior": LifecycleJSON.string("preserve-items-and-cursor")]
            : lifecycleNotApplicable
        return LifecycleObservation(
            visibleState: visible, availableActions: actions,
            reconciliation: ["strategy": .string(strategy)], cancellation: cancellation
        )
    }

    private static func observation(
        visible: [String: LifecycleJSON], actions: [String], strategy: String
    ) -> LifecycleObservation {
        LifecycleObservation(
            visibleState: visible, availableActions: actions,
            reconciliation: ["strategy": .string(strategy)], cancellation: lifecycleNotApplicable
        )
    }

    private static func require<Value>(_ value: Value?, _ message: String) throws -> Value {
        guard let value else { throw LifecycleScenarioError.invalid(message) }
        return value
    }
}

private struct LifecyclePageItem: Identifiable {
    let id: String
}
