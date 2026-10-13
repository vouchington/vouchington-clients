import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

@MainActor
final class NativeChatMCPToolBridgeTests: XCTestCase {
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

    func testSchemaNumericEqualityAppliesToConstEnumAndUniqueItems() throws {
        let constant: DecodedJSONValue = .object(["const": .integer(1)])
        let choices: DecodedJSONValue = .object(["enum": .array([.unsignedInteger(1)])])
        let unique: DecodedJSONValue = .object([
            "type": .string("array"), "uniqueItems": .bool(true), "maxItems": .integer(256)
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.number(1), schema: constant))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(Decimal(1)), schema: choices))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .array([.integer(1), .decimal(Decimal(1))]), schema: unique
        ))
    }

    func testUniqueItemsValidationHasAnIndependentWorkBound() throws {
        let unique: DecodedJSONValue = .object([
            "type": .string("array"), "uniqueItems": .bool(true), "maxItems": .integer(256)
        ])
        let values: [DecodedJSONValue] = (0 ... 256).map { .string(String($0)) }
        try NativeChatMCPInputSchema.validateSchema(unique)
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.array(Array(values.prefix(256))), schema: unique))
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.array(values), schema: unique))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "type": .string("array"), "uniqueItems": .bool(true)
        ])))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "type": .string("array"), "uniqueItems": .bool(true), "maxItems": .integer(257)
        ])))
    }

    func testComposedReferenceValidationHasACumulativeWorkBound() throws {
        var definitions: [String: DecodedJSONValue] = [
            "leaf": .object(["type": .string("object")])
        ]
        for level in (0 ..< 7).reversed() {
            let next = level == 6 ? "leaf" : "level\(level + 1)"
            definitions["level\(level)"] = .object([
                "allOf": .array(Array(repeating: .object([
                    "$ref": .string("#/$defs/\(next)")
                ]), count: 4))
            ])
        }
        let schema: DecodedJSONValue = .object([
            "$defs": .object(definitions), "$ref": .string("#/$defs/level0")
        ])
        try NativeChatMCPInputSchema.validateSchema(schema)
        XCTAssertThrowsError(try NativeChatMCPInputSchema.accepts(.object([:]), schema: schema))

        let bounded: DecodedJSONValue = .object([
            "$defs": .object(definitions),
            "allOf": .array(Array(repeating: .object(["$ref": .string("#/$defs/leaf")]), count: 2))
        ])
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([:]), schema: bounded))
    }

    func testSchemaBoundsKeepLargeIntegerPrecisionAndRejectUnrepresentableCounts() throws {
        let maximum: DecodedJSONValue = .object(["maximum": .integer(9_007_199_254_740_992)])
        XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
            .integer(9_007_199_254_740_993), schema: maximum
        ))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
            .integer(9_007_199_254_740_992), schema: maximum
        ))
        let invalidCount: DecodedJSONValue = .object([
            "type": .string("array"), "maxItems": .unsignedInteger(UInt64.max)
        ])
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(invalidCount))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(.object([
            "minLength": .decimal(Decimal(-1))
        ])))
        let integerType: DecodedJSONValue = .object(["type": .string("integer")])
        let largeInteger = try XCTUnwrap(Decimal(string: "18446744073709551615"))
        XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(largeInteger), schema: integerType))
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
private final class BridgeMCPService: NativeChatMCPServicing {
    private(set) var calls = 0
    var holdCalls = false
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
        Self.catalog
    }

    func refreshTools(accountId _: String) async throws -> DecodedJSONValue {
        Self.catalog
    }

    func disconnect(accountId _: String) async throws {}

    func callTool(
        accountId _: String, name _: String, arguments _: [String: DecodedJSONValue]
    ) async throws -> MemberMCPClient.ToolResult {
        calls += 1
        callStarted?.resume()
        callStarted = nil
        if holdCalls { await withCheckedContinuation { pendingCall = $0 } }
        try Task.checkCancellation()
        return try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(
            #"{"content":[],"structuredContent":{"quote":"<external-content>quoted text</external-content>"},"isError":false}"#
                .utf8
        ))
    }

    private static let catalog: DecodedJSONValue = .object(["tools": .array([.object([
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
        ])
    ])])])
}
