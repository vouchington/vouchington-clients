import Foundation
import VouchaModels

/// A 401 triggers one single-flight refresh; a second 401 requires a new browser authorization.
public actor MemberMCPAuthorizedClient {
    private let client: MemberMCPClient
    private let tokens: MemberMCPTokenManager

    public init(client: MemberMCPClient, tokens: MemberMCPTokenManager) {
        self.client = client
        self.tokens = tokens
    }

    public func listTools() async throws -> DecodedJSONValue {
        let access = try await tokens.accessToken()
        do {
            return try await client.listTools(accessToken: access)
        } catch MemberMCPClient.Failure.unauthorized {
            let refreshed = try await tokens.refresh(rejectedAccessToken: access)
            do {
                return try await client.listTools(accessToken: refreshed)
            } catch MemberMCPClient.Failure.unauthorized {
                try await tokens.clear()
                throw MemberMCPTokenManager.Failure.notAuthorized
            }
        }
    }

    public func callTool(name: String, arguments: [String: DecodedJSONValue]) async throws -> MemberMCPClient
        .ToolResult {
        let access = try await tokens.accessToken()
        do {
            return try await client.callTool(name: name, arguments: arguments, accessToken: access)
        } catch MemberMCPClient.Failure.unauthorized {
            let refreshed = try await tokens.refresh(rejectedAccessToken: access)
            do {
                return try await client.callTool(name: name, arguments: arguments, accessToken: refreshed)
            } catch MemberMCPClient.Failure.unauthorized {
                try await tokens.clear()
                throw MemberMCPTokenManager.Failure.notAuthorized
            }
        }
    }
}
