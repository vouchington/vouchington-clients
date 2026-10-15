import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    @MainActor
    final class NativeChatFoundationModelsToolState {
        private(set) var attempted = false
        private var callCount = 0
        private var admittedArgumentBytes = 0
        private(set) var deliveredResultBytes = 0
        static let maximumSessionResultBytes = 1_200
        static let maximumSessionArgumentBytes = NativeChatFoundationModelsSchema.maximumSessionArgumentBytes

        func checkCapacity(argumentBytes: Int = 0) throws {
            guard callCount < 4,
                  argumentBytes <= Self.maximumSessionArgumentBytes - admittedArgumentBytes else {
                if attempted { throw LocalLLMToolLoopFailure.partialExecution }
                throw LocalLLMToolLoopFailure.toolNotAdmitted
            }
        }

        func markStarted(argumentBytes: Int = 0) {
            callCount += 1
            admittedArgumentBytes += argumentBytes
            attempted = true
        }

        func boundedResult(_ result: String) -> String {
            let reserved = (4 - callCount) * 80
            let available = max(80, Self.maximumSessionResultBytes - deliveredResultBytes - reserved)
            let bounded = NativeChatFoundationModelsTool.contextBoundResult(
                result, maximumBytes: min(available, NativeChatFoundationModelsTool.maximumModelResultBytes)
            )
            deliveredResultBytes += bounded.utf8.count
            return bounded
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
                beforeAdmission: { try state.checkCapacity(argumentBytes: raw.utf8.count) },
                onStarted: { state.markStarted(argumentBytes: raw.utf8.count) }
            )
            return await state.boundedResult(result)
        }

        /// This cannot throw after an admitted RPC: a large successful result must remain an answerable tool result.
        static func contextBoundResult(_ result: String, maximumBytes: Int = maximumModelResultBytes) -> String {
            guard result.utf8.count > maximumBytes else { return result }
            let envelope = try? JSONSerialization.jsonObject(with: Data(result.utf8)) as? [String: Any]
            let isError = envelope?["isError"] as? Bool == true
            let textContent = (envelope?["content"] as? [[String: Any]] ?? [])
                .compactMap { item -> String? in
                    guard item["type"] as? String == "text" else { return nil }
                    return item["text"] as? String
                }.joined(separator: "\n")
            let structured = envelope?["structuredContent"]
            let structuredText = (structured as? String) ?? structured.flatMap { value -> String? in
                guard !(value is NSNull) else { return nil }
                if let object = value as? [String: Any], object.isEmpty { return nil }
                if let array = value as? [Any], array.isEmpty { return nil }
                guard let data = try? JSONSerialization.data(withJSONObject: value, options: .fragmentsAllowed) else {
                    return nil
                }
                return String(data: data, encoding: .utf8)
            }
            let nonTextContent = (envelope?["content"] as? [[String: Any]] ?? [])
                .first { $0["type"] as? String != "text" }
                .flatMap { item -> String? in
                    if let type = item["type"] as? String {
                        let mimeType = item["mimeType"] as? String
                        return [type, mimeType].compactMap { $0 }.joined(separator: " ")
                    }
                    guard let data = try? JSONSerialization.data(withJSONObject: item) else { return nil }
                    return String(data: data, encoding: .utf8)
                }
            let previewSource = !textContent.isEmpty ? textContent : (structuredText ?? nonTextContent ?? result)
            for previewLimit in [280, 140, 70, 0] {
                let preview = String(previewSource.prefix(previewLimit))
                let bounded: [String: Any] = [
                    "isError": isError,
                    "truncated": true,
                    "content": [["type": "text", "text": "<external-content>\(preview)</external-content>"]]
                ]
                if let data = try? JSONSerialization.data(withJSONObject: bounded),
                   data.count <= maximumBytes,
                   let text = String(data: data, encoding: .utf8) {
                    return text
                }
            }
            return isError
                ? #"{"content":[],"isError":true,"truncated":true}"#
                : #"{"content":[],"isError":false,"truncated":true}"#
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
