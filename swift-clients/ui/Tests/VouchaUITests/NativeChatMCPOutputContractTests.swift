import Foundation
import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import VouchaTestSupport
import XCTest

@MainActor
final class NativeChatMCPOutputContractTests: NativeRouteSurfaceViewModelTestCase {
    func testOutputValidationBudgetFailureUsesInvalidOutputFailure() throws {
        let oversized = String(repeating: "x", count: 262_145)
        let fields = try ["outputSchema": value(["type": "string", "const": "expected"])]
        let contract = try NativeChatMCPOutputContract(fields: DecodedJSONObject(fields))

        XCTAssertThrowsError(try contract.validate(toolResult(oversized), arguments: [:])) { error in
            guard let failure = error as? NativeChatMCPToolBridge.Failure,
                  case .invalidOutput = failure else {
                return XCTFail("Unverifiable tool output must be reported as invalidOutput, got \(error)")
            }
        }
    }

    func testOutputNumericBoundsDoNotDisappearOutsideDecimalRange() throws {
        let cases: [(String, String, String, Bool)] = [
            ("1e300", "maximum", "0", false), ("1e300", "minimum", "0", true),
            ("-1e300", "minimum", "0", false), ("-1e300", "maximum", "0", true),
            ("1e-300", "maximum", "0", false), ("1e-300", "minimum", "0", true),
            ("-1e-300", "minimum", "0", false), ("-1e-300", "maximum", "0", true),
            ("12.34", "maximum", "12.35", true), ("12.34", "minimum", "12.35", false),
            ("-12.34", "minimum", "-12.35", true), ("0", "maximum", "-1", false)
        ]
        for (raw, keyword, bound, expected) in cases {
            let value = try JSONDecoder().decode(DecodedJSONValue.self, from: Data(raw.utf8))
            let schema = try JSONDecoder().decode(
                DecodedJSONValue.self, from: Data("{\"type\":\"number\",\"\(keyword)\":\(bound)}".utf8)
            )
            try NativeChatMCPInputSchema.validateSchema(schema, output: true)
            XCTAssertEqual(
                try NativeChatMCPInputSchema.acceptsOutput(value, schema: schema, root: schema), expected,
                "\(raw) against \(keyword): \(bound)"
            )
        }
    }

