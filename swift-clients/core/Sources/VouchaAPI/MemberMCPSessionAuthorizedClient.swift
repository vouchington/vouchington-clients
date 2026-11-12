import Foundation
import VouchaModels

/// Bearer MCP calls authorized by a session-bound token. A 401 mints once more; a second 401 is
/// reported as not authorized rather than retried.
public actor MemberMCPSessionAuthorizedClient {
    private let client: MemberMCPClient
    private let tokens: MemberMCPSessionTokenSource

    public init(client: MemberMCPClient, tokens: MemberMCPSessionTokenSource) {
        self.client = client
        self.tokens = tokens
    }

    public func listTools(cursor: String? = nil) async throws -> DecodedJSONValue {
        try await withToken { access in try await self.client.listTools(accessToken: access, cursor: cursor) }
    }

    public func callTool(name: String, arguments: DecodedJSONObject) async throws -> MemberMCPClient.ToolResult {
        try await withToken { access in
            try await self.client.callTool(name: name, arguments: arguments, accessToken: access)
        }
    }

    /// Closes the remote session with an already-minted token and never mints a new one.
    public func disconnect() async {
        let access = await tokens.currentAccessToken
        await tokens.clear()
        guard let access else { return }
        try? await client.close(accessToken: access)
    }

    private func withToken<T: Sendable>(_ call: @Sendable (String) async throws -> T) async throws -> T {
        let access = try await tokens.accessToken()
        do {
            return try await call(access)
        } catch MemberMCPClient.Failure.unauthorized {
            let fresh = try await tokens.refresh(rejectedAccessToken: access)
            do {
                return try await call(fresh)
            } catch MemberMCPClient.Failure.unauthorized {
                throw MemberMCPTokenManager.Failure.notAuthorized
            }
        }
    }
}
