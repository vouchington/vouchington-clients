import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest
#if canImport(FoundationModels)
    import FoundationModels
#endif

@MainActor
final class NativeChatMCPToolBridgeTests: XCTestCase {
    #if canImport(FoundationModels)
        @available(macOS 26.4, iOS 26.4, *)
        func testFoundationModelsRoundedFractionCannotBypassOriginalSchemaMaximum() async throws {
            let maximum = try XCTUnwrap(Decimal(string: "0.099999999999999999"))
            let service = BridgeMCPService()
            service.catalog = .object(["tools": .array([.object([
                "name": .string("set_weight"),
                "inputSchema": .object([
                    "type": .string("object"),
                    "properties": .object(["weight": .object([
                        "type": .string("number"), "maximum": .decimal(maximum)
                    ])]),
                    "required": .array([.string("weight")])
                ])
            ])])])
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let descriptor = try XCTUnwrap(bridge.tools.first)
            let state = NativeChatFoundationModelsToolState()
            let tool = try NativeChatFoundationModelsTool(
                name: descriptor.name, description: descriptor.description,
                parameters: NativeChatFoundationModelsSchema.project(descriptor.parameters),
                bridge: bridge, session: session, snapshot: snapshot, state: state
            )
            let generated = try GeneratedContent(json: #"{"weight":0.099999999999999999}"#)
            let normalized = try XCTUnwrap(
                JSONSerialization.jsonObject(with: Data(generated.jsonString.utf8)) as? [String: Double]
            )
            XCTAssertEqual(normalized["weight"], 0.1)
            do {
                _ = try await tool.call(arguments: generated)
                XCTFail("A rounded SDK number above the original maximum must not reach MCP")
            } catch {
                XCTAssertEqual(service.calls, 0)
                let attempted = state.attempted
                XCTAssertFalse(attempted)
            }
        }

        @available(macOS 26.4, iOS 26.4, *)
        func testStaleFoundationModelsCallDoesNotMarkPossibleSideEffectBeforeRpcAdmission() async throws {
            let service = BridgeMCPService()
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let state = NativeChatFoundationModelsToolState()
            await session.useAccount("member-b")

            do {
                _ = try await bridge.execute(
                    name: "read_community",
                    rawArguments: #"{"option":"details","arguments":{"id":"one"}}"#,
                    session: session, accountId: snapshot.accountId, revision: snapshot.revision,
                    beforeAdmission: { try state.checkCapacity() },
                    onStarted: { state.markStarted() }
                )
                XCTFail("A stale account must fail before an RPC is admitted")
            } catch {
                XCTAssertEqual(service.calls, 0)
                let attempted = state.attempted
                XCTAssertFalse(attempted)
            }
        }

        @available(macOS 26.4, iOS 26.4, *)
        func testAdmissionCallbackInvalidationCannotStartFoundationModelsRpc() async throws {
            let service = BridgeMCPService()
            let session = NativeChatMCPAgentSession(service: service)
            await session.useAccount("member-a")
            try await session.connect()
            let snapshot = try XCTUnwrap(session.connectedSnapshot())
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let state = NativeChatFoundationModelsToolState()

            do {
                _ = try await bridge.execute(
                    name: "read_community",
                    rawArguments: #"{"option":"details","arguments":{"id":"one"}}"#,
                    session: session, accountId: snapshot.accountId, revision: snapshot.revision,
                    beforeAdmission: {
                        try state.checkCapacity()
                        session.invalidateRejectedAuthorization(
                            accountId: snapshot.accountId, revision: snapshot.revision
                        )
                    },
                    onStarted: { state.markStarted() }
                )
                XCTFail("An invalidated session must not start the RPC")
            } catch {
                XCTAssertEqual(service.calls, 0)
                XCTAssertFalse(state.attempted)
            }
        }

        @available(macOS 26.4, iOS 26.4, *)
        func testPublishedMCPToolsSelectBoundedFoundationModelsSession() async throws {
            let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp.json"])
            let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/mcp.json", root: root)
            let manifest = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
            let servers = try XCTUnwrap(manifest["servers"] as? [[String: Any]])
            let server = try XCTUnwrap(servers.first { $0["surface"] as? String == "mcp" })
            let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
            let publishedTools = try entries.map { try XCTUnwrap($0["tool"] as? [String: Any]) }
            let catalog = try JSONDecoder().decode(DecodedJSONValue.self, from: JSONSerialization.data(
                withJSONObject: ["tools": publishedTools]
            ))
            let bridge = try NativeChatMCPToolBridge(catalog: catalog)
            let service = BridgeMCPService()
            service.catalog = catalog
            let mcpSession = NativeChatMCPAgentSession(service: service)
            await mcpSession.useAccount("member-a")
            try await mcpSession.connect()
            let snapshot = try XCTUnwrap(mcpSession.connectedSnapshot())
            let state = NativeChatFoundationModelsToolState()
            let selected = NativeChatFoundationModelsToolSelection.select(bridge, matching: "Show my cards")
            let tools: [any Tool] = selected.map { item in
                NativeChatFoundationModelsTool(
                    name: item.descriptor.name, description: item.descriptor.description,
                    parameters: item.schema,
                    bridge: bridge, session: mcpSession, snapshot: snapshot, state: state
                )
            }
            XCTAssertEqual(bridge.tools.count, publishedTools.count)
            XCTAssertFalse(tools.isEmpty)
            XCTAssertLessThanOrEqual(tools.count, NativeChatFoundationModelsToolSelection.maximumTools)
            _ = NativeChatAppleFoundationModelsProvider.toolSession(tools)
        }
    #endif

    func testFractionalArgumentReachesToolWithoutDoubleRounding() async throws {
        let service = BridgeMCPService()
        service.catalog = .object(["tools": .array([.object([
            "name": .string("set_weight"),
            "inputSchema": .object([
                "type": .string("object"),
                "properties": .object(["weight": .object(["type": .string("number")])]),
                "required": .array([.string("weight")])
            ])
        ])])])
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
        _ = try await bridge.execute(
            name: "set_weight", rawArguments: #"{"weight":0.099999999999999999}"#,
            session: session, accountId: snapshot.accountId, revision: snapshot.revision
        )
        guard case let .decimal(weight)? = service.lastArguments?["weight"] else {
            return XCTFail("Fractional MCP argument was rounded before tool execution")
        }
        XCTAssertEqual(NSDecimalNumber(decimal: weight).stringValue, "0.099999999999999999")
    }

    func testProducerSizedHtmlUnicodeAndSlashesSurviveActualBridgeEncoding() async throws {
        let service = BridgeMCPService()
        service.catalog = .object(["tools": .array([.object([
            "name": .string("unstructured_text"), "inputSchema": .object(["type": .string("object")])
        ])])])
        let text = String(repeating: "<é/\u{007F}\u{0080}\u{2028}", count: 100_000)
        let producer = Data(#"{"content":[{"type":"text","text":""#.utf8) +
            Data(text.utf8) + Data(#""}],"isError":false}"#.utf8)
        XCTAssertTrue(producer.count <= 1_048_576)
        service.toolResult = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: producer)
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)

        let output = try await bridge.execute(
            name: "unstructured_text", rawArguments: "{}",
            session: session, accountId: snapshot.accountId, revision: snapshot.revision
        )

        XCTAssertEqual(service.calls, 1)
        XCTAssertTrue(output.utf8.count <= OpenAICompatibleResponsesClient.maximumToolResultBytes)
        let decoded = try JSONDecoder().decode(DecodedJSONValue.self, from: Data(output.utf8))
        guard case let .object(object) = decoded,
              case let .array(content)? = object["content"],
              case let .object(first)? = content.first,
              case let .string(actual)? = first["text"] else {
            return XCTFail("Actual bridge output lost MCP content")
        }
        XCTAssertEqual(actual, text)
    }

    func testPublishedUserSchemasProjectWithoutDroppingOptionRefs() throws {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp.json"])
        let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/mcp.json", root: root)
        let manifest = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
        let servers = try XCTUnwrap(manifest["servers"] as? [[String: Any]])
        let server = try XCTUnwrap(servers.first { $0["surface"] as? String == "mcp" })
        let entries = try XCTUnwrap(server["tools"] as? [[String: Any]])
        let tools = try entries.map { try XCTUnwrap($0["tool"] as? [String: Any]) }
        let catalog = try JSONDecoder().decode(
            DecodedJSONValue.self,
            from: JSONSerialization.data(withJSONObject: ["tools": tools])
        )

        let bridge = try NativeChatMCPToolBridge(catalog: catalog)
        XCTAssertEqual(bridge.tools.count, tools.count)
        let merged = try XCTUnwrap(bridge.tools.first { tool in
            tools.contains { $0["name"] as? String == tool.name &&
                (($0["inputSchema"] as? [String: Any])?["oneOf"] != nil)
            }
        })
        let published = try XCTUnwrap(tools.first { $0["name"] as? String == merged.name })
        let projected = try XCTUnwrap(JSONSerialization.jsonObject(with: merged.parameters) as? NSDictionary)
        XCTAssertTrue(try projected.isEqual(to: XCTUnwrap(published["inputSchema"] as? [String: Any])))
    }

    func testBridgeRejectsMCPNamesThatCannotBeModelFunctionNames() throws {
        for name in ["posts.search", String(repeating: "a", count: 65)] {
            let catalog: DecodedJSONValue = .object(["tools": .array([.object([
                "name": .string(name), "inputSchema": .object(["type": .string("object")])
            ])])])
            XCTAssertThrowsError(try NativeChatMCPToolBridge(catalog: catalog))
        }
    }

    func testOnlyAdvertisedAndSchemaValidCallsExecuteWithUnchangedResultMarkers() async throws {
        let service = BridgeMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)

        XCTAssertThrowsError(try bridge.validate(name: "invented", rawArguments: "{}"))

        do {
            _ = try await bridge.execute(
                name: "invented", rawArguments: "{}", session: session,
                accountId: snapshot.accountId, revision: snapshot.revision
            )
            XCTFail("Unknown tool must not execute")
        } catch { XCTAssertEqual(service.calls, 0) }
        for invalid in [
            #"{"option":"missing","arguments":{"id":"one"}}"#,
            #"{"option":"details","arguments":{"unknown":"one"}}"#,
            #"{"option":"details","arguments":{"id":"longer"}}"#
        ] {
            XCTAssertThrowsError(try bridge.validate(name: "read_community", rawArguments: invalid))
            do {
                _ = try await bridge.execute(
                    name: "read_community", rawArguments: invalid, session: session,
                    accountId: snapshot.accountId, revision: snapshot.revision
                )
                XCTFail("Invalid option envelope must not execute")
            } catch { XCTAssertEqual(service.calls, 0) }
        }
        XCTAssertNoThrow(try bridge.validate(
            name: "read_community", rawArguments: #"{"option":"details","arguments":{"id":"one"}}"#
        ))
        let output = try await bridge.execute(
            name: "read_community",
            rawArguments: #"{"option":"details","arguments":{"id":"one"}}"#,
            session: session, accountId: snapshot.accountId, revision: snapshot.revision
        )
        XCTAssertEqual(service.calls, 1)
        let result = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(output.utf8)) as? [String: Any])
        XCTAssertEqual(result["isError"] as? Bool, false)
        let structured = try XCTUnwrap(result["structuredContent"] as? [String: Any])
        XCTAssertEqual(structured["quote"] as? String, "<external-content>quoted text</external-content>")

        await session.useAccount("member-b")
        do {
            _ = try await bridge.execute(
                name: "read_community",
                rawArguments: #"{"option":"details","arguments":{"id":"one"}}"#,
                session: session, accountId: snapshot.accountId, revision: snapshot.revision
            )
            XCTFail("Old account call must not execute")
        } catch { XCTAssertEqual(service.calls, 1) }
    }

    func testAccountSwitchCancelsHeldToolCallAndRejectsLateResult() async throws {
        let service = BridgeMCPService()
        service.holdCalls = true
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
        let call = Task {
            try await bridge.execute(
                name: "read_community",
                rawArguments: #"{"option":"details","arguments":{"id":"one"}}"#,
                session: session, accountId: snapshot.accountId, revision: snapshot.revision
            )
        }
        await service.waitForCall()
        await session.useAccount("member-b")
        service.releaseCall()
        do {
            _ = try await call.value
            XCTFail("Late old-account result must not publish")
        } catch is CancellationError {
            XCTAssertEqual(service.calls, 1)
            XCTAssertNil(session.connectedSnapshot())
        }
    }

    func testSchemaAppliesAdditionalPropertyAndArrayItemAssertions() throws {
        let objectSchema: DecodedJSONValue = .object([
            "type": .string("object"),
            "additionalProperties": .object(["type": .string("integer"), "minimum": .integer(1)])
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
            .object(["count": .integer(2)]), schema: objectSchema
        ))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .object(["count": .integer(0)]), schema: objectSchema
        ))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .object(["count": .string("2")]), schema: objectSchema
        ))

        let arraySchema: DecodedJSONValue = .object([
            "type": .string("array"), "items": .object(["type": .string("string")])
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
            .array([.string("first"), .string("second")]), schema: arraySchema
        ))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .array([.string("first"), .integer(2)]), schema: arraySchema
        ))
    }

    func testSchemaRejectsUnboundedPatternsAndInvalidFormatsBeforeToolCall() throws {
        for source in ["^[a-z]+$", "^(a+)+$"] {
            let catalog: DecodedJSONValue = .object(["tools": .array([.object([
                "name": .string("read"), "inputSchema": .object([
                    "type": .string("object"), "properties": .object([
                        "text": .object(["type": .string("string"), "pattern": .string(source)])
                    ])
                ])
            ])])])
            XCTAssertThrowsError(try NativeChatMCPToolBridge(catalog: catalog))
        }

        let uuid: DecodedJSONValue = .object([
            "type": .string("string"), "format": .string("uuid")
        ])
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("not-a-uuid"), schema: uuid))
        let uri: DecodedJSONValue = .object([
            "type": .string("string"), "format": .string("uri")
        ])
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("relative/path"), schema: uri))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("mailto:a b"), schema: uri))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("https://example.test/%GG"), schema: uri))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.string("mailto:a%20b"), schema: uri))

        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "pattern": .string("[")
        ])))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "format": .string("email")
        ])))
    }
}

