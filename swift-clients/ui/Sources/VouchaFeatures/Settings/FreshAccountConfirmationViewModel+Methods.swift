import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels
#if canImport(AuthenticationServices)
    import AuthenticationServices
#endif

extension FreshAccountConfirmationViewModel {
    func failureMessage(_ error: Error, emailCode: Bool) -> UiMessage {
        if emailCode {
            switch error as? VouchaError {
            case .api(400, _), .api(410, _), .apiMessage(400, _, _), .apiMessage(410, _, _):
                return .init(.extractedMyEmailManagerInvalidOrExpiredVerificationCode9b5b98fe)
            default: break
            }
        }
        return .init(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
    }

    func loadMethods(request: UUID) async {
        do {
            guard await hasCurrentContext(request) else { return }
            let methods = try await service.methods()
            guard await hasCurrentContext(request) else { return }
            self.methods = methods
            usingEmail = methods.hasEmail
            if methods.hasPasskey {
                let page: Page<AccountConfirmationPasskey> = try await service.client.send(
                    Endpoint(.GET, path: "/api/v1/auth/passkeys", queryItems: [.init(name: "limit", value: "25")])
                )
                guard await hasCurrentContext(request) else { return }
                passkeyPagination.reset(items: page.results)
                passkeyPagination.restoreContinuation(
                    endCursor: page.pageInfo.endCursor,
                    hasMore: page.pageInfo.hasNextPage
                )
            }
        } catch {
            guard await hasCurrentContext(request) else { return }
            errorMessage = UiMessage(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
        }
    }

    func loadMorePasskeys() async {
        guard isPresented, let request = passkeyPagination.beginNextPage() else { return }
        let owner = generation
        do {
            guard await hasCurrentContext(owner) else { return }
            let page: Page<AccountConfirmationPasskey> = try await service.client.send(Endpoint(
                .GET, path: "/api/v1/auth/passkeys",
                queryItems: [.init(name: "limit", value: "25"), .init(name: "after", value: request.cursor)]
            ))
            guard await hasCurrentContext(owner) else { return }
            passkeyPagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch {
            guard await hasCurrentContext(owner) else { return }
            passkeyPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    func prepareApple() async {
        guard methods?.providers.contains("apple") == true else { return }
        let owner = generation
        await verifyOperation {
            let challenge: ReAuthProviderChallengeResponse = try await self.service.client.send(
                .reAuthProviderChallenge(provider: "apple")
            )
            guard await self.hasCurrentContext(owner) else { throw CancellationError() }
            self.appleChallenge = challenge
            return nil
        }
    }

    func verifyApple(credential: String, flowId: String) async {
        guard methods?.providers.contains("apple") == true, let challenge = appleChallenge,
              challenge.flowId == flowId else { return }
        appleChallenge = nil
        await verifyProvider(provider: "apple", challenge: challenge, credential: credential)
    }

    func verifyGoogle() async {
        guard methods?.providers.contains("google") == true, let googleAssertion else {
            errorMessage = .init(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
            return
        }
        let owner = generation
        await verifyOperation {
            let challenge: ReAuthProviderChallengeResponse = try await self.service.client.send(
                .reAuthProviderChallenge(provider: "google")
            )
            guard await self.hasCurrentContext(owner) else { throw CancellationError() }
            let credential = try await googleAssertion.assertion(nonce: challenge.nonce)
            guard await self.hasCurrentContext(owner) else { throw CancellationError() }
            let result: ReAuthTokenResponse = try await self.service.client.send(.verifyReAuthProvider(
                provider: "google", flowId: challenge.flowId, credential: credential
            ))
            return result.reAuthToken
        }
    }

    private func verifyProvider(
        provider: String, challenge: ReAuthProviderChallengeResponse, credential: String
    ) async {
        await verifyOperation {
            let result: ReAuthTokenResponse = try await self.service.client.send(.verifyReAuthProvider(
                provider: provider, flowId: challenge.flowId, credential: credential
            ))
            return result.reAuthToken
        }
    }

    func beginBroker(provider: NativeOAuthProvider, openURL: @escaping @MainActor (URL) -> Void) async {
        guard methods?.providers.contains(provider.rawValue) == true, provider != .google, let broker else { return }
        let owner = generation
        await verifyOperation {
            if broker.errorMessage != nil, broker.pending?.flowId == self.brokerFlowID {
                broker.cancelFreshAccountConfirmation()
            }
            await broker.begin(provider: provider, purpose: .reauthenticate, openAuthorizationURL: openURL)
            guard await self.hasCurrentContext(owner) else { throw CancellationError() }
            guard let pending = broker.pending, pending.purpose == .reauthenticate else {
                throw AccountConfirmationError.unavailableMethod
            }
            self.brokerFlowID = pending.flowId
            return nil
        }
    }

    func consumeBrokerResult(_ result: NativeOAuthAuthorizationResult?) async {
        if result == nil, brokerFlowID != nil, broker?.errorMessage != nil {
            errorMessage = .init(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
            return
        }
        if case .expired(_, .reauthenticate) = result, brokerFlowID != nil {
            brokerFlowID = nil
            errorMessage = .init(.extractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96)
            broker?.acknowledgeResult()
            return
        }
        guard case let .confirmationReady(provider, flowId) = result,
              brokerFlowID == flowId, methods?.providers.contains(provider.rawValue) == true else { return }
        // Keep the result until the busy operation ends; verifyOperation re-delivers it.
        guard !isWorking else { return }
        brokerFlowID = nil
        defer { broker?.acknowledgeResult() }
        await verifyOperation {
            let response: ReAuthTokenResponse = try await self.service.client.send(
                .proveReAuthProvider(provider: provider.rawValue, flowId: flowId)
            )
            return response.reAuthToken
        }
    }

    #if canImport(AuthenticationServices)
        func verifyPasskey(id: String, anchor: ASPresentationAnchor) async {
            guard methods?.hasPasskey == true, passkeys.contains(where: { $0.id == id }) else { return }
            await verifyOperation { try await self.service.verifyPasskey(id: id, presentationAnchor: anchor) }
        }
    #endif
}
