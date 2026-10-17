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
    final class NativeChatMCPGeneralPreferenceBridgeTests: XCTestCase {
        func testCanonicalGeneralFactRetainsEnvelopeAndCallIdentityWithoutRedispatchOnOverflow() async throws {
            let fixture = try NativeChatMCPGeneralPreferenceFixture()
            let service = BridgeMCPService()
            service.catalog = fixture.catalog
            let content = String(repeating: "x", count: 100_000)
            service.toolResult = try fixture.result(text: content)
            let admission = ReceiptAdmission()
            admission.continuationFits = false
            let (tool, state) = try await makeTool(service, admission: admission)

            do {
                _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                XCTFail("A full continuation must stop after retaining the validated general fact")
            } catch let stop as NativeChatFoundationModelsStop {
                XCTAssertEqual(stop.reason, .contextStopped)
            }
            let receipt = try XCTUnwrap(state.receipts.first)
            guard case let .generalPreferences(preferences)? = receipt.fact else {
                return XCTFail("Expected the validated general option to produce its typed fact")
            }
            XCTAssertFalse(preferences.shouldImportHackerNewsDiscussions)
            XCTAssertEqual(preferences.country, "US")
            XCTAssertNil(preferences.uiLocale)
            XCTAssertEqual(receipt.callId, try XCTUnwrap(admission.preparedCallIds.first))
            XCTAssertEqual(admission.retainedCallIds, admission.preparedCallIds)
            let envelope = try XCTUnwrap(JSONSerialization
                .jsonObject(with: Data(receipt.envelope.utf8)) as? NSDictionary)
            XCTAssertEqual(envelope["isError"] as? Bool, false)
            XCTAssertEqual((envelope["content"] as? [[String: String]])?.first?["text"], content)
            XCTAssertTrue(try XCTUnwrap(envelope["structuredContent"] as? NSDictionary).isEqual(to: fixture.structured))
            do {
                _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                XCTFail("A stopped turn must never redispatch the tool")
            } catch { XCTAssertEqual(service.calls, 1) }
            XCTAssertEqual(service.calls, 1)
            XCTAssertEqual(admission.preparedCallIds.count, 1)
        }

        func testEveryRequiredFieldAndSelectedOutputSchemaMustValidateBeforePublishingFact() async throws {
            let canonical = try NativeChatMCPGeneralPreferenceFixture()
            let settings = try XCTUnwrap(canonical.structured["settings"] as? [String: Any])
            var invalidSettings: [[String: Any]] = settings.keys.sorted().map { key in
                var missing = settings
                missing.removeValue(forKey: key)
                return missing
            }
            var extra = settings
            extra["unexpected"] = true
            invalidSettings.append(extra)
            for (key, value) in [
                ("should_import_hacker_news_discussions", "false"),
                ("follows_visibility", "unknown-audience")
            ] {
                var invalid = settings
                invalid[key] = value
                invalidSettings.append(invalid)
            }
            for invalid in invalidSettings {
                var fixture = canonical
                fixture.structured["settings"] = invalid
                let service = BridgeMCPService()
                service.catalog = fixture.catalog
                service.toolResult = try fixture.result()
                let (tool, state) = try await makeTool(service)
                do {
                    _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                    XCTFail("An invalid selected-option output must not become a fact")
                } catch { XCTAssertTrue(state.receipts.isEmpty) }
                XCTAssertEqual(service.calls, 1)
            }
        }

        func testGeneralErrorResultAndMismatchedEmailOptionNeverPublishGeneralFact() async throws {
            let fixture = try NativeChatMCPGeneralPreferenceFixture()
            let service = BridgeMCPService()
            service.catalog = fixture.catalog
            service.toolResult = try fixture.result(isError: true)
            let (tool, state) = try await makeTool(service)
            _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
            XCTAssertNil(state.receipts.first?.fact)
            XCTAssertEqual(state.receipts.first?.kind, .toolError)

            let mismatch = BridgeMCPService()
            mismatch.catalog = fixture.catalog
            mismatch.toolResult = try fixture.result()
            let (otherTool, otherState) = try await makeTool(mismatch)
            do {
                _ = try await otherTool.call(arguments: GeneratedContent(json: #"{"option":"email","arguments":{}}"#))
                XCTFail("The email option cannot publish a general result")
            } catch { XCTAssertTrue(otherState.receipts.isEmpty) }
            XCTAssertEqual(mismatch.calls, 1)
        }

        private func makeTool(
            _ service: any NativeChatMCPServicing, admission: ReceiptAdmission? = nil
        ) async throws -> (NativeChatFoundationModelsTool, NativeChatFoundationModelsToolState) {
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let descriptor = try XCTUnwrap(bridge.tools.first { $0.name == "read_my_preferences" })
            let state = NativeChatFoundationModelsToolState(turn: .fixture(snapshot))
            let tool = try NativeChatFoundationModelsTool(
                name: descriptor.name, description: descriptor.description,
                parameters: NativeChatFoundationModelsSchema.project(descriptor.parameters),
                bridge: bridge, session: session, snapshot: snapshot, state: state,
                admission: admission ?? ReceiptAdmission()
            )
            return (tool, state)
        }
    }
#endif
