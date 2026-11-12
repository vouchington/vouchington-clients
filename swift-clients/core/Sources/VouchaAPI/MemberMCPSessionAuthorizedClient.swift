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

    /// Drops the in-memory token immediately, then closes the remote session with that already-minted
    /// token (never minting a new one) in a bounded best-effort task. Returns without waiting for the
    /// network; await the returned task only when completion matters, for example in tests.
    @discardableResult
    public func disconnect(closeTimeout: TimeInterval = 5) async -> Task<Void, Never> {
        let access = await tokens.currentAccessToken
        await tokens.clear()
        let client = client
        return Task {
            guard let access else { return }
            await withTaskGroup(of: Void.self) { group in
                group.addTask { try? await client.close(accessToken: access) }
                group.addTask { try? await Task.sleep(nanoseconds: UInt64(max(closeTimeout, 0) * 1_000_000_000)) }
                _ = await group.next()
                group.cancelAll()
            }
        }
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
