import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    protocol NativeChatFoundationModelsAdmitting: Sendable {
        func prepare(name: String, arguments: String) async throws -> String
        func retain(callId: String, name: String, envelope: String) async throws -> Bool
        func finalTranscriptFits() async throws -> Bool
    }

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatModelContextAdmission: NativeChatFoundationModelsAdmitting {
        enum Failure: Error { case uncorrelatedCall, contextUnavailable }
        /// Minimum room for a useful continuation, not a generation or delivered-answer cap.
        static let minimumAnswerHeadroom = 128
        weak var session: LanguageModelSession?
        private let contextSize: Int
        private let countTokens: @Sendable ([Transcript.Entry]) async throws -> Int
        private var claimedCalls = Set<String>()
        private var outputs: [String: Transcript.ToolOutput] = [:]

        init(
            contextSize: Int = SystemLanguageModel.default.contextSize,
            countTokens: @escaping @Sendable ([Transcript.Entry]) async throws -> Int = {
                try await SystemLanguageModel.default.tokenCount(for: $0)
            }
        ) {
            self.contextSize = contextSize
            self.countTokens = countTokens
        }

        func prepare(name: String, arguments: String) async throws -> String {
            guard let session else { throw Failure.uncorrelatedCall }
            let expected = try Self.canonicalArguments(arguments)
            let calls = Array(session.transcript).flatMap { entry -> [Transcript.ToolCall] in
                if case let .toolCalls(calls) = entry { return Array(calls) }
                return []
            }
            let matches = try calls.filter {
                guard $0.toolName == name, !claimedCalls.contains($0.id) else { return false }
                return try Self
                    .canonicalArguments(NativeChatFoundationModelsTool.jsonArguments($0.arguments)) == expected
            }
            guard matches.count == 1, let call = matches.first else { throw Failure.uncorrelatedCall }
            claimedCalls.insert(call.id)
            guard try await fits(entries: Array(session.transcript), headroom: Self.minimumAnswerHeadroom) else {
                throw Failure.contextUnavailable
            }
            return call.id
        }

        func retain(callId: String, name: String, envelope: String) async throws -> Bool {
            guard let session, claimedCalls.contains(callId) else { throw Failure.uncorrelatedCall }
            outputs[callId] = .init(id: callId, toolName: name, segments: [.text(.init(content: envelope))])
            var entries = Array(session.transcript)
            var committed: [Data: Int] = [:]
            for entry in entries {
                if case let .toolOutput(output) = entry, let key = Self.outputIdentity(output) {
                    committed[key, default: 0] += 1
                }
            }
            // SDK entry IDs are correlation metadata, not an assumption about native ownership.
            // Match exact output bytes with multiplicity before appending retained sibling outputs.
            for id in outputs.keys.sorted() {
                guard let output = outputs[id], let key = Self.outputIdentity(output) else { continue }
                if let count = committed[key], count > 0 {
                    committed[key] = count - 1
                } else { entries.append(.toolOutput(output)) }
            }
            return try await fits(entries: entries, headroom: Self.minimumAnswerHeadroom)
        }

        func finalTranscriptFits() async throws -> Bool {
            guard let session else { throw Failure.uncorrelatedCall }
            return try await fits(entries: Array(session.transcript), headroom: 0)
        }

        private func fits(entries: [Transcript.Entry], headroom: Int) async throws -> Bool {
            let count = try await countTokens(entries)
            return count >= 0 && count <= contextSize && headroom <= contextSize - count
        }

        private static func outputIdentity(_ output: Transcript.ToolOutput) -> Data? {
            var key = Data(output.toolName.utf8)
            key.append(0)
            for segment in output.segments {
                guard case let .text(text) = segment else { return nil }
                key.append(contentsOf: text.content.utf8)
            }
            return key
        }

        private static func canonicalArguments(_ raw: String) throws -> Data {
            let value = try JSONSerialization.jsonObject(with: Data(raw.utf8))
            return try JSONSerialization.data(withJSONObject: value, options: [.sortedKeys])
        }
    }
#endif
