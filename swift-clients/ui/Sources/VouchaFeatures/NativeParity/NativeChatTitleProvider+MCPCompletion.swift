import Foundation
import VouchaCore

#if canImport(FoundationModels)
    extension NativeChatAppleFoundationModelsProvider {
        /// SDK success is insufficient when an admitted call is unresolved or the native loop stopped.
        @available(macOS 26.4, iOS 26.4, *)
        @MainActor
        static func completeMCPGeneration(
            content: String,
            state: NativeChatFoundationModelsToolState,
            admission: any NativeChatFoundationModelsAdmitting,
            session: NativeChatMCPAgentSession,
            snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
        ) async throws -> NativeChatMCPGenerationOutcome? {
            try validateCompletionState(state, session: session, snapshot: snapshot)
            let attempted = state.attempted
            if let stopped = state.stopped {
                guard attempted else { throw LocalLLMToolLoopFailure.toolNotAdmitted }
                return .toolResultsOnly(stopped)
            }
            let text = content.trimmingCharacters(in: .whitespacesAndNewlines)
            if attempted {
                let fits = await (try? admission.finalTranscriptFits()) == true
                try validateCompletionState(state, session: session, snapshot: snapshot)
                if let stopped = state.stopped { return .toolResultsOnly(stopped) }
                if text.isEmpty || !fits { return .toolResultsOnly(.processingStopped) }
            } else if text.isEmpty { return nil }
            return .answer(.init(
                content: text, modelProvider: "apple_foundation", modelName: "apple-foundation-system",
                admittedMCPCall: attempted
            ))
        }

        @MainActor
        @available(macOS 26.4, iOS 26.4, *)
        private static func validateCompletionState(
            _ state: NativeChatFoundationModelsToolState,
            session: NativeChatMCPAgentSession,
            snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
        ) throws {
            try Task.checkCancellation()
            guard !state.hasAmbiguousAttempt else { throw LocalLLMToolLoopFailure.partialExecution }
            guard !state.attempted || session.isCurrent(snapshot) else {
                throw LocalLLMToolLoopFailure.partialExecution
            }
        }
    }
#endif
