import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsProjectionBudgetTests: XCTestCase {
        func testDecimalGuideRequiresAtLeastOneJSONSerializableDouble() throws {
            let impossible = Data(#"{"type":"number","minimum":0.100000000000000005,"maximum":0.100000000000000005}"#
                .utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: impossible)
            let exact = try XCTUnwrap(Decimal(string: "0.100000000000000005"))
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.decimal(exact), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.number(0.1), schema: authoritative))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(impossible))

            let possible = Data(#"{"type":"number","minimum":0.1,"maximum":0.10000000000000001}"#.utf8)
            let projected = try NativeChatFoundationModelsSchema.project(possible)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("0.1"))
            XCTAssertFalse(encoded.contains("0.100000000000000005"))
        }

        func testArrayMinimumMustFitBoundedModelSession() throws {
            let excessive = Data(#"{"type":"array","items":{"type":"null"},"minItems":10000}"#.utf8)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(excessive))
            let allowed = Data(#"{"type":"array","items":{"type":"null"},"minItems":64,"maxItems":10000}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: allowed)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .array(Array(repeating: .null, count: 64)),
                schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(allowed))
        }

        func testGeneratedNodeNameSkipsReachableDefinitionName() throws {
            let raw = Data(
                ##"{"type":"object","$defs":{"MCPNode_1":{"type":"string","enum":["ready"]}},"properties":{"value":{"$ref":"#/$defs/MCPNode_1"}}}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["value": .string("ready")]),
                schema: authoritative
            ))
            let projected = try NativeChatFoundationModelsSchema.project(raw)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("MCPNode_1"))
            XCTAssertTrue(encoded.contains("MCPNode_2"))
        }
    }
#endif
