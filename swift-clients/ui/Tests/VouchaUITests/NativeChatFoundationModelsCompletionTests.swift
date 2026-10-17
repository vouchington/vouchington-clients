import Foundation
import VouchaCore
@testable import VouchaFeatures
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsCompletionTests: XCTestCase {
        func testSdkTextCannotEraseAlreadyStartedUnknownSibling() async throws {
            let (session, snapshot, state) = try await setup()
            state.markStarted()
            try state.record(
                id: UUID(),
                name: "read_fixture",
                result: .init(envelope: "complete first response", isError: false)
            )
            state.markStarted()
            do {
                _ = try await NativeChatAppleFoundationModelsProvider.completeMCPGeneration(
                    content: "SDK text must never be persisted", state: state, admission: ReceiptAdmission(),
                    session: session, snapshot: snapshot
                )
                XCTFail("An unresolved RPC must remain a possible side effect even if SDK respond succeeds")
            } catch LocalLLMToolLoopFailure.partialExecution {
                XCTAssertTrue(state.hasAmbiguousAttempt)
                XCTAssertEqual(state.receipts.count, 1)
            }
        }

        func testSdkTextCannotTurnAnEstablishedContextStopIntoAnAnswer() async throws {
            let (session, snapshot, state) = try await setup()
            state.markStarted()
            try state.record(id: UUID(), name: "read_fixture", result: .init(envelope: "whole receipt", isError: false))
            _ = state.stop(.contextStopped)
            let outcome = try await NativeChatAppleFoundationModelsProvider.completeMCPGeneration(
                content: "SDK text after stopped tool", state: state, admission: ReceiptAdmission(),
                session: session, snapshot: snapshot
            )
            guard case .toolResultsOnly(.contextStopped)? = outcome
            else { return XCTFail("Established stop must forbid answer publication") }
            XCTAssertEqual(state.receipts.first?.envelope, "whole receipt")
        }

        func testCompleteLargeAnswerRemainsAllowedInsideActualSdkContext() async throws {
            let (session, snapshot, state) = try await setup()
            state.markStarted()
            try state.record(id: UUID(), name: "read_fixture", result: .init(envelope: "whole receipt", isError: false))
            let text = String(repeating: "complete answer ", count: 300).trimmingCharacters(in: .whitespaces)
            let outcome = try await NativeChatAppleFoundationModelsProvider.completeMCPGeneration(
                content: text, state: state, admission: ReceiptAdmission(), session: session, snapshot: snapshot
            )
            guard case let .answer(answer)? = outcome
            else { return XCTFail("Minimum headroom must not become an arbitrary answer cap") }
            XCTAssertEqual(answer.content, text)
            XCTAssertTrue(answer.admittedMCPCall)
        }

        func testFinalContextFailureKeepsValidatedResponseWithoutAnAnswer() async throws {
            let (session, snapshot, state) = try await setup()
            state.markStarted()
            try state.record(id: UUID(), name: "read_fixture", result: .init(envelope: "whole receipt", isError: false))
            let admission = ReceiptAdmission()
            admission.continuationFits = false
            let outcome = try await NativeChatAppleFoundationModelsProvider.completeMCPGeneration(
                content: "complete but over-context SDK answer", state: state, admission: admission,
                session: session, snapshot: snapshot
            )
            guard case .toolResultsOnly(.processingStopped)? = outcome
            else { return XCTFail("Over-context answer must not be published") }
            XCTAssertEqual(state.receipts.count, 1)
            XCTAssertFalse(state.hasAmbiguousAttempt)
        }

        func testStopEstablishedDuringFinalCountingCannotBecomeAnAnswer() async throws {
            let (session, snapshot, state) = try await setup()
            state.markStarted()
            try state.record(id: UUID(), name: "read_fixture", result: .init(envelope: "whole receipt", isError: false))
            let admission = ReceiptAdmission()
            admission.holdFinalCount = true
            let counting = expectation(description: "final transcript count suspended")
            admission.onFinalCount = { counting.fulfill() }
            let completion = Task {
                try await NativeChatAppleFoundationModelsProvider.completeMCPGeneration(
                    content: "SDK text", state: state, admission: admission, session: session, snapshot: snapshot
                )
            }
            await fulfillment(of: [counting], timeout: 5)
            _ = state.stop(.contextStopped)
            admission.releaseFinalCount()
            guard case .toolResultsOnly(.contextStopped)? = try await completion.value
            else { return XCTFail("Stop established during counting must forbid answer publication") }
        }

        func testChangedOwnershipOrUnknownSiblingDuringFinalCountingForbidsAnswer() async throws {
            for change in ["account", "unknown sibling", "cancellation"] {
                let (session, snapshot, state) = try await setup()
                state.markStarted()
                try state.record(
                    id: UUID(),
                    name: "read_fixture",
                    result: .init(envelope: "whole receipt", isError: false)
                )
                let admission = ReceiptAdmission()
                admission.holdFinalCount = true
                let counting = expectation(description: "final count before \(change)")
                admission.onFinalCount = { counting.fulfill() }
                let completion = Task {
                    try await NativeChatAppleFoundationModelsProvider.completeMCPGeneration(
                        content: "SDK text", state: state, admission: admission, session: session, snapshot: snapshot
                    )
                }
                await fulfillment(of: [counting], timeout: 5)
                switch change {
                case "account": await session.useAccount("member-b")
                case "unknown sibling": state.markStarted()
                default: completion.cancel()
                }
                admission.releaseFinalCount()
                do {
                    _ = try await completion.value
                    XCTFail("Changed \(change) must forbid answer publication")
                } catch LocalLLMToolLoopFailure.partialExecution {
                    XCTAssertNotEqual(change, "cancellation")
                } catch is CancellationError {
                    XCTAssertEqual(change, "cancellation")
                }
                XCTAssertEqual(state.receipts.count, 1)
            }
        }

        private func setup() async throws -> (
            NativeChatMCPAgentSession, NativeChatMCPAgentSession.ConnectedSnapshot, NativeChatFoundationModelsToolState
        ) {
            let session = NativeChatMCPAgentSession(service: BridgeMCPService())
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            return (session, snapshot, NativeChatFoundationModelsToolState(turn: .fixture(snapshot)))
        }
    }
#endif
