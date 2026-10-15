import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

#if canImport(FoundationModels)
    @MainActor
    @available(macOS 26.4, iOS 26.4, *)
    final class NativeChatFoundationModelsProjectionCompatibilityTests: XCTestCase {
        func testDeclaredObjectPropertiesCanIgnoreAdditionalPropertySchema() throws {
            let raw = Data(
                #"{"type":"object","properties":{"id":{"type":"string"}},"required":["id"],"additionalProperties":{"type":"string"}}"#
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["id": .string("one")]), schema: authoritative
            ))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
                .object(["id": .string("one"), "extra": .integer(7)]), schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))

            let referenced = Data(
                ##"{"type":"object","$defs":{"Entry":{"type":"object","properties":{"id":{"type":"string"}},"required":["id"],"additionalProperties":{"type":"string"}}},"properties":{"entry":{"$ref":"#/$defs/Entry"}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(referenced))
            XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(
                #"{"type":"object","additionalProperties":{"type":"string"}}"#.utf8
            )))
        }

        func testAssertionFreeSingletonAllOfProjectsRootAndNestedReference() throws {
            let raw = Data(
                ##"{"$defs":{"Id":{"type":"string","enum":["one"]}},"allOf":[{"type":"object","properties":{"id":{"$ref":"#/$defs/Id"}},"required":["id"]}]}"##
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object(["id": .string("one")]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.object(["id": .string("two")]), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))

            let nested = Data(
                #"{"type":"object","properties":{"entry":{"allOf":[{"type":"object","properties":{"id":{"type":"string"}}}]}}}"#
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(nested))
            for invalid in [
                #"{"allOf":[{"type":"object"},{"type":"object"}]}"#,
                #"{"type":"object","allOf":[{"type":"object"}]}"#
            ] {
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(invalid.utf8)))
            }
        }

        func testSafeIntegerConstantProjectsSingletonRange() throws {
            let raw = Data(
                #"{"type":"object","properties":{"count":{"type":"integer","const":7}}}"#.utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(.object(["count": .integer(7)]), schema: authoritative))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(.object(["count": .integer(8)]), schema: authoritative))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))
            for invalid in [
                #"{"type":"object","properties":{"count":{"type":"integer","const":7.5}}}"#,
                #"{"type":"object","properties":{"count":{"type":"integer","const":9007199254740992}}}"#,
                #"{"type":"object","properties":{"count":{"type":"integer","const":7,"minimum":0}}}"#
            ] {
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(invalid.utf8)))
            }
        }

        func testFixedStringMinimumCountsUnicodeScalars() throws {
            let raw = Data(
                #"{"type":"object","properties":{"name":{"type":"string","enum":["e\u0301"],"minLength":2},"code":{"type":"string","const":"ok","minLength":2}}}"#
                    .utf8
            )
            let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
            XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                .object(["name": .string("e\u{301}"), "code": .string("ok")]), schema: authoritative
            ))
            XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
                .object(["name": .string("é"), "code": .string("ok")]), schema: authoritative
            ))
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))
            for invalid in [
                #"{"type":"object","properties":{"name":{"type":"string","enum":["é"],"minLength":2}}}"#,
                #"{"type":"object","properties":{"name":{"type":"string","minLength":2}}}"#
            ] {
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(Data(invalid.utf8)))
            }
        }
    }
#endif
