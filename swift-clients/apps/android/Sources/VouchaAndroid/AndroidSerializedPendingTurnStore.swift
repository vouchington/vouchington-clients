import Foundation

/// Keeps calls for one owner in invocation order while allowing other owners to load independently.
@MainActor
final class AndroidSerializedPendingTurnStore: AndroidPendingTurnStoring {
    private let backing: any AndroidPendingTurnStoring
    private var busyOwners: Set<String> = []
    private var waitingByOwner: [String: [CheckedContinuation<Void, Never>]] = [:]

    init(_ backing: any AndroidPendingTurnStoring) {
        self.backing = backing
    }

    func load(ownerID: String) async throws -> [AndroidPendingChatTurn] {
        await enter(ownerID)
        defer { leave(ownerID) }
        return try await backing.load(ownerID: ownerID)
    }

    func save(ownerID: String, turn: AndroidPendingChatTurn) async throws {
        await enter(ownerID)
        defer { leave(ownerID) }
        try await backing.save(ownerID: ownerID, turn: turn)
    }

    func delete(ownerID: String, turn: AndroidPendingChatTurn) async throws {
        await enter(ownerID)
        defer { leave(ownerID) }
        try await backing.delete(ownerID: ownerID, turn: turn)
    }

    func discardOwner(ownerID: String) async throws {
        await enter(ownerID)
        defer { leave(ownerID) }
        try await backing.discardOwner(ownerID: ownerID)
    }

    private func enter(_ ownerID: String) async {
        if busyOwners.insert(ownerID).inserted { return }
        await withCheckedContinuation { continuation in
            waitingByOwner[ownerID, default: []].append(continuation)
        }
    }

    private func leave(_ ownerID: String) {
        if var waiters = waitingByOwner[ownerID], !waiters.isEmpty {
            let continuation = waiters.removeFirst()
            if waiters.isEmpty {
                waitingByOwner.removeValue(forKey: ownerID)
            } else {
                waitingByOwner[ownerID] = waiters
            }
            continuation.resume()
        } else {
            busyOwners.remove(ownerID)
        }
    }
}
