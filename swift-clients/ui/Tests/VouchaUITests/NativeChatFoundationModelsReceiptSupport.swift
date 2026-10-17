import Foundation
@testable import VouchaFeatures

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class ReceiptAdmission: NativeChatFoundationModelsAdmitting {
        var continuationFits = true
        private var retentionHold: CheckedContinuation<Void, Never>?
        var holdRetention = false
        var holdFinalCount = false
        var onFinalCount: (() -> Void)?
        private var finalCountHold: CheckedContinuation<Void, Never>?
        private var prepareHold: CheckedContinuation<Void, Never>?
        var holdPreparationCall: Int?
        var onRetain: ((String) -> Void)?
        var onPrepared: (() -> Void)?
        var retained: [String] = []
        var prepareCalls = 0

        func prepare(name _: String, arguments _: String) async throws -> String {
            prepareCalls += 1
            let id = UUID().uuidString
            onPrepared?()
            if holdPreparationCall == prepareCalls { await withCheckedContinuation { prepareHold = $0 } }
            return id
        }

        func retain(callId _: String, name _: String, envelope: String) async throws -> Bool {
            retained.append(envelope)
            onRetain?(envelope)
            if holdRetention { await withCheckedContinuation { retentionHold = $0 } }
            return continuationFits
        }

        func releaseRetention() {
            retentionHold?.resume()
            retentionHold = nil
        }

        func releasePreparation() {
            prepareHold?.resume()
            prepareHold = nil
        }

        func finalTranscriptFits() async throws -> Bool {
            onFinalCount?()
            if holdFinalCount { await withCheckedContinuation { finalCountHold = $0 } }
            return continuationFits
        }

        func releaseFinalCount() {
            finalCountHold?.resume()
            finalCountHold = nil
        }
    }

    extension NativeChatMCPTurnContext {
        @MainActor
        static func fixture(
            _ snapshot: NativeChatMCPAgentSession.ConnectedSnapshot,
            isCurrent: @escaping @MainActor @Sendable () -> Bool = { true }
        ) -> Self {
            .init(owner: .init(
                accountId: snapshot.accountId, revision: snapshot.revision,
                conversationId: "conversation-1", userMessageId: "local-user-1"
            ), isCurrent: isCurrent, publish: { _ in isCurrent() })
        }
    }
#endif
