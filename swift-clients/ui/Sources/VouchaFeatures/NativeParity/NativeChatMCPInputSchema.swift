import Foundation
import VouchaModels

/// The published MCP catalog's JSON Schema subset. Unknown constraints fail closed.
enum NativeChatMCPInputSchema {
    enum Failure: Error { case unsupportedSchema }

    /// Bounds the pairwise `uniqueItems` comparison. The producer advertises up to 500 follower
    /// recipients (the DynamicConfig hard max), so the bound sits just above it.
    static let maximumUniqueItems = 512

    static func validateSchema(_ root: DecodedJSONValue, output: Bool = false) throws {
        try inspect(root, root: root, depth: 0, output: output)
    }

    static func accepts(_ value: DecodedJSONValue, schema: DecodedJSONValue) throws -> Bool {
        var remainingWork = 4_096
        return try check(value, schema: schema, root: schema, depth: 0, remainingWork: &remainingWork)
    }

}
