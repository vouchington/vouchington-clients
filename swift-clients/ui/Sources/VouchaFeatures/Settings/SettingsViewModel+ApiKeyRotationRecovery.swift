import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension SettingsViewModel {
    func refreshApiKeysAfterRotation(
        _ response: SettingsApiKeyResponse,
        originalId: String,
        context: ApiKeyRotationContext,
        client: APIClient
    ) async {
        do {
            let page: SettingsListResponse<ApiKey> = try await client.send(.myApiKeys())
            guard context.loadGeneration == settingsLoadGeneration, context.ownerId == identity?.id else { return }
            replaceApiKeyPage(page)
        } catch {
            if isUnauthorized(error) {
                invalidateApiKeyRotationOwner(ifGenerationMatches: context.invalidationGeneration)
                return
            }
            guard !Task.isCancelled,
                  context.loadGeneration == settingsLoadGeneration,
                  context.ownerId == identity?.id else { return }
            apiKeyPagination.invalidateRequestsPreservingPage()
            apiKeyPagination.replaceItems(
                [response.apiKey] + apiKeyPagination.items.filter { $0.id != originalId && $0.id != response.apiKey.id }
            )
        }
    }

    func recoverApiKeysAfterRotationFailure(_ error: Error, context: ApiKeyRotationContext) async {
        if isUnauthorized(error) {
            invalidateApiKeyRotationOwner(ifGenerationMatches: context.invalidationGeneration)
            return
        }
        guard context.loadGeneration == settingsLoadGeneration, context.ownerId == identity?.id,
              let client else { return }
        statusMessage = apiKeyRotationFailureMessage(error)
        do {
            let page: SettingsListResponse<ApiKey> = try await client.send(.myApiKeys())
            guard context.loadGeneration == settingsLoadGeneration, context.ownerId == identity?.id else { return }
            replaceApiKeyPage(page)
        } catch {
            if isUnauthorized(error) {
                invalidateApiKeyRotationOwner(ifGenerationMatches: context.invalidationGeneration)
            }
        }
    }

    private func isUnauthorized(_ error: Error) -> Bool {
        guard let apiError = error as? VouchaError else { return false }
        if case .unauthorized = apiError { return true }
        return false
    }

    private func apiKeyRotationFailureMessage(_ error: Error) -> UiVerbatimText {
        guard let apiError = error as? VouchaError else { return .verbatim(error.localizedDescription) }
        switch apiError {
        case .notFound, .api(statusCode: 404, _), .apiMessage(statusCode: 404, _, _):
            return .message(.nativeApiKeysRotationNotFound)
        case .api(statusCode: 409, _), .apiMessage(statusCode: 409, _, _):
            return .message(.nativeApiKeysRotationConflict)
        default: return .verbatim(apiError.localizedDescription)
        }
    }

}
