import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatMCPEmailPreferenceBridgeTests: XCTestCase {
        func testCanonicalEmailFactAndCallIdSurviveFailedContinuationWithoutRedispatch() async throws {
            let fixture = try emailPreferencesFixture()
            let service = BridgeMCPService()
            service.catalog = fixture.catalog
            let largeContent = String(repeating: "x", count: 100_000)
            service.toolResult = try result(
                structured: fixture.structured, text: largeContent
            )
            let admission = ReceiptAdmission()
            admission.continuationFits = false
            let (tool, state) = try await makeTool(service: service, admission: admission)

            do {
                _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                XCTFail("An oversized continuation must stop after publishing the validated fact")
            } catch let stop as NativeChatFoundationModelsStop {
                XCTAssertEqual(stop.reason, .contextStopped)
            }

            XCTAssertEqual(service.calls, 1)
            let receipt = try XCTUnwrap(state.receipts.first)
            let preferenceValues = try XCTUnwrap(fixture.structured["email_preferences"] as? [String: Any])
            guard case let .emailPreferences(preferences)? = receipt.fact else {
                return XCTFail("Expected the validated email preference option to produce a typed fact")
            }
            XCTAssertEqual(
                preferences.engagementEmailsEnabled,
                preferenceValues["is_engagement_emails_enabled"] as? Bool
            )
            XCTAssertEqual(preferences.newsDigestFrequency, preferenceValues["news_digest_frequency"] as? String)
            XCTAssertEqual(
                preferences.moderationEmailsEnabled,
                preferenceValues["is_moderation_emails_enabled"] as? Bool
            )
            XCTAssertEqual(
                preferences.communityDigestFrequency,
                preferenceValues["community_digest_frequency"] as? String
            )
            XCTAssertEqual(preferences.moderationEmailCadence, preferenceValues["moderation_email_cadence"] as? String)
            XCTAssertEqual(
                preferences.moderationEmailDaysOfWeek,
                preferenceValues["moderation_email_days_of_week"] as? [Int]
            )
            XCTAssertEqual(
                preferences.moderationEmailTimeOfDay,
                preferenceValues["moderation_email_time_of_day"] as? String
            )
            XCTAssertEqual(
                preferences.moderationEmailTimezone,
                preferenceValues["moderation_email_timezone"] as? String
            )
            XCTAssertEqual(receipt.callId, try XCTUnwrap(admission.preparedCallIds.first))
            XCTAssertEqual(admission.retainedCallIds, admission.preparedCallIds)

            let envelope = try XCTUnwrap(JSONSerialization
                .jsonObject(with: Data(receipt.envelope.utf8)) as? NSDictionary)
            XCTAssertEqual(envelope["isError"] as? Bool, false)
            XCTAssertEqual((envelope["content"] as? [[String: String]])?.first?["text"], largeContent)
            XCTAssertTrue(try XCTUnwrap(envelope["structuredContent"] as? NSDictionary).isEqual(to: fixture.structured))

            do {
                _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))
                XCTFail("Stopped turn must not redispatch")
            } catch { XCTAssertEqual(service.calls, 1) }
            XCTAssertEqual(admission.preparedCallIds.count, 1)
        }

        func testCanonicalEmailErrorResultDoesNotPublishPreferenceFact() async throws {
            let fixture = try emailPreferencesFixture()
            let service = BridgeMCPService()
            service.catalog = fixture.catalog
            service.toolResult = try result(structured: fixture.structured, isError: true)
            let (tool, state) = try await makeTool(service: service, admission: ReceiptAdmission())

            _ = try await tool.call(arguments: GeneratedContent(json: fixture.arguments))

            XCTAssertEqual(service.calls, 1)
            XCTAssertNil(state.receipts.first?.fact)
            XCTAssertEqual(state.receipts.first?.kind, .toolError)
        }

        func testGeneralPreferenceOptionPublishesDistinctFact() async throws {
            let fixture = try emailPreferencesFixture()
            let service = BridgeMCPService()
            service.catalog = fixture.catalog
            service.toolResult = try result(structured: [
                "success": true,
                "settings": [
                    "follows_visibility": "everyone", "topic_follows_visibility": "everyone",
                    "rss_feed_follows_visibility": "everyone", "community_memberships_visibility": "everyone",
                    "followers_visibility": "everyone", "likes_visibility": "everyone",
                    "direct_messages_audience": "everyone", "default_post_broadcast": "everyone",
                    "default_post_privacy": "private", "country": NSNull(), "ui_locale": "en",
                    "should_import_hacker_news_discussions": false
                ]
            ], text: "synthetic general-option result")
            let (tool, state) = try await makeTool(service: service, admission: ReceiptAdmission())

            _ = try await tool.call(arguments: GeneratedContent(json: #"{"option":"general","arguments":{}}"#))

            XCTAssertEqual(service.calls, 1)
            XCTAssertNotNil(state.receipts.first?.fact)
            if case .emailPreferences? = state.receipts.first?.fact {
                XCTFail("The general option must not be rendered as email preferences")
            }
        }

        private func makeTool(
            service: any NativeChatMCPServicing, admission: ReceiptAdmission
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
                bridge: bridge, session: session, snapshot: snapshot, state: state, admission: admission
            )
            return (tool, state)
        }

        private func emailPreferencesFixture() throws -> (
            catalog: DecodedJSONValue, arguments: String, structured: [String: Any]
        ) {
            let root = try FilamentsContractRoot.url(requiredPaths: [
                "api-fixtures/v1/mcp.json", "api-fixtures/v1/mcp-results.json"
            ])
            func object(_ path: String) throws -> [String: Any] {
                try XCTUnwrap(JSONSerialization
                    .jsonObject(with: Data(contentsOf: root.appendingPathComponent(path))) as? [String: Any])
            }
            let servers = try XCTUnwrap(object("api-fixtures/v1/mcp.json")["servers"] as? [[String: Any]])
            let server = try XCTUnwrap(servers.first { $0["name"] as? String == "voucha-user-mcp" })
            let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
            let tool = try XCTUnwrap(entries.compactMap { $0["tool"] as? [String: Any] }
                .first { $0["name"] as? String == "read_my_preferences" })
            let rows = try XCTUnwrap(object("api-fixtures/v1/mcp-results.json")["cases"] as? [[String: Any]])
            let row = try XCTUnwrap(rows.first { $0["id"] as? String == "native.mcp.read-my-preferences.email" })
            let structured = try XCTUnwrap(row["structuredContent"] as? [String: Any])
            let arguments = try XCTUnwrap(row["arguments"])
            let catalog = try JSONDecoder().decode(DecodedJSONValue.self, from: JSONSerialization.data(
                withJSONObject: ["tools": [tool]]
            ))
            return try (
                catalog,
                XCTUnwrap(String(data: JSONSerialization.data(withJSONObject: arguments), encoding: .utf8)),
                structured
            )
        }

        private func result(
            structured: [String: Any], text: String = "synthetic receipt content", isError: Bool = false
        ) throws -> MemberMCPClient.ToolResult {
            try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: JSONSerialization.data(withJSONObject: [
                "content": [["type": "text", "text": text]], "isError": isError, "structuredContent": structured
            ]))
        }
    }
#endif
