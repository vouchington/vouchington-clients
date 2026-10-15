import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsDefinitionTests: XCTestCase {
        func testReachableDefinitionKeepsExplicitTypeDespiteIgnoredProperties() throws {
            for (type, accepted) in [
                ("string", DecodedJSONValue.string("example")),
                ("array", DecodedJSONValue.array([])),
                ("boolean", DecodedJSONValue.bool(true)),
                ("null", DecodedJSONValue.null)
            ] {
                let raw = Data(
                    (##"{"type":"object","properties":{"value":{"$ref":"#/$defs/Bad"}},"$defs":{"Bad":{"type":""##
                        + type + #"","properties":{"id":{"type":"string"}}}}}"#).utf8
                )
                let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
                XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.object([
                    "value": .object(["id": .string("example")])
                ]), schema: authoritative))
                XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([
                    "value": accepted
                ]), schema: authoritative))
                XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))
            }
        }

        func testOnlyTransitivelyReachableDefinitionsAreProjected() throws {
            let raw = Data(
                ##"{"type":"object","properties":{"value":{"$ref":"#/$defs/First"}},"$defs":{"First":{"type":"object","properties":{"next":{"$ref":"#/$defs/Second"}}},"Second":{"type":"string","enum":["ready"]},"Unused":{"type":"string","pattern":"blocked"}}}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object([
                "value": .object(["next": .string("ready")])
            ]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.object([
                "value": .object(["next": .string("other")])
            ]), schema: authoritative))
            let projected = try NativeChatFoundationModelsSchema.project(raw)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("First"))
            XCTAssertTrue(encoded.contains("Second"))
            XCTAssertFalse(encoded.contains("Unused"))
        }

        func testMissingReachableDefinitionFailsClosedWithoutWalkingUnusedCycles() throws {
            let missing = Data(
                ##"{"type":"object","properties":{"value":{"$ref":"#/$defs/Missing"}},"$defs":{"Unused":{"$ref":"#/$defs/Unused"}}}"##
                    .utf8
            )
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(missing))
            let unusedCycle = Data(
                ##"{"type":"object","properties":{"value":{"type":"string"}},"$defs":{"Unused":{"$ref":"#/$defs/Unused"}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(unusedCycle))
            let defaultData = Data(
                ##"{"type":"object","default":{"$ref":"#/$defs/NotASchemaRef"},"properties":{"value":{"type":"string"}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(defaultData))
        }

        func testNumberProjectionKeepsOnlyEncodableIntersection() throws {
            for raw in [
                ##"{"type":"number","minimum":9007199254740992}"##,
                ##"{"type":"number","maximum":-9007199254740992}"##
            ] {
                let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: Data(raw.utf8))
                XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.integer(0), schema: authoritative))
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(raw.utf8)))
            }
            let raw = Data(##"{"type":"number","minimum":-9007199254740992,"maximum":9007199254740992}"##.utf8)
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.integer(0), schema: authoritative))
            let projected = try NativeChatFoundationModelsSchema.projectWithProperty(raw)
            let encoded = try XCTUnwrap(String(data: JSONEncoder().encode(projected), encoding: .utf8))
            XCTAssertTrue(encoded.contains("9007199254740991"))
            XCTAssertFalse(encoded.contains("9007199254740992"))
        }
    }
#endif
