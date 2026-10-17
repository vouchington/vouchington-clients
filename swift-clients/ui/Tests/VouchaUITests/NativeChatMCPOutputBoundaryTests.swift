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

    private func decode(_ raw: String) throws -> DecodedJSONValue {
        try JSONDecoder().decode(DecodedJSONValue.self, from: Data(raw.utf8))
    }
}