@MainActor
final class BridgeMCPService: NativeChatMCPServicing {
    private(set) var calls = 0
    private(set) var lastArguments: [String: DecodedJSONValue]?
    var holdCalls = false
    var toolResult: MemberMCPClient.ToolResult?
    var catalog = BridgeMCPService.defaultCatalog
    private var pendingCall: CheckedContinuation<Void, Never>?
    private var callStarted: CheckedContinuation<Void, Never>?

    func waitForCall() async {
        if calls > 0 { return }
        await withCheckedContinuation { callStarted = $0 }
    }

    func releaseCall() {
        pendingCall?.resume()
        pendingCall = nil
    }

    func connect(accountId _: String) async throws -> DecodedJSONValue {
        catalog
    }

    func refreshTools(accountId _: String) async throws -> DecodedJSONValue {
        catalog
    }

    func disconnect(accountId _: String) async throws {}

    func callTool(
        accountId _: String, name _: String, arguments: [String: DecodedJSONValue]
    ) async throws -> MemberMCPClient.ToolResult {
        calls += 1
        lastArguments = arguments
        callStarted?.resume()
        callStarted = nil
        if holdCalls { await withCheckedContinuation { pendingCall = $0 } }
        try Task.checkCancellation()
        if let toolResult { return toolResult }
        return try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(
            #"{"content":[],"structuredContent":{"quote":"<external-content>quoted text</external-content>"},"isError":false}"#
                .utf8
        ))
    }

    private static let defaultCatalog: DecodedJSONValue = .object(["tools": .array([.object([
        "name": .string("read_community"), "description": .string("Read community"),
        "inputSchema": .object([
            "type": .string("object"), "$defs": .object(["Id": .object(["type": .string("string")])]),
            "oneOf": .array([.object([
                "type": .string("object"),
                "properties": .object([
                    "option": .object(["enum": .array([.string("details")])]),
                    "arguments": .object([
                        "type": .string("object"),
                        "properties": .object(["id": .object([
                            "$ref": .string("#/$defs/Id"), "maxLength": .number(3)
                        ])]),
                        "required": .array([.string("id")]), "additionalProperties": .bool(false)
                    ])
                ]),
                "required": .array([.string("option"), .string("arguments")]),
                "additionalProperties": .bool(false)
            ])])
        ]),
        "outputSchema": .object([
            "type": .string("object"),
            "properties": .object(["quote": .object(["type": .string("string")])]),
            "required": .array([.string("quote")]), "additionalProperties": .bool(false)
        ]),
        "_meta": .object(["voucha/optionOutputPointers": .object(["details": .string("#")])])
    ])])])
}
