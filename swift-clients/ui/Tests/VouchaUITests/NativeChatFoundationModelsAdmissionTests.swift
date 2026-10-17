import Foundation
@testable import VouchaFeatures
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsAdmissionTests: XCTestCase {
        func testPendingCallCorrelationIgnoresJsonObjectKeyOrder() async throws {
            let call = try call(id: "sdk-correlation-only", arguments: #"{"b":1,"a":2}"#)
            let session = LanguageModelSession(transcript: Transcript(entries: [.toolCalls(.init([call]))]))
            let counter = AdmissionCounter()
            let admission = NativeChatModelContextAdmission(
                contextSize: 400,
                countTokens: { await counter.count($0) }
            )
            admission.session = session
            let id = try await admission.prepare(name: "read_fixture", arguments: #"{"a":2,"b":1}"#)
            XCTAssertEqual(id, call.id)
            do {
                _ = try await admission.prepare(name: "read_fixture", arguments: #"{"a":2,"b":1}"#)
                XCTFail("One SDK call cannot be admitted twice")
            } catch {
                let count = await counter.callCount()
                XCTAssertEqual(count, 1)
            }
        }

        func testAmbiguousIdenticalBatchDeclinesBeforeAnyTokenAdmission() async throws {
            let calls = try [call(id: "one"), call(id: "two")]
            let session = LanguageModelSession(transcript: Transcript(entries: [.toolCalls(.init(calls))]))
            let counter = AdmissionCounter()
            let admission = NativeChatModelContextAdmission(
                contextSize: 400,
                countTokens: { await counter.count($0) }
            )
            admission.session = session
            do {
                _ = try await admission.prepare(name: "read_fixture", arguments: "{}")
                XCTFail("Identical batch calls have no unique argument correlation")
            } catch {
                let count = await counter.callCount()
                XCTAssertEqual(count, 0)
            }
        }

        func testRetainedSiblingOutputsShareExactContextAdmission() async throws {
            let calls = try [call(id: "one", arguments: #"{"n":1}"#), call(id: "two", arguments: #"{"n":2}"#)]
            let session = LanguageModelSession(transcript: Transcript(entries: [.toolCalls(.init(calls))]))
            let counter = AdmissionCounter()
            let admission = NativeChatModelContextAdmission(
                contextSize: 400,
                countTokens: { await counter.count($0) }
            )
            admission.session = session
            let first = try await admission.prepare(name: "read_fixture", arguments: #"{"n":1}"#)
            let second = try await admission.prepare(name: "read_fixture", arguments: #"{"n":2}"#)
            let firstFits = try await admission.retain(
                callId: first,
                name: "read_fixture",
                envelope: "first full result"
            )
            let secondFits = try await admission.retain(
                callId: second,
                name: "read_fixture",
                envelope: "second full result"
            )
            XCTAssertTrue(firstFits)
            XCTAssertFalse(secondFits)
            let captured = await counter.lastEntries()
            let last = try XCTUnwrap(captured)
            let outputs = last.compactMap { entry -> Transcript.ToolOutput? in
                if case let .toolOutput(output) = entry { return output }
                return nil
            }
            XCTAssertEqual(outputs.count, 2)
            XCTAssertEqual(outputs.map(\.toolName), ["read_fixture", "read_fixture"])
        }

        func testCommittedOutputIdentityDoesNotAssumeSdkEntryIdEqualsCallId() async throws {
            let call = try call(id: "call-id")
            let committed = Transcript.ToolOutput(
                id: "different-entry-id",
                toolName: "read_fixture",
                segments: [.text(.init(content: "whole receipt"))]
            )
            let session = LanguageModelSession(transcript: Transcript(entries: [
                .toolCalls(.init([call])),
                .toolOutput(committed)
            ]))
            let counter = AdmissionCounter()
            let admission = NativeChatModelContextAdmission(
                contextSize: 500,
                countTokens: { await counter.count($0) }
            )
            admission.session = session
            let id = try await admission.prepare(name: "read_fixture", arguments: "{}")
            _ = try await admission.retain(callId: id, name: "read_fixture", envelope: "whole receipt")
            let captured = await counter.lastEntries()
            let last = try XCTUnwrap(captured)
            XCTAssertEqual(last.count, 2)
        }

        func testMinimumHeadroomIsNotAMaximumFinalAnswerCap() async throws {
            let session = LanguageModelSession()
            let counter = AdmissionCounter(fixedCounts: [399, 401])
            let admission = NativeChatModelContextAdmission(
                contextSize: 400,
                countTokens: { await counter.count($0) }
            )
            admission.session = session
            let withinActualContext = try await admission.finalTranscriptFits()
            let overActualContext = try await admission.finalTranscriptFits()
            XCTAssertTrue(withinActualContext)
            XCTAssertFalse(overActualContext)
        }

        private func call(id: String, arguments: String = "{}") throws -> Transcript.ToolCall {
            try .init(id: id, toolName: "read_fixture", arguments: GeneratedContent(json: arguments))
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    private actor AdmissionCounter {
        private var entries: [[Transcript.Entry]] = []
        private var fixedCounts: [Int]
        init(fixedCounts: [Int] = []) {
            self.fixedCounts = fixedCounts
        }

        func count(_ value: [Transcript.Entry]) -> Int {
            entries.append(value)
            return fixedCounts.isEmpty ? value.count * 100 : fixedCounts.removeFirst()
        }

        func callCount() -> Int {
            entries.count
        }

        func lastEntries() -> [Transcript.Entry]? {
            entries.last
        }
    }
#endif
