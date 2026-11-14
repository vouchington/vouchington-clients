import Observation

@Observable @MainActor
final class AndroidAccountReplacementCoordinator {
    private(set) var isReplacing = false

    func perform(
        operation: @MainActor () async -> Bool,
        isCurrent: @MainActor () -> Bool,
        replace: @MainActor () async -> Void
    ) async {
        guard !isReplacing, isCurrent() else { return }
        isReplacing = true
        defer { isReplacing = false }
        guard await operation(), isCurrent() else { return }
        await replace()
    }
}
