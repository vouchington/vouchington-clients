import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    @MainActor
    final class NativeChatFoundationModelsToolState {
        private(set) var attempted = false
        private var callCount = 0

        func checkCapacity() throws {
            guard callCount < 4 else { throw LocalLLMToolLoopFailure.partialExecution }
        }

        func markStarted() {
            callCount += 1
            attempted = true
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    struct NativeChatFoundationModelsTool: Tool {
        static let maximumModelResultBytes = 800

        let name: String
        let description: String
        let parameters: GenerationSchema
        let bridge: NativeChatMCPToolBridge
        let session: NativeChatMCPAgentSession
        let snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
        let state: NativeChatFoundationModelsToolState

        func call(arguments: GeneratedContent) async throws -> String {
            let raw = try Self.jsonArguments(arguments)
            try bridge.validate(name: name, rawArguments: raw)
            guard await session.isCurrent(snapshot) else { throw CancellationError() }
            let result = try await bridge.execute(
                name: name, rawArguments: raw, session: session,
                accountId: snapshot.accountId, revision: snapshot.revision,
                beforeAdmission: { try state.checkCapacity() },
                onStarted: { state.markStarted() }
            )
            return Self.contextBoundResult(result)
        }

        /// This cannot throw after an admitted RPC: a large successful result must remain an answerable tool result.
        static func contextBoundResult(_ result: String) -> String {
            guard result.utf8.count > maximumModelResultBytes else { return result }
            let envelope = try? JSONSerialization.jsonObject(with: Data(result.utf8)) as? [String: Any]
            let isError = envelope?["isError"] as? Bool == true
            for previewLimit in [280, 140, 70, 0] {
                let preview = String(result.prefix(previewLimit))
                let bounded: [String: Any] = [
                    "isError": isError,
                    "truncated": true,
                    "content": [["type": "text", "text": "<external-content>\(preview)</external-content>"]]
                ]
                if let data = try? JSONSerialization.data(withJSONObject: bounded),
                   data.count <= maximumModelResultBytes,
                   let text = String(data: data, encoding: .utf8) {
                    return text
                }
            }
            return #"{"content":[],"isError":true,"truncated":true}"#
        }

        static func jsonArguments(_ arguments: GeneratedContent) throws -> String {
            let object = try jsonValue(arguments)
            guard object is [String: Any], JSONSerialization.isValidJSONObject(object) else {
                throw NativeChatMCPToolBridge.Failure.invalidArguments
            }
            let data = try JSONSerialization.data(withJSONObject: object)
            guard let raw = String(data: data, encoding: .utf8) else {
                throw NativeChatMCPToolBridge.Failure.invalidArguments
            }
            return raw
        }

        private static func jsonValue(_ content: GeneratedContent) throws -> Any {
            switch content.kind {
            case .null: return NSNull()
            case let .bool(value): return value
            case let .number(value):
                guard value.isFinite, value.rounded() != value || abs(value) <= 9_007_199_254_740_991 else {
                    throw NativeChatMCPToolBridge.Failure.invalidArguments
                }
                return value
            case let .string(value): return value
            case let .array(values): return try values.map(jsonValue)
            case let .structure(properties, _): return try properties.mapValues(jsonValue)
            @unknown default: throw NativeChatMCPToolBridge.Failure.invalidArguments
            }
        }
    }
#endif
