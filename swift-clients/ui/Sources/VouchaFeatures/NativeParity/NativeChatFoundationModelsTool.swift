import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels

    struct NativeChatFoundationModelsStop: Error {
        let reason: NativeChatMCPContinuation
    }

    @available(macOS 26.4, iOS 26.4, *)
    @MainActor
    final class NativeChatFoundationModelsToolState {
        private(set) var attempted = false
        private var callCount = 0
        private var admittedArgumentBytes = 0
        private(set) var receipts: [NativeChatMCPReceipt] = []
        private(set) var stopped: NativeChatMCPContinuation?
        let turn: NativeChatMCPTurnContext
        static let maximumSessionArgumentBytes = NativeChatFoundationModelsSchema.maximumSessionArgumentBytes

        init(turn: NativeChatMCPTurnContext) {
            self.turn = turn
        }

        var hasAmbiguousAttempt: Bool {
            callCount > receipts.count
        }

        func checkCapacity(argumentBytes: Int = 0) throws {
            guard !Task.isCancelled, turn.isCurrent() else { throw CancellationError() }
            if let stopped { throw NativeChatFoundationModelsStop(reason: stopped) }
            guard callCount < 4,
                  argumentBytes <= Self.maximumSessionArgumentBytes - admittedArgumentBytes else {
                if attempted { throw stop(.processingStopped) }
                throw LocalLLMToolLoopFailure.toolNotAdmitted
            }
        }

        func markStarted(argumentBytes: Int = 0) {
            callCount += 1
            admittedArgumentBytes += argumentBytes
            attempted = true
        }

        func record(id: UUID, name: String, result: NativeChatMCPToolBridge.ValidatedResult) throws {
            let receipt = NativeChatMCPReceipt(
                id: id, owner: turn.owner, toolName: name, envelope: result.envelope,
                kind: result.isError ? .toolError : .response
            )
            guard !Task.isCancelled, turn.publish(receipt) else { throw CancellationError() }
            receipts.append(receipt)
        }

        func validateReturn(
            session: NativeChatMCPAgentSession, snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
        ) throws {
            try Task.checkCancellation()
            guard turn.isCurrent(), session.isCurrent(snapshot) else { throw CancellationError() }
            if let stopped { throw NativeChatFoundationModelsStop(reason: stopped) }
        }

        func stop(_ reason: NativeChatMCPContinuation) -> NativeChatFoundationModelsStop {
            if stopped == nil { stopped = reason }
            return NativeChatFoundationModelsStop(reason: stopped ?? reason)
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    struct NativeChatFoundationModelsTool: Tool {
        let name: String
        let description: String
        let parameters: GenerationSchema
        let bridge: NativeChatMCPToolBridge
        let session: NativeChatMCPAgentSession
        let snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
        let state: NativeChatFoundationModelsToolState
        let admission: any NativeChatFoundationModelsAdmitting

        func call(arguments: GeneratedContent) async throws -> String {
            let raw = try Self.jsonArguments(arguments)
            try bridge.validate(name: name, rawArguments: raw)
            guard await session.isCurrent(snapshot) else { throw CancellationError() }
            try await state.checkCapacity(argumentBytes: raw.utf8.count)
            let callId: String
            do {
                callId = try await admission.prepare(name: name, arguments: raw)
            } catch NativeChatModelContextAdmission.Failure.contextUnavailable {
                throw await state.stop(.contextStopped)
            } catch {
                throw await state.stop(.processingStopped)
            }
            let executionId = UUID()
            let result = try await bridge.executeResult(
                name: name, rawArguments: raw, session: session,
                accountId: snapshot.accountId, revision: snapshot.revision,
                beforeAdmission: { try state.checkCapacity(argumentBytes: raw.utf8.count) },
                onStarted: { state.markStarted(argumentBytes: raw.utf8.count) }
            )
            guard await session.isCurrent(snapshot) else { throw CancellationError() }
            try await state.record(id: executionId, name: name, result: result)
            if let stopped = await state.stopped { throw NativeChatFoundationModelsStop(reason: stopped) }
            do {
                guard try await admission.retain(callId: callId, name: name, envelope: result.envelope) else {
                    throw await state.stop(.contextStopped)
                }
            } catch let stop as NativeChatFoundationModelsStop {
                throw stop
            } catch {
                throw await state.stop(.processingStopped)
            }
            try await state.validateReturn(session: session, snapshot: snapshot)
            try Task.checkCancellation()
            return result.envelope
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
