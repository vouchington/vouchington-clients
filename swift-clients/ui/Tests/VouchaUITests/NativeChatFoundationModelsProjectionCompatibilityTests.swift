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

        func testFixedStringsSatisfyingSupportedUpperPatternAndFormatConstraintsProject() throws {
            let accepted: [(String, String)] = [
                (#"{"type":"string","enum":["ok"],"maxLength":2}"#, "ok"),
                (#"{"type":"string","enum":["ok",null],"maxLength":2}"#, "ok"),
                (#"{"enum":["ok",null],"maxLength":2}"#, "ok"),
                (#"{"type":"string","const":"e\u0301","maxLength":2}"#, "e\u{301}"),
                (#"{"enum":["ok"],"maxLength":2}"#, "ok"),
                (#"{"type":"string","enum":["member_1"],"pattern":"^[a-z][a-z0-9_]{0,99}$"}"#, "member_1"),
                (
                    #"{"type":"string","const":"00000000-0000-7000-8000-000000000001","format":"uuid"}"#,
                    "00000000-0000-7000-8000-000000000001"
                ),
                (
                    #"{"type":"string","enum":["https://example.test/path"],"format":"uri"}"#,
                    "https://example.test/path"
                )
            ]
            for (property, value) in accepted {
                let raw = Data(#"{"type":"object","properties":{"value":\#(property)}}"#.utf8)
                let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
                try NativeChatMCPInputSchema.validateSchema(authoritative)
                XCTAssertTrue(try NativeChatMCPInputSchema.accepts(
                    .object(["value": .string(value)]), schema: authoritative
                ))
                XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(raw))
            }

            let rejected: [(String, String)] = [
                (#"{"type":"string","enum":["e\u0301"],"maxLength":1}"#, "e\u{301}"),
                (#"{"type":"string","enum":["ok","long"],"maxLength":2}"#, "long"),
                (#"{"type":"string","enum":["ok","long",null],"maxLength":2}"#, "long"),
                (#"{"type":"string","enum":[null],"maxLength":2}"#, "anything"),
                (#"{"type":"string","const":"TooLong","pattern":"^[a-z][a-z0-9_]{0,99}$"}"#, "TooLong"),
                (#"{"type":"string","enum":["not-a-uuid"],"format":"uuid"}"#, "not-a-uuid")
            ]
            for (property, value) in rejected {
                let raw = Data(#"{"type":"object","properties":{"value":\#(property)}}"#.utf8)
                let authoritative = try JSONDecoder().decode(DecodedJSONValue.self, from: raw)
                try NativeChatMCPInputSchema.validateSchema(authoritative)
                XCTAssertFalse(try NativeChatMCPInputSchema.accepts(
                    .object(["value": .string(value)]), schema: authoritative
                ))
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))
            }

            for property in [
                #"{"type":"string","maxLength":2}"#,
                #"{"type":"string","const":"ok","pattern":"^ok$"}"#,
                #"{"type":"string","const":"ok","format":"email"}"#
            ] {
                let raw = Data(#"{"type":"object","properties":{"value":\#(property)}}"#.utf8)
                XCTAssertThrowsError(try NativeChatFoundationModelsSchema.project(raw))
            }

            let referenced = Data(
                ##"{"type":"object","$defs":{"Code":{"type":"string","enum":["ok",null],"maxLength":2}},"properties":{"value":{"$ref":"#/$defs/Code"}}}"##
                    .utf8
            )
            XCTAssertNoThrow(try NativeChatFoundationModelsSchema.project(referenced))
        }
    }
#endif
