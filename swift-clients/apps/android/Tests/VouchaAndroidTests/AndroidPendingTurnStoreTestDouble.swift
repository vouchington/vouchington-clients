import Foundation
@testable import VouchaAndroid

@MainActor
final class AndroidPendingTurnStoreTestDouble: AndroidPendingTurnStoring {
    enum Failure: Error { case save, delete, load, discard, conflict }

    var records: [String: [String: AndroidPendingChatTurn]] = [:]
    var failSave = false
    var failSaveAfterHold = false
    var failDelete = false
    var failLoad = false
    var failDiscardAfterPartialDelete = false
    var failDiscardKeyDeletion = false
    var holdNextDiscard = false
    var holdNextDelete = false
    var discardCalls: [String] = []
    var loadCalls = 0
    var ownerKeys: Set<String> = []
    var discardMarkers: Set<String> = []
    var holdNextLoad = false
    var savedBodies: [Data] = []
    var saveAttempts: [(ownerID: String, body: Data)] = []
    var holdSave = false
    private var saveEntered = false
    private var saveEnteredWaiter: CheckedContinuation<Void, Never>?
    private var saveRelease: CheckedContinuation<Void, Never>?
    private var loadEntered = false
    private var loadEnteredWaiter: CheckedContinuation<Void, Never>?
    private var loadRelease: CheckedContinuation<Void, Never>?
    private var discardEntered = false
    private var discardEnteredWaiter: CheckedContinuation<Void, Never>?
    private var discardRelease: CheckedContinuation<Void, Never>?
    private var deleteEntered = false
    private var deleteEnteredWaiter: CheckedContinuation<Void, Never>?
    private var deleteRelease: CheckedContinuation<Void, Never>?

    func waitForDeleteEntry() async {
        if deleteEntered { return }
        await withCheckedContinuation { deleteEnteredWaiter = $0 }
    }

    func releaseDelete() {
        deleteRelease?.resume()
        deleteRelease = nil
    }

    func waitForDiscardEntry() async {
        if discardEntered { return }
        await withCheckedContinuation { discardEnteredWaiter = $0 }
    }

    func releaseDiscard() {
        discardRelease?.resume()
        discardRelease = nil
    }

    func waitForLoadEntry() async {
        if loadEntered { return }
        await withCheckedContinuation { loadEnteredWaiter = $0 }
    }

    func releaseLoad() {
        loadRelease?.resume()
        loadRelease = nil
    }

    func waitForSaveEntry() async {
        if saveEntered { return }
        await withCheckedContinuation { saveEnteredWaiter = $0 }
    }

    func releaseSave() {
        holdSave = false
        saveRelease?.resume()
        saveRelease = nil
    }

    func load(ownerID: String) async throws -> [AndroidPendingChatTurn] {
        loadCalls += 1
        if failLoad || discardMarkers.contains(ownerID) { throw Failure.load }
        let snapshot = Array(records[ownerID, default: [:]].values)
        if holdNextLoad {
            holdNextLoad = false
            loadEntered = true
            loadEnteredWaiter?.resume()
            loadEnteredWaiter = nil
            await withCheckedContinuation { loadRelease = $0 }
        }
        return snapshot
    }

    func save(ownerID: String, turn: AndroidPendingChatTurn) async throws {
        saveAttempts.append((ownerID: ownerID, body: turn.requestBody))
        if discardMarkers.contains(ownerID) { throw Failure.discard }
        if failSave { throw Failure.save }
        if holdSave {
            saveEntered = true
            saveEnteredWaiter?.resume()
            saveEnteredWaiter = nil
            await withCheckedContinuation { saveRelease = $0 }
        }
        if failSaveAfterHold { throw Failure.save }
        if let existing = records[ownerID]?[turn.conversationID], existing != turn {
            throw Failure.conflict
        }
        records[ownerID, default: [:]][turn.conversationID] = turn
        ownerKeys.insert(ownerID)
        savedBodies.append(turn.requestBody)
    }

    func delete(ownerID: String, turn: AndroidPendingChatTurn) async throws {
        if discardMarkers.contains(ownerID) { throw Failure.discard }
        if failDelete { throw Failure.delete }
        if holdNextDelete {
            holdNextDelete = false
            await withCheckedContinuation {
                deleteRelease = $0
                deleteEntered = true
                deleteEnteredWaiter?.resume()
                deleteEnteredWaiter = nil
            }
        }
        guard records[ownerID]?[turn.conversationID] == turn else { throw Failure.conflict }
        records[ownerID]?.removeValue(forKey: turn.conversationID)
    }

    func discardOwner(ownerID: String) async throws {
        discardCalls.append(ownerID)
        if holdNextDiscard {
            holdNextDiscard = false
            discardEntered = true
            discardEnteredWaiter?.resume()
            discardEnteredWaiter = nil
            await withCheckedContinuation { discardRelease = $0 }
        }
        discardMarkers.insert(ownerID)
        if failDiscardAfterPartialDelete {
            if let first = records[ownerID]?.keys.sorted().first {
                records[ownerID]?.removeValue(forKey: first)
            }
            throw Failure.discard
        }
        records.removeValue(forKey: ownerID)
        if failDiscardKeyDeletion { throw Failure.discard }
        ownerKeys.remove(ownerID)
        discardMarkers.remove(ownerID)
    }
}
