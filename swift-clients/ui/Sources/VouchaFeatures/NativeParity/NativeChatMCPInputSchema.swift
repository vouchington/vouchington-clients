import Foundation
import VouchaModels

/// The published MCP catalog's JSON Schema subset. Unknown constraints fail closed.
enum NativeChatMCPInputSchema {
    enum Failure: Error { case unsupportedSchema }

    static func validateSchema(_ root: DecodedJSONValue) throws {
        try inspect(root, root: root, depth: 0)
    }

    static func accepts(_ value: DecodedJSONValue, schema: DecodedJSONValue) throws -> Bool {
        var remainingWork = 4_096
        return try check(value, schema: schema, root: schema, depth: 0, remainingWork: &remainingWork)
    }

}