    func testNestedOutputNumericBoundsApplyThroughReferencesAndCompositions() throws {
        let schema = try JSONDecoder().decode(DecodedJSONValue.self, from: Data(#"""
        {"type":"object","properties":{"samples":{"type":"array","items":{"anyOf":[
            {"$ref":"#/$defs/limited"},{"type":"null"}]}}},"required":["samples"],
         "$defs":{"limited":{"type":"number","maximum":0}}}
        """#.utf8))
        try NativeChatMCPInputSchema.validateSchema(schema, output: true)
        for (raw, expected) in [("1e300", false), ("-1e300", true)] {
            let value = try JSONDecoder().decode(
                DecodedJSONValue.self, from: Data("{\"samples\":[\(raw),null]}".utf8)
            )
            XCTAssertEqual(try NativeChatMCPInputSchema.acceptsOutput(value, schema: schema, root: schema), expected)
        }
    }

    func testCurrentProducerOutputSchemasAndRealResultFixturesValidate() async throws {
        let tools = try publishedTools()
        let catalog = try value(["tools": tools])
        let bridge = try NativeChatMCPToolBridge(catalog: catalog)
        XCTAssertEqual(bridge.tools.count, tools.count)
        let service = BridgeMCPService()
        service.catalog = catalog
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        for projected in bridge.tools {
            let published = try XCTUnwrap(tools.first { $0["name"] as? String == projected.name })
            let actual = try XCTUnwrap(JSONSerialization.jsonObject(with: projected.parameters) as? NSDictionary)
            XCTAssertTrue(try actual.isEqual(to: XCTUnwrap(published["inputSchema"] as? [String: Any])))
        }
        let fixtures = try fixtureObject("mcp-results.json")
        for row in try XCTUnwrap(fixtures["cases"] as? [[String: Any]]) {
            let name = try XCTUnwrap(row["tool"] as? String)
            let tool = try XCTUnwrap(tools.first { $0["name"] as? String == name })
            let fields = try object(value(tool))
            let arguments = try object(value(XCTUnwrap(row["arguments"])))
            let result = try toolResult(XCTUnwrap(row["structuredContent"]))
            try NativeChatMCPOutputContract(fields: fields).validate(result, arguments: arguments)
            let raw = try XCTUnwrap(String(
                data: JSONSerialization.data(withJSONObject: XCTUnwrap(row["arguments"])),
                encoding: .utf8
            ))
            try bridge.validate(name: name, rawArguments: raw)
            service.toolResult = result
            let envelope = try await bridge.execute(
                name: name,
                rawArguments: raw,
                session: session,
                accountId: snapshot.accountId,
                revision: snapshot.revision
            )
            let decoded = try object(JSONDecoder().decode(DecodedJSONValue.self, from: Data(envelope.utf8)))
            XCTAssertTrue(try NativeChatMCPInputSchema.same(
                XCTUnwrap(decoded["structuredContent"]),
                value(XCTUnwrap(row["structuredContent"]))
            ))
        }
    }

    func testRealProducerResultsRejectMissingNullEnumDateAndCoercedFacts() throws {
        let tools = try publishedTools()
        let fixtures = try XCTUnwrap(fixtureObject("mcp-results.json")["cases"] as? [[String: Any]])
        for (id, mutation) in [
            ("native.mcp.read-my-profile.bio", "missing"),
            ("native.mcp.read-my-profile.bio", "null"),
            ("native.mcp.read-my-profile.links", "enum"),
            ("native.mcp.read-feed.posts", "date"),
            ("native.mcp.read-feed.posts", "boolean")
        ] {
            let fixture = try XCTUnwrap(fixtures.first { $0["id"] as? String == id })
            var structured = try XCTUnwrap(fixture["structuredContent"] as? [String: Any])
            switch mutation {
            case "missing": structured.removeValue(forKey: "profile")
            case "null":
                var profile = try XCTUnwrap(structured["profile"] as? [String: Any])
                profile["markdown"] = NSNull()
                structured["profile"] = profile
            case "enum", "date":
                var results = try XCTUnwrap(structured["results"] as? [[String: Any]])
                results[0][mutation == "enum" ? "link_type" : "shared_at"] = mutation == "enum" ? "unknown" : "2025-02-29T00:00:00Z"
                structured["results"] = results
            default:
                var page = try XCTUnwrap(structured["page_info"] as? [String: Any])
                page["has_next_page"] = "false"
                structured["page_info"] = page
            }
            let name = try XCTUnwrap(fixture["tool"] as? String)
            let tool = try XCTUnwrap(tools.first { $0["name"] as? String == name })
            let contract = try NativeChatMCPOutputContract(fields: object(value(tool)))
            let arguments = try object(value(XCTUnwrap(fixture["arguments"])))
            XCTAssertThrowsError(try contract.validate(toolResult(structured), arguments: arguments), mutation)
        }
    }

    func testRealCanonicalOptionToolRequiresMappingAndOutputSchema() throws {
        let original = try XCTUnwrap(publishedTools().first { $0["name"] as? String == "read_my_profile" })
        for removed in ["meta", "mapping", "output"] {
            var tool = original
            if removed == "meta" { tool.removeValue(forKey: "_meta") }
            else if removed == "output" { tool.removeValue(forKey: "outputSchema") }
            else {
                var meta = try XCTUnwrap(tool["_meta"] as? [String: Any])
                meta.removeValue(forKey: "voucha/optionOutputPointers")
                tool["_meta"] = meta
            }
            XCTAssertThrowsError(try NativeChatMCPToolBridge(catalog: value(["tools": [tool]])), removed)
        }
    }

    func testCanonicalOptionSchemaRejectsDuplicateOptionLiterals() throws {
        for hasOutputMapping in [false, true] {
            var tool = mappedTool()
            var input = try XCTUnwrap(tool["inputSchema"] as? [String: Any])
            var branches = try XCTUnwrap(input["oneOf"] as? [[String: Any]])
            var second = branches[1]
            var properties = try XCTUnwrap(second["properties"] as? [String: Any])
            properties["option"] = ["const": "bio"]
            properties["arguments"] = ["type": "string"]
            second["properties"] = properties
            branches[1] = second
            input["oneOf"] = branches
            tool["inputSchema"] = input
            if !hasOutputMapping { tool.removeValue(forKey: "_meta") }

            XCTAssertThrowsError(try NativeChatMCPToolBridge(catalog: value(["tools": [tool]])))
        }
    }

    func testOrdinaryOneOfToolKeepsItsAdvertisedUnionWithoutAnOptionMap() throws {
        let tool: [String: Any] = [
            "name": "measure", "inputSchema": ["type": "object", "oneOf": [
                ["properties": ["unit": ["const": "meters"]], "required": ["unit"]],
                ["properties": ["unit": ["const": "feet"]], "required": ["unit"]]
            ]], "outputSchema": [
                "type": "object",
                "properties": ["value": ["type": "number"]],
                "required": ["value"],
                "additionalProperties": false
            ]
        ]
        XCTAssertNoThrow(try NativeChatMCPToolBridge(catalog: value(["tools": [tool]])))
    }

    func testOutputDefinitionsRemainSeparateFromInputDefinitions() throws {
        let fields = try object(value([
            "inputSchema": ["type": "object", "$defs": ["S1": ["type": "string"]]],
            "outputSchema": [
                "type": "object",
                "properties": ["id": ["$ref": "#/$defs/S1"]],
                "required": ["id"],
                "additionalProperties": false,
                "$defs": ["S1": ["type": "integer"]]
            ]
        ]))
        let contract = try NativeChatMCPOutputContract(fields: fields)
        XCTAssertNoThrow(try contract.validate(toolResult(["id": 12]), arguments: [:]))
        XCTAssertThrowsError(try contract.validate(toolResult(["id": "12"]), arguments: [:]))
        XCTAssertThrowsError(try contract.validate(toolResult(["id": 12, "extra": 1]), arguments: [:]))
        XCTAssertThrowsError(try contract.validate(toolResult([:]), arguments: [:]))
    }

    func testOutputDefinitionReferencesDecodeOnlyRFC6901Escapes() throws {
        let fields = try object(value([
            "inputSchema": ["type": "object"],
            "outputSchema": [
                "type": "object",
                "properties": [
                    "slash": ["$ref": "#/$defs/a~1b"],
                    "tilde": ["$ref": "#/$defs/a~0b"],
                    "escapedZeroThenOne": ["$ref": "#/$defs/a~01"]
                ],
                "required": ["slash", "tilde", "escapedZeroThenOne"],
                "$defs": [
                    "a/b": ["type": "string"],
                    "a~b": ["type": "integer"],
                    "a~1": ["type": "boolean"]
                ]
            ]
        ]))
        let contract = try NativeChatMCPOutputContract(fields: fields)
        try contract.validate(
            toolResult(["slash": "ok", "tilde": 4, "escapedZeroThenOne": true]), arguments: [:]
        )
        XCTAssertThrowsError(try contract.validate(
            toolResult(["slash": 4, "tilde": 4, "escapedZeroThenOne": true]), arguments: [:]
        ))

        for pointer in ["#/$defs/a/b", "#/$defs/a~2b", "#/$defs/a~"] {
            let invalidFields = try object(value([
                "inputSchema": ["type": "object"],
                "outputSchema": ["type": "object", "properties": ["value": ["$ref": pointer]]]
            ]))
            XCTAssertThrowsError(try NativeChatMCPOutputContract(fields: invalidFields), pointer)
        }
    }

    func testSelectedOptionAlsoEnforcesOutputRootConstraints() throws {
        var tool = mappedTool()
        var output = try XCTUnwrap(tool["outputSchema"] as? [String: Any])
        var branches = try XCTUnwrap(output["anyOf"] as? [[String: Any]])
        for index in branches.indices {
            branches[index].removeValue(forKey: "additionalProperties")
        }
        output["anyOf"] = branches
        output["properties"] = ["scope": ["const": "member"]]
        output["required"] = ["scope"]
        tool["outputSchema"] = output
        let contract = try NativeChatMCPOutputContract(fields: object(value(tool)))

        try contract.validate(toolResult(["bio": "Hello", "scope": "member"]), arguments: ["option": .string("bio")])
        XCTAssertThrowsError(try contract.validate(
            toolResult(["bio": "Hello", "scope": "staff"]), arguments: ["option": .string("bio")]
        ))
        XCTAssertThrowsError(try contract.validate(
            toolResult(["bio": "Hello"]), arguments: ["option": .string("bio")]
        ))
    }

    func testExplicitOutputPointersRejectAnotherOptionsOtherwiseValidResult() throws {
        let contract = try NativeChatMCPOutputContract(fields: object(value(mappedTool())))
        try contract.validate(toolResult(["bio": "Hello"]), arguments: ["option": .string("bio")])
        try contract.validate(toolResult(["links": []]), arguments: ["option": .string("links")])
        XCTAssertThrowsError(try contract.validate(toolResult(["links": []]), arguments: ["option": .string("bio")]))
        XCTAssertThrowsError(try contract.validate(toolResult(["bio": "Hello"]), arguments: [:]))
        // MCP refusal outcomes are separate from the successful factual output contract.
        let refusal = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(
            #"{"content":[{"type":"text","text":"<external-content>refused</external-content>"}],"isError":true}"#.utf8
        ))
        XCTAssertNoThrow(try contract.validate(refusal, arguments: ["option": .string("bio")]))
    }

    func testMalformedOrIncompleteOutputPointersFailCatalogValidation() throws {
        for mapping in [
            ["bio": "#/anyOf/9", "links": "#/anyOf/1"],
            ["bio": "#/anyOf/00", "links": "#/anyOf/1"],
            ["bio": "https://example.com/schema", "links": "#/anyOf/1"],
            ["bio": "#/anyOf/0"],
            ["bio": "#/anyOf/0", "links": "#/anyOf/1", "unknown": "#"]
        ] {
            var tool = mappedTool()
            tool["_meta"] = ["voucha/optionOutputPointers": mapping]
            XCTAssertThrowsError(try NativeChatMCPToolBridge(catalog: value(["tools": [tool]])))
        }
    }

    func testOutputDateTimeIsCalendarValidAndInputSubsetIsUnchanged() throws {
        let schema = try value(["type": "string", "format": "date-time"])
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(schema))
        try NativeChatMCPInputSchema.validateSchema(schema, output: true)
        for timestamp in [
            "2026-10-09T00:00:00.000Z", "2024-02-29T23:59:59+02:00",
            "2026-10-09 00:00:00+0000", "0000-02-29T12:00:00Z", "2026-10-09\u{FEFF}00:00:00Z"
        ] {
            XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(.string(timestamp), schema: schema, root: schema))
        }
        for timestamp in [
            "2025-02-29T00:00:00Z", "2026-10-09\u{0085}00:00:00Z",
            "2026-10-09",
            "2026-10-09T00:00:00",
            "2026-10-09T24:00:00Z",
            "2026-10-09T00:00:00Z\n"
        ] {
            XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(.string(timestamp), schema: schema, root: schema))
        }
    }

    func testOutputDateTimeMatchesProducerLeapSecondPlacementFixture() throws {
        let fixture = try fixtureObject("mcp-date-time.json")
        XCTAssertEqual(fixture["version"] as? Int, 1)
        let schema = try value(XCTUnwrap(fixture["schema"]))
        let cases = try XCTUnwrap(fixture["cases"] as? [[String: Any]])
        for row in cases {
            let id = try XCTUnwrap(row["id"] as? String)
            let timestamp = try XCTUnwrap(row["value"] as? String)
            let expected = try XCTUnwrap(row["valid"] as? Bool)
            XCTAssertEqual(
                try NativeChatMCPInputSchema.acceptsOutput(.string(timestamp), schema: schema, root: schema),
                expected,
                id
            )
        }
    }

    func testInvalidMutationOutputStopsLocalModelLoopWithoutRedispatch() async throws {
        let service = BridgeMCPService()
        service.catalog = try value(["tools": publishedTools()])
        service.toolResult = try toolResult(["success": true])
        let cases = try XCTUnwrap(fixtureObject("mcp-results.json")["cases"] as? [[String: Any]])
        let fixture = try XCTUnwrap(cases.first { $0["id"] as? String == "native.mcp.manage-bookmark.set" })
        let rawArguments = try XCTUnwrap(String(
            data: JSONSerialization.data(withJSONObject: XCTUnwrap(fixture["arguments"])),
            encoding: .utf8
        ))
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
        CannedFeedURLProtocol.handlers["/v1/responses"] = try (JSONSerialization.data(withJSONObject: [
            "output": [[
                "type": "function_call",
                "call_id": "mutation-1",
                "name": "manage_bookmark",
                "arguments": rawArguments
            ]]
        ]), 200)
        let client = OpenAICompatibleResponsesClient(
            protocolClasses: [CannedFeedURLProtocol.self], addressResolver: OutputLoopResolver(),
            connectProbe: OutputLoopProbe()
        )
        let endpoint = try XCTUnwrap(makeLocalLLMConfiguration().selectedEndpoint)
        do {
            _ = try await client.generateAssistantResponseWithTools(
                request: LocalLLMToolRequest(
                    message: "Edit my biography",
                    history: [],
                    endpoint: endpoint,
                    apiKey: nil,
                    tools: bridge.tools
                ),
                isCurrent: { true },
                validate: { name, arguments in try bridge.validate(name: name, rawArguments: arguments) },
                execute: { name, arguments in
                    try await bridge.execute(
                        name: name,
                        rawArguments: arguments,
                        session: session,
                        accountId: snapshot.accountId,
                        revision: snapshot.revision
                    )
                }
            )
            XCTFail("Wrong-option output must not become model facts")
        } catch LocalLLMToolLoopFailure.partialExecution {}
        XCTAssertEqual(service.calls, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/v1/responses" }.count, 1)
    }

    private func mappedTool() -> [String: Any] {
        ["name": "edit_profile", "inputSchema": ["type": "object", "oneOf": ["bio", "links"].map { option in
            [
                "type": "object",
                "properties": ["option": ["enum": [option]], "arguments": ["type": "object"]],
                "required": ["option", "arguments"],
                "additionalProperties": false
            ] as [String: Any]
        }], "outputSchema": ["type": "object", "anyOf": [
            [
                "type": "object",
                "properties": ["bio": ["type": "string"]],
                "required": ["bio"],
                "additionalProperties": false
            ],
            [
                "type": "object",
                "properties": ["links": ["type": "array"]],
                "required": ["links"],
                "additionalProperties": false
            ]
        ]], "_meta": ["voucha/optionOutputPointers": ["bio": "#/anyOf/0", "links": "#/anyOf/1"]]]
    }

    private func publishedTools() throws -> [[String: Any]] {
        let manifest = try fixtureObject("mcp.json")
        let servers = try XCTUnwrap(manifest["servers"] as? [[String: Any]])
        let server = try XCTUnwrap(servers.first { $0["name"] as? String == "voucha-user-mcp" })
        return try XCTUnwrap(server["tools"] as? [[String: Any]]).map { try XCTUnwrap($0["tool"] as? [String: Any]) }
    }

    private func fixtureObject(_ file: String) throws -> [String: Any] {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/\(file)"])
        let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/\(file)", root: root)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
    }

    private func value(_ object: Any) throws -> DecodedJSONValue {
        try JSONDecoder().decode(DecodedJSONValue.self, from: JSONSerialization.data(withJSONObject: object))
    }

    private func object(_ value: DecodedJSONValue) throws -> DecodedJSONObject {
        guard case let .object(fields) = value else { throw NativeChatMCPToolBridge.Failure.invalidCatalog }
        return fields
    }

    private func toolResult(_ structured: Any) throws -> MemberMCPClient.ToolResult {
        try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: JSONSerialization.data(
            withJSONObject: ["content": [], "structuredContent": structured, "isError": false]
        ))
    }
}

private struct OutputLoopResolver: LocalLLMAddressResolving {
    func addresses(for _: String) async throws -> [LocalLLMIPAddress] {
        try [XCTUnwrap(LocalLLMIPAddress.parse("127.0.0.1"))]
    }
}

private struct OutputLoopProbe: LocalLLMConnectProbing {
    func canConnect(to _: LocalLLMIPAddress, port _: Int, timeout _: TimeInterval) async throws -> Bool {
        true
    }
}
