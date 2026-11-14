import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatMCPProfileBridgeTests: XCTestCase {
        func testCanonicalBioAndLinksKeepTheirCompleteEnvelopeAndDispatchOnce() async throws {
            for option in [NativeChatMCPProfileFixture.Option.bio, .links] {
                let fixture = try NativeChatMCPProfileFixture(option: option)
                let service = BridgeMCPService()
                service.catalog = fixture.catalog
                let syntheticContent = String(repeating: "synthetic-result-content", count: 5_000)
                service.toolResult = try fixture.result(text: syntheticContent)
                let admission = ReceiptAdmission()
                admission.continuationFits = false
                let (tool, state) = try await makeTool(service, admission: admission)

                do {
                    _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                    XCTFail("Complete result should be retained when model continuation cannot fit")
                } catch let stop as NativeChatFoundationModelsStop {
                    XCTAssertEqual(stop.reason, .contextStopped)
                }
                let receipt = try XCTUnwrap(state.receipts.first)
                XCTAssertEqual(receipt.callId, try XCTUnwrap(admission.preparedCallIds.first))
                XCTAssertEqual(admission.retainedCallIds, admission.preparedCallIds)
                switch (option, receipt.fact) {
                case (.bio, .profileBio): break
                case (.links, .profileLinks): break
                default: XCTFail("The validated option should publish only its matching profile fact")
                }
                let envelope = try XCTUnwrap(JSONSerialization
                    .jsonObject(with: Data(receipt.envelope.utf8)) as? NSDictionary)
                XCTAssertEqual(envelope["isError"] as? Bool, false)
                XCTAssertEqual((envelope["content"] as? [[String: String]])?.first?["text"], syntheticContent)
                XCTAssertTrue(try XCTUnwrap(envelope["structuredContent"] as? NSDictionary)
                    .isEqual(to: fixture.structured))
                do {
                    _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                    XCTFail("Stopped turn must not redispatch a completed profile read")
                } catch { XCTAssertEqual(service.calls, 1) }
                XCTAssertEqual(service.calls, 1)
                XCTAssertEqual(admission.preparedCallIds.count, 1)
            }
        }

        func testErrorAndWrongSelectedOutputCannotPublishProfileFact() async throws {
            let fixture = try NativeChatMCPProfileFixture(option: .bio)
            let errorService = BridgeMCPService()
            errorService.catalog = fixture.catalog
            errorService.toolResult = try fixture.result(isError: true)
            let (errorTool, errorState) = try await makeTool(errorService)
            _ = try await errorTool.call(arguments: GeneratedContent(json: fixture.arguments))
            XCTAssertNil(errorState.receipts.first?.fact)
            XCTAssertEqual(errorState.receipts.first?.kind, .toolError)
            XCTAssertEqual(errorService.calls, 1)

            let wrongService = BridgeMCPService()
            wrongService.catalog = fixture.catalog
            wrongService.toolResult = try fixture.result()
            let (wrongTool, wrongState) = try await makeTool(wrongService)
            do {
                _ = try await wrongTool.call(arguments: GeneratedContent(json: #"{"option":"links","arguments":{}}"#))
                XCTFail("Bio output cannot pass the links selected-option schema")
            } catch { XCTAssertTrue(wrongState.receipts.isEmpty) }
            XCTAssertEqual(wrongService.calls, 1)
        }

        func testProfileFactReceiptRejectsOtherAccountAndReusedCallIdentityAcrossTurns() throws {
            let fixture = try NativeChatMCPProfileFixture(option: .bio)
            let fact = try XCTUnwrap(NativeChatMCPFact.makeProfileBio(fixture.value))
            let owner = NativeChatMCPTurnOwner(
                accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
            )
            var ledger = NativeChatMCPReceiptLedger(owner: owner)
            for stale in [
                NativeChatMCPTurnOwner(
                    accountId: "account-b", revision: 2, conversationId: "conversation-1", userMessageId: "turn-2"
                ),
                NativeChatMCPTurnOwner(
                    accountId: "account-a", revision: 1, conversationId: "conversation-1", userMessageId: "turn-2"
                ),
                NativeChatMCPTurnOwner(
                    accountId: "account-a", revision: 2, conversationId: "conversation-1", userMessageId: "turn-1"
                )
            ] {
                XCTAssertFalse(ledger.record(.init(
                    id: UUID(), callId: "reused-call", owner: stale,
                    toolName: "read_my_profile", envelope: "synthetic profile envelope", kind: .response, fact: fact
                )))
            }
            XCTAssertTrue(ledger.receipts.isEmpty)
            XCTAssertTrue(ledger.record(.init(
                id: UUID(), callId: "reused-call", owner: owner,
                toolName: "read_my_profile", envelope: "synthetic profile envelope", kind: .response, fact: fact
            )))
            XCTAssertEqual(ledger.receipts.count, 1)
        }

        private func makeTool(
            _ service: any NativeChatMCPServicing, admission: ReceiptAdmission? = nil
        ) async throws -> (NativeChatFoundationModelsTool, NativeChatFoundationModelsToolState) {
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let descriptor = try XCTUnwrap(bridge.tools.first { $0.name == "read_my_profile" })
            let state = NativeChatFoundationModelsToolState(turn: .fixture(snapshot))
            let tool = try NativeChatFoundationModelsTool(
                name: descriptor.name, description: descriptor.description,
                parameters: NativeChatFoundationModelsSchema.project(descriptor.parameters),
                bridge: bridge, session: session, snapshot: snapshot,
                state: state, admission: admission ?? ReceiptAdmission()
            )
            return (tool, state)
        }
    }
#endif
