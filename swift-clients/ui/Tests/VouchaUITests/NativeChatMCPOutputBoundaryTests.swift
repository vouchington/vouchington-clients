import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

final class NativeChatMCPOutputBoundaryTests: XCTestCase {
    func testLargeNumericEqualityAndNestedUniqueness() throws {
        for keyword in ["const", "enum"] {
            let assertion = keyword == "enum" ? "[1e300]" : "1e300"
            let schema = try decode("{\"type\":\"number\",\"\(keyword)\":\(assertion)}")
            XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(
                decode("1e300"), schema: schema, root: schema
            ))
            XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
                decode("2e300"), schema: schema, root: schema
            ))
        }
        let schema = try decode(#"{"type":"array","uniqueItems":true,"maxItems":256}"#)
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            decode(#"[{"value":[1e300]},{"value":[1e300]}]"#), schema: schema, root: schema
        ))
        XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(
            decode("[1e300,2e300]"), schema: schema, root: schema
        ))
    }

    func testLargeAdvertisedBoundsAreAdmittedAndEnforced() throws {
        let schema = try decode(#"{"type":"number","minimum":-1e300,"maximum":1e300}"#)
        try NativeChatMCPInputSchema.validateSchema(schema, output: true)
        XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(decode("1e300"), schema: schema, root: schema))
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(decode("2e300"), schema: schema, root: schema))
        XCTAssertThrowsError(try NativeChatMCPInputSchema.validateSchema(schema))
    }

    func testRootMappedOutputIsTraversedOnceWithinExistingBudget() throws {
        let tool = try decode(##"""
        {"inputSchema":{"oneOf":[{"type":"object","properties":{
        "option":{"const":"list"},"arguments":{"type":"object"}},
        "required":["option","arguments"],"additionalProperties":false}]},
        "outputSchema":{"type":"array","items":{"type":"null"}},
        "_meta":{"voucha/optionOutputPointers":{"list":"#"}}}
        """##)
        guard case let .object(fields) = tool else { return XCTFail("Expected tool object") }
        let items = Array(repeating: "null", count: 140_000).joined(separator: ",")
        let raw = "{\"content\":[],\"structuredContent\":[\(items)],\"isError\":false}"
        let result = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(raw.utf8))
        try NativeChatMCPOutputContract(fields: fields).validate(result, arguments: ["option": .string("list")])
    }

    func testAllowedPatternOutputRetainsValidationForLargeArrays() throws {
        let schema = try decode(#"{"type":"array","items":{"type":"string","pattern":"\\S"}}"#)
        try NativeChatMCPInputSchema.validateSchema(schema, output: true)
        XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(
            .array(Array(repeating: .string("x"), count: 100_000)), schema: schema, root: schema
        ))
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            .array([.string(" ")]), schema: schema, root: schema
        ))
    }

    func testSelectedRootAnyOfBranchSharesOneTraversalAndKeepsRootConstraints() throws {
        let schema =
            try decode(
                #"{"type":"array","maxItems":150000,"anyOf":[{"items":{"type":"null"}},{"items":{"type":"string"}}]}"#
            )
        XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(
            .array(Array(repeating: .null, count: 140_000)), schema: schema, root: schema,
            selectedSchema: nil, selectedRootAnyOfIndex: 0
        ))
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            .array([.string("other branch")]), schema: schema, root: schema, selectedRootAnyOfIndex: 0
        ))
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            .array(Array(repeating: .null, count: 150_001)), schema: schema, root: schema,
            selectedRootAnyOfIndex: 0
        ))
    }

    func testNestedDefinitionMatchCannotSatisfySelectedRootBranch() throws {
        let schema = try decode(##"""
        {"anyOf":[{"$ref":"#/$defs/a"},{"type":"object","properties":{"nested":{"$ref":"#/$defs/a"}},
        "required":["nested"]}],"$defs":{"a":{"type":"object","properties":{"kind":{"const":"a"}},
        "required":["kind"]}}}
        """##)
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            decode(#"{"nested":{"kind":"a"}}"#), schema: schema, root: schema, selectedRootAnyOfIndex: 0
        ))
    }

    func testOutputEqualityUsesExactUnicodeForValuesAndObjectNames() throws {
        let scalar = try decode(#"{"enum":["é"]}"#)
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            decode(#""e\u0301""#), schema: scalar, root: scalar
        ))
        let unique = try decode(#"{"type":"array","uniqueItems":true,"maxItems":256}"#)
        XCTAssertTrue(try NativeChatMCPInputSchema.acceptsOutput(
            decode(#"["é","e\u0301"]"#), schema: unique, root: unique
        ))
        let object = try decode(#"{"const":{"é":1}}"#)
        XCTAssertFalse(try NativeChatMCPInputSchema.acceptsOutput(
            decode(#"{"e\u0301":1}"#), schema: object, root: object
        ))
    }

    func testOutputEqualityChargesLongStringsAndWideObjects() throws {
        let schema = try decode(#"{"type":"array","uniqueItems":true,"maxItems":256}"#)
        let strings = (0 ..< 256).map { DecodedJSONValue.string(String(repeating: "x", count: 4_000) + String($0)) }
        XCTAssertThrowsError(try NativeChatMCPInputSchema.acceptsOutput(
            .array(strings), schema: schema, root: schema
        ))
        let objects = (0 ..< 256).map { index -> DecodedJSONValue in
            var fields = Dictionary(uniqueKeysWithValues: (0 ..< 100).map { ("key\($0)", DecodedJSONValue.null) })
            fields["index"] = .number(Double(index))
            return .object(fields)
        }
        XCTAssertThrowsError(try NativeChatMCPInputSchema.acceptsOutput(
            .array(objects), schema: schema, root: schema
        ))
    }

    private func decode(_ raw: String) throws -> DecodedJSONValue {
        try JSONDecoder().decode(DecodedJSONValue.self, from: Data(raw.utf8))
    }
}
