import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaModels

extension RootView {
    func handleIncomingURL(_ url: URL) {
        let oauthCoordinator = viewModelFactory.nativeOAuthAuthorizationCoordinator
        if oauthCoordinator.handleIncomingURL(url) {
            switch oauthCoordinator.pending?.purpose {
            case .authenticate:
                showingSignIn = true
            case .connect:
                routeNativeTargetPath("/my/identity")
            case nil:
                showingSignIn = true
            }
            return
        }
        let store = NativeBlueskyLinkStore()
        do {
            guard let callback = try store.claimCallback(for: url) else {
                if !store.isCallbackURL(url) {
                    routeNativeURL(url)
                }
                return
            }
            Task {
                switch callback {
                case let .completion(pending):
                    await finalizeNativeBlueskyLink(pending, factory: viewModelFactory, store: store)
                case .failure:
                    store.recordResult(.providerFailure)
                }
                routeNativeTargetPath("/my/identity")
            }
        } catch {
            store.recordResult(.finalizationFailure)
            routeNativeTargetPath("/my/identity")
        }
    }
}

@MainActor
func resumePendingNativeBlueskyLink(
    factory: ViewModelFactory,
    store: NativeBlueskyLinkStore = NativeBlueskyLinkStore(),
    now: Date = Date()
) async {
    do {
        switch try store.pendingStatus(now: now) {
        case let .active(pending) where pending.isFinalizing:
            await finalizeNativeBlueskyLink(pending, factory: factory, store: store)
        case .expired:
            store.recordResult(.expired)
        case .none, .active:
            break
        }
    } catch {
        store.recordResult(.finalizationFailure)
    }
}

@MainActor
private func finalizeNativeBlueskyLink(
    _ pending: PendingNativeBlueskyLink,
    factory: ViewModelFactory,
    store: NativeBlueskyLinkStore
) async {
    let completed = await store.finalize(pending, complete: { flowId, completionToken, completionProofVerifier in
        let _: EmptyResponse = try await factory.apiClient.send(
            .completeNativeBlueskyAccountLink(
                flowId: flowId,
                completionToken: completionToken,
                completionProofVerifier: completionProofVerifier
            )
        )
    }, confirmAttached: {
        await isBlueskyAccountAttached(factory: factory)
    })
    if completed {
        _ = await factory.sessionManager.refresh()
    }
}

@MainActor
private func isBlueskyAccountAttached(factory: ViewModelFactory) async -> Bool {
    struct IdentityEnvelope: Decodable {
        let identity: PrivateUser
    }

    do {
        let response: IdentityEnvelope = try await factory.apiClient.send(.myIdentity)
        guard response.identity.blueskyAccount != nil else { return false }
        factory.sessionManager.synchronize(with: response.identity)
        return true
    } catch {
        return false
    }
}
