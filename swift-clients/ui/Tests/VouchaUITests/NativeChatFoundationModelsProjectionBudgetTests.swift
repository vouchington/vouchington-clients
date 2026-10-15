import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    import FoundationModels

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
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(impossible))

            let possible = Data(#"{"type":"number","minimum":0.1,"maximum":0.10000000000000001}"#.utf8)
            let projected = try NativeChatFoundationModelsSchema.projectWithProperty(possible)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("0.1"))
            XCTAssertFalse(encoded.contains("0.100000000000000005"))
        }

        func testArrayMinimumMustFitBoundedModelSession() throws {
            let excessive = Data(#"{"type":"array","items":{"type":"null"},"minItems":10000}"#.utf8)
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(excessive))
            let allowed = Data(#"{"type":"array","items":{"type":"null"},"minItems":64,"maxItems":10000}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: allowed)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .array(Array(repeating: .null, count: 64)),
                schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(allowed))
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

        func testNestedRequiredArraysShareOneOutputBudget() throws {
            let excessive = Data(
                #"{"type":"array","minItems":64,"items":{"type":"array","minItems":64,"items":{"type":"null"}}}"#.utf8
            )
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(excessive))

            let bounded = Data(
                #"{"type":"object","required":["rows"],"properties":{"rows":{"type":"array","minItems":4,"items":{"type":"array","minItems":4,"items":{"type":"null"}}}}}"#
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(bounded))

            let referenced = Data(
                ##"{"type":"array","minItems":64,"items":{"$ref":"#/$defs/Row"},"$defs":{"Row":{"type":"array","minItems":64,"items":{"type":"null"}}}}"##
                    .utf8
            )
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(referenced))
        }

        func testNullOnlyEnumAndAnnotationOnlySchemaRemainProjectable() throws {
            let nullOnly = Data(#"{"enum":[null]}"#.utf8)
            let nullSchema = try JSONDecoder().decode(DecodedJSONValue.self, from: nullOnly)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.null, schema: nullSchema))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("other"), schema: nullSchema))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(nullOnly))

            let annotationOnly = Data(
                #"{"description":"Arbitrary JSON payload","default":{"ok":true},"examples":[null,{"ok":true}]}"#.utf8
            )
            let arbitrarySchema = try JSONDecoder().decode(DecodedJSONValue.self, from: annotationOnly)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object(["ok": .bool(true)]), schema: arbitrarySchema))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(annotationOnly))

            let nested = Data(
                ##"{"type":"object","properties":{"choice":{"$ref":"#/$defs/NullChoice"},"payload":{"description":"Any JSON"}},"$defs":{"NullChoice":{"enum":[null]}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(nested))

            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(#"{"minimum":1}"#.utf8)))
        }

        func testTypedNullEnumProjectsOnlyItsAcceptedValue() throws {
            let valid = Data(#"{"type":"null","enum":[null]}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: valid)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.null, schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.string("other"), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(valid))

            for raw in [
                #"{"type":"null","enum":[]}"#,
                #"{"type":"null","enum":["other"]}"#,
                #"{"type":"null","enum":[null,"other"]}"#
            ] {
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.projectWithProperty(Data(raw.utf8)))
            }
        }

        func testUnconstrainedRootArgumentsStillGenerateAnObject() throws {
            for raw in [
                #"{}"#,
                #"{"description":"Arbitrary JSON arguments","examples":[{"id":"one"}]}"#
            ] {
                let projected = try NativeChatFoundationModelsSchema.project(Data(raw.utf8))
                let encoded = try XCTUnwrap(JSONSerialization.jsonObject(
                    with: JSONEncoder().encode(projected)
                ) as? [String: Any])
                XCTAssertEqual(encoded["type"] as? String, "object")
                XCTAssertNotNil(encoded["properties"] as? [String: Any])
            }
            XCTAssertNoThrow(try NativeChatFoundationModelsTool.jsonArguments(GeneratedContent(json: "{}")))
            XCTAssertThrowsError(try NativeChatFoundationModelsTool.jsonArguments(GeneratedContent(json: #""scalar""#)))
        }

        func testForcedLiteralBytesMultiplyThroughRequiredArray() throws {
            let long = String(repeating: "x", count: 1_000)
            let item = ["const": long]
            let schema: [String: Any] = [
                "type": "object", "required": ["items"],
                "properties": ["items": ["type": "array", "minItems": 64, "items": item]]
            ]
            let raw = try JSONSerialization.data(withJSONObject: schema)
            XCTAssertLessThan(raw.count, NativeChatFoundationModelsToolSelection.maximumCatalogBytes)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([
                "items": .array(Array(repeating: .string(long), count: 64))
            ]), schema: authoritative))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))

            let short: [String: Any] = [
                "type": "object", "required": ["items"],
                "properties": ["items": ["type": "array", "minItems": 64, "items": ["const": "x"]]]
            ]
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema
                .project(JSONSerialization.data(withJSONObject: short)))
        }

        func testAcyclicReferencesBeyondAuthoritativeDepthFailProjection() throws {
            var definitions: [String: Any] = [:]
            for index in 0 ..< 32 {
                definitions["Step\(index)"] = index == 31
                    ? ["type": "string"]
                    : ["$ref": "#/$defs/Step\(index + 1)"]
            }
            let schema: [String: Any] = [
                "type": "object", "required": ["value"],
                "properties": ["value": ["$ref": "#/$defs/Step0"]], "$defs": definitions
            ]
            let raw = try JSONSerialization.data(withJSONObject: schema)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertThrowsError(try NativeChatMCPInputSchema.accepts(
                .object(["value": .string("ok")]), schema: authoritative
            ))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))
        }

        func testArrayWithoutItemsKeepsTheValidEmptyArray() throws {
            let raw = Data(#"{"type":"array","maxItems":0}"#.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.array([]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.array([.null]), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.projectWithProperty(raw))
        }

        func testRootChoiceCannotGenerateScalarToolArguments() throws {
            for raw in [
                #"{"anyOf":[{"type":"object","properties":{"id":{"type":"string"}}},{"type":"string"}]}"#,
                #"{"oneOf":[{"type":"object","properties":{"id":{"type":"string"}}},{"type":"string"}]}"#,
                #"{"type":["object","string"]}"#
            ] {
                let schema = Data(raw.utf8)
                let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: schema)
                XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.string("scalar"), schema: authoritative))
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(schema))
            }
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(Data(
                #"{"anyOf":[{"type":"object","properties":{"id":{"type":"string"}}},{"type":"object","properties":{"name":{"type":"string"}}}]}"#
                    .utf8
            )))
            for raw in [
                #"{"type":"string"}"#,
                #"{"enum":["one","two"]}"#,
                ##"{"$ref":"#/$defs/Value","$defs":{"Value":{"type":"string"}}}"##
            ] {
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(raw.utf8)))
            }
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(Data(
                ##"{"$ref":"#/$defs/Args","$defs":{"Args":{"type":"object","properties":{"id":{"type":"string"}}}}}"##
                    .utf8
            )))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                #"{"anyOf":[{}]}"#.utf8
            )))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                ##"{"$ref":"#/$defs/Anything","$defs":{"Anything":{}}}"##.utf8
            )))
        }

        func testStructuralDepthAndReferenceDepthShareValidatorLimit() throws {
            var definitions: [String: Any] = [:]
            for index in 0 ..< 16 {
                definitions["Step\(index)"] = index == 15
                    ? ["type": "string"] : ["$ref": "#/$defs/Step\(index + 1)"]
            }
            var value: [String: Any] = ["$ref": "#/$defs/Step0"]
            for _ in 0 ..< 16 {
                value = ["type": "array", "minItems": 1, "items": value]
            }
            let root: [String: Any] = [
                "type": "object", "required": ["value"], "properties": ["value": value], "$defs": definitions
            ]
            let raw = try JSONSerialization.data(withJSONObject: root)
            var argument: DecodedJSONValue = .string("ok")
            for _ in 0 ..< 16 {
                argument = .array([argument])
            }
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertThrowsError(try NativeChatMCPInputSchema.accepts(
                .object(["value": argument]),
                schema: authoritative
            ))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))
        }

        func testRequiredCompositionFanoutRespectsValidatorWorkBudget() throws {
            let branches: [[String: Any]] = Array(repeating: ["type": "string"], count: 64)
            var properties: [String: Any] = [:]
            var required: [String] = []
            var arguments: [String: DecodedJSONValue] = [:]
            for index in 0 ..< 64 {
                let key = "field\(index)"
                properties[key] = ["anyOf": branches]
                required.append(key)
                arguments[key] = .string("ok")
            }
            let root: [String: Any] = [
                "type": "object", "required": required, "properties": properties
            ]
            let raw = try JSONSerialization.data(withJSONObject: root)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertThrowsError(try NativeChatMCPInputSchema.accepts(.object(arguments), schema: authoritative))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))
        }

        func testTypedStringEnumBudgetIgnoresIncompatibleShortNumericMember() throws {
            let long = String(repeating: "x", count: 1_000)
            let item: [String: Any] = ["type": "string", "enum": [long, 0]]
            let schema: [String: Any] = [
                "type": "object", "required": ["items"],
                "properties": ["items": ["type": "array", "minItems": 64, "items": item]]
            ]
            let raw = try JSONSerialization.data(withJSONObject: schema)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([
                "items": .array(Array(repeating: .string(long), count: 64))
            ]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.object([
                "items": .array(Array(repeating: .integer(0), count: 64))
            ]), schema: authoritative))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func projectWithProperty(_ data: Data) throws -> GenerationSchema {
            let property = try JSONSerialization.jsonObject(with: data)
            let wrapper: [String: Any] = [
                "type": "object", "properties": ["value": property], "required": ["value"]
            ]
            return try project(JSONSerialization.data(withJSONObject: wrapper))
        }
    }
#endif
