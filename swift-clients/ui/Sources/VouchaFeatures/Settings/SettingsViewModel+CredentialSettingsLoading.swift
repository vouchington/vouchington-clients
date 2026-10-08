import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

private enum CredentialSettingsRequestFailure: Error {
    case cancelled
    case error(VouchaError)
}

private enum CredentialSettingsRequestResult {
    case catalog(Result<ScopeCatalogResponse, CredentialSettingsRequestFailure>)
    case grants(Result<Page<OAuthGrant>, CredentialSettingsRequestFailure>)
}

private func credentialSettingsRequestFailure(_ error: Error) -> CredentialSettingsRequestFailure {
    guard !(error is CancellationError), !Task.isCancelled else { return .cancelled }
    return .error(error as? VouchaError ?? .unexpected(error.localizedDescription))
}

private func fetchCredentialScopeCatalog(client: APIClient) async -> CredentialSettingsRequestResult {
    do {
        let catalog: ScopeCatalogResponse = try await client.send(.scopeCatalog)
        return .catalog(.success(catalog))
    } catch {
        return .catalog(.failure(credentialSettingsRequestFailure(error)))
    }
}

private func fetchOAuthGrantPage(client: APIClient) async -> CredentialSettingsRequestResult {
    do {
        let grants: Page<OAuthGrant> = try await client.send(.myOAuthGrants())
        return .grants(.success(grants))
    } catch {
        return .grants(.failure(credentialSettingsRequestFailure(error)))
    }
}

public extension SettingsViewModel {
    func loadCredentialSettings() async {
        guard let client else { return }
        credentialLoadGeneration += 1
        let catalogGeneration = credentialLoadGeneration
        oauthGrantLoadGeneration += 1
        let grantGeneration = oauthGrantLoadGeneration
        oauthGrantPagination.invalidateRequestsPreservingPage()
        credentialState = .loading
        oauthGrantState = .loading

        await withTaskGroup(of: CredentialSettingsRequestResult.self) { group in
            group.addTask { await fetchCredentialScopeCatalog(client: client) }
            group.addTask { await fetchOAuthGrantPage(client: client) }

            for await result in group {
                applyCredentialSettingsResult(
                    result,
                    catalogGeneration: catalogGeneration,
                    grantGeneration: grantGeneration
                )
            }
        }
    }

    private func applyCredentialSettingsResult(
        _ result: CredentialSettingsRequestResult,
        catalogGeneration: Int,
        grantGeneration: Int
    ) {
        switch result {
        case let .catalog(outcome):
            applyCredentialCatalogOutcome(outcome, generation: catalogGeneration)
        case let .grants(outcome):
            applyOAuthGrantsOutcome(outcome, generation: grantGeneration)
        }
    }

    private func applyCredentialCatalogOutcome(
        _ outcome: Result<ScopeCatalogResponse, CredentialSettingsRequestFailure>,
        generation: Int
    ) {
        guard generation == credentialLoadGeneration else { return }
        switch outcome {
        case let .success(catalog):
            guard !Task.isCancelled else {
                credentialState = .idle
                return
            }
            do {
                try applyCredentialScopeCatalog(catalog)
                credentialState = .loaded
            } catch {
                credentialState = .error(error as? VouchaError ?? .unexpected(error.localizedDescription))
            }
        case let .failure(.error(error)):
            credentialState = .error(error)
        case .failure(.cancelled):
            credentialState = .idle
        }
    }

    private func applyOAuthGrantsOutcome(
        _ outcome: Result<Page<OAuthGrant>, CredentialSettingsRequestFailure>,
        generation: Int
    ) {
        guard generation == oauthGrantLoadGeneration else { return }
        switch outcome {
        case let .success(grants):
            guard !Task.isCancelled else {
                oauthGrantState = .idle
                return
            }
            replaceOAuthGrantPage(grants)
            oauthGrantState = .loaded
        case let .failure(.error(error)):
            oauthGrantState = .error(error)
        case .failure(.cancelled):
            oauthGrantState = .idle
        }
    }
}
