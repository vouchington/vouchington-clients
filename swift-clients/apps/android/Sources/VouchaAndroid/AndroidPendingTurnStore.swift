import Foundation
import SkipFuse

@MainActor
protocol AndroidPendingTurnStoring {
    func load(ownerID: String) async throws -> [AndroidPendingChatTurn]
    func save(ownerID: String, turn: AndroidPendingChatTurn) async throws
    func delete(ownerID: String, turn: AndroidPendingChatTurn) async throws
    func discardOwner(ownerID: String) async throws
}

@MainActor
final class AndroidPendingTurnStore: AndroidPendingTurnStoring {
    #if !os(Android)
        private var records: [String: [String: AndroidPendingChatTurn]] = [:]
    #endif

    func load(ownerID: String) async throws -> [AndroidPendingChatTurn] {
        #if os(Android)
            let encoded: String = try await Task.detached {
                try D.voucha.android.PendingTurnStoreBridge.INSTANCE().load(ownerID) ?? ""
            }.value
            guard let data = encoded.data(using: .utf8)
            else { throw AndroidPendingTurnStoreError.invalidBridgeResponse }
            let entries = try JSONDecoder().decode([AndroidPendingTurnStoreEntry].self, from: data)
            return try entries.map { entry in
                guard let body = Data(base64Encoded: entry.body) else {
                    throw AndroidPendingTurnStoreError.invalidBridgeResponse
                }
                return try AndroidPendingChatTurn(conversationID: entry.conversationID, requestBody: body)
            }
        #else
            return Array(records[ownerID, default: [:]].values)
        #endif
    }

    func save(ownerID: String, turn: AndroidPendingChatTurn) async throws {
        #if os(Android)
            let conversationID = turn.conversationID
            let body = turn.requestBody.base64EncodedString()
            try await Task.detached {
                let _: Void = try D.voucha.android.PendingTurnStoreBridge.INSTANCE().save(ownerID, conversationID, body)
            }.value
        #else
            if let old = records[ownerID]?[turn.conversationID], old != turn {
                throw AndroidPendingTurnStoreError.conflictingTurn
            }
            records[ownerID, default: [:]][turn.conversationID] = turn
        #endif
    }

    func delete(ownerID: String, turn: AndroidPendingChatTurn) async throws {
        #if os(Android)
            let conversationID = turn.conversationID
            let body = turn.requestBody.base64EncodedString()
            try await Task.detached {
                let _: Void = try D.voucha.android.PendingTurnStoreBridge.INSTANCE().delete(
                    ownerID,
                    conversationID,
                    body
                )
            }.value
        #else
            guard records[ownerID]?[turn.conversationID] == turn else {
                throw AndroidPendingTurnStoreError.conflictingTurn
            }
            records[ownerID]?.removeValue(forKey: turn.conversationID)
        #endif
    }

    func discardOwner(ownerID: String) async throws {
        #if os(Android)
            try await Task.detached {
                let _: Void = try D.voucha.android.PendingTurnStoreBridge.INSTANCE().discardOwner(ownerID)
            }.value
        #else
            records.removeValue(forKey: ownerID)
        #endif
    }
}

private struct AndroidPendingTurnStoreEntry: Decodable {
    let conversationID: String
    let body: String
}

enum AndroidPendingTurnStoreError: Error {
    case invalidBridgeResponse
    case conflictingTurn
}
