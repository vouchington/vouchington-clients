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
            await Self.boundedBy(seconds: closeTimeout) { try? await client.close(accessToken: access) }
        }
    }

    /// Returns when `work` finishes or after `seconds`, whichever comes first. The work is cancelled on
    /// timeout but never joined, so a close stalled behind MCP initialization cannot outlive the bound.
    private static func boundedBy(seconds: TimeInterval, _ work: @escaping @Sendable () async -> Void) async {
        let gate = ResumeOnce()
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            let timer = Task {
                try? await Task.sleep(nanoseconds: UInt64(max(seconds, 0) * 1_000_000_000))
                if gate.claim() { continuation.resume() }
            }
            let worker = Task {
                await work()
                timer.cancel()
                if gate.claim() { continuation.resume() }
            }
            Task {
                _ = await timer.result
                worker.cancel()
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

private final class ResumeOnce: @unchecked Sendable {
    private let lock = NSLock()
    private var claimed = false

    func claim() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        if claimed { return false }
        claimed = true
        return true
    }
}
