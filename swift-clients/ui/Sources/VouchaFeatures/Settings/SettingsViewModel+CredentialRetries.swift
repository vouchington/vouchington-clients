import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension SettingsViewModel {
    func retryCredentialScopeCatalog() async {
        guard let client else { return }
        credentialLoadGeneration += 1
        let generation = credentialLoadGeneration
        credentialState = .loading
        do {
            let catalog: ScopeCatalogResponse = try await client.send(.scopeCatalog)
            guard generation == credentialLoadGeneration else { return }
            if Task.isCancelled {
                credentialState = .idle
                return
            }
            try applyCredentialScopeCatalog(catalog)
            credentialState = .loaded
        } catch {
            guard generation == credentialLoadGeneration else { return }
            if error is CancellationError || Task.isCancelled {
                credentialState = .idle
            } else {
                credentialState = .error(error as? VouchaError ?? .unexpected(error.localizedDescription))
            }
        }
    }

    func retryOAuthGrants() async {
        guard let client else { return }
        oauthGrantLoadGeneration += 1
        let generation = oauthGrantLoadGeneration
        oauthGrantPagination.invalidateRequestsPreservingPage()
        oauthGrantState = .loading
        do {
            let grants: Page<OAuthGrant> = try await client.send(.myOAuthGrants())
            guard generation == oauthGrantLoadGeneration else { return }
            if Task.isCancelled {
                oauthGrantState = .idle
                return
            }
            replaceOAuthGrantPage(grants)
            oauthGrantState = .loaded
        } catch {
            guard generation == oauthGrantLoadGeneration else { return }
            if error is CancellationError || Task.isCancelled {
                oauthGrantState = .idle
            } else {
                oauthGrantState = .error(error as? VouchaError ?? .unexpected(error.localizedDescription))
            }
        }
    }
}
