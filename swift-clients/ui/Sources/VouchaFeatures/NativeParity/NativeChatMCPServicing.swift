import Foundation
import VouchaAPI
import VouchaModels

@MainActor
public protocol NativeChatMCPServicing: AnyObject {
    func connect(accountId: String) async throws -> DecodedJSONValue
    func connect(accountId: String, presentationWindow: AnyObject?) async throws -> DecodedJSONValue
    func refreshTools(accountId: String) async throws -> DecodedJSONValue
    func callTool(accountId: String, name: String, arguments: [String: DecodedJSONValue]) async throws
        -> MemberMCPClient.ToolResult
    func disconnect(accountId: String) async throws
}

public extension NativeChatMCPServicing {
    func connect(accountId: String, presentationWindow _: AnyObject?) async throws -> DecodedJSONValue {
        try await connect(accountId: accountId)
    }

    func callTool(
        accountId _: String,
        name _: String,
        arguments _: [String: DecodedJSONValue]
    ) async throws -> MemberMCPClient.ToolResult {
        throw CancellationError()
    }
}
