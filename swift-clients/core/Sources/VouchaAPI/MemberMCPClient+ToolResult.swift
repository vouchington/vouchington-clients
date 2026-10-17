import Foundation
import VouchaModels

public extension MemberMCPClient {
    struct ToolResult: Decodable, MCPResponseValue {
        public let content: [DecodedJSONValue]
        public let structuredContent: DecodedJSONValue?
        public let isError: Bool?

        static func decodeMCPResponse(_ value: DecodedJSONValue) throws -> ToolResult {
            guard case let .object(fields) = value,
                  case let .array(content)? = fields["content"] else {
                throw Failure.invalidResponse
            }
            let structured: DecodedJSONValue? = if let value = fields["structuredContent"], case .null = value {
                nil
            } else { fields["structuredContent"] }
            let isError: Bool? = if let value = fields["isError"], case .null = value {
                nil
            } else if case let .bool(value)? = fields["isError"] { value } else if fields["isError"] == nil {
                nil
            } else { throw Failure.invalidResponse }
            return ToolResult(content: content, structuredContent: structured, isError: isError)
        }
    }
}
