import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public extension SettingsViewModel {
    func beginBlueskyLink(
        store: NativeBlueskyLinkStore? = nil,
        openAuthorizationURL: @escaping @MainActor (URL) -> Void
    ) async {
        guard let client else { return }
        let handle = blueskyHandle.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !handle.isEmpty else {
            blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyHandleRequired))
            return
        }
        blueskyLinkState = .linking
        do {
            let completionProof = try NativeAuthorizationCompletionProof.generate()
            let response: BeginBlueskyAccountLinkResponse = try await client.send(
                .beginNativeBlueskyAccountLink(
                    handle: handle,
                    completionProofChallenge: completionProof.challenge
                )
            )
            guard let flowId = response.flowId,
                  let url = URL(string: response.redirectUrl),
                  url.scheme == "https" else {
                blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
                return
            }
            let startedAt = now()
            guard (store ?? blueskyLinkStore).save(
                flowId: flowId,
                completionProofVerifier: completionProof.verifier,
                now: startedAt
            ) else {
                blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
                return
            }
            blueskyLinkExpiresAt = startedAt.addingTimeInterval(NativeBlueskyLinkStore.lifetime)
            blueskyLinkState = .awaitingCallback
            openAuthorizationURL(url)
        } catch {
            blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
        }
    }

    var canBeginBlueskyLink: Bool {
        let hasFinalizingLink: Bool
        do {
            hasFinalizingLink = try blueskyLinkStore.pending(now: now())?.isFinalizing == true
        } catch {
            return false
        }
        return !hasFinalizingLink &&
            blueskyLinkState != .linking &&
            blueskyLinkState != .awaitingCallback &&
            blueskyLinkState != .finalizing
    }

    func cancelBlueskyLink() {
        guard blueskyLinkState == .awaitingCallback else { return }
        do {
            try blueskyLinkStore.clear()
            blueskyLinkExpiresAt = nil
            blueskyLinkState = .cancelled
        } catch {
            blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
        }
    }

    func reconcileBlueskyLinkState(now: Date? = nil) {
        guard blueskyLinkState == .awaitingCallback else { return }
        do {
            switch try blueskyLinkStore.pendingStatus(now: now ?? self.now()) {
            case let .active(pending):
                blueskyLinkExpiresAt = pending.expiresAt
            case .expired:
                blueskyLinkExpiresAt = nil
                blueskyLinkState = .expired
            case .none:
                break
            }
        } catch {
            blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
        }
    }

    var blueskyExpiryTaskID: Date? {
        blueskyLinkState == .awaitingCallback ? blueskyLinkExpiresAt : nil
    }

    func waitForBlueskyExpiry(
        sleeper: @MainActor (TimeInterval) async throws -> Void = { delay in
            try await Task.sleep(for: .seconds(delay))
        }
    ) async {
        while blueskyLinkState == .awaitingCallback {
            let currentTime = now()
            do {
                switch try blueskyLinkStore.pendingStatus(now: currentTime) {
                case let .active(pending):
                    blueskyLinkExpiresAt = pending.expiresAt
                    do {
                        try await sleeper(max(0, pending.expiresAt.timeIntervalSince(currentTime)))
                    } catch {
                        return
                    }
                case .expired:
                    blueskyLinkExpiresAt = nil
                    blueskyLinkState = .expired
                    return
                case .none:
                    return
                }
            } catch {
                blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
                return
            }
        }
    }

    func consumeBlueskyLinkResult() async {
        guard let result = blueskyLinkStore.takeResult() else { return }
        blueskyLinkState = Self.state(for: result)
        blueskyLinkExpiresAt = nil
        guard result == .success, let client else { return }
        do {
            let response: SettingsIdentityResponse = try await client.send(.myIdentity)
            apply(identity: response.identity)
        } catch {
            blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
        }
    }

    static func state(for result: NativeBlueskyLinkResult) -> NativeBlueskyLinkState {
        switch result {
        case .finalizing: .finalizing
        case .success: .success
        case .expired: .expired
        case .providerFailure, .finalizationFailure: .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
        }
    }

    func disconnectBluesky() async {
        guard let client else { return }
        blueskyLinkState = .disconnecting
        do {
            let _: EmptyResponse = try await client.send(.disconnectBlueskyAccount)
            await reload()
            blueskyLinkState = .idle
        } catch {
            blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyDisconnectFailed))
        }
    }
}
