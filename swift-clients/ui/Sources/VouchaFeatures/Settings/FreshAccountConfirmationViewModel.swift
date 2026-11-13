import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
public final class FreshAccountConfirmationViewModel {
    public internal(set) var isPresented = false
    public internal(set) var isWorking = false
    public internal(set) var methods: ReAuthMethodsResponse?
    public internal(set) var errorMessage: UiMessage?
    public internal(set) var emailSent = false
    var appleChallenge: ReAuthProviderChallengeResponse?
    var brokerFlowID: String?
    var passkeyPagination = CursorPaginationState<AccountConfirmationPasskey>()
    var passkeys: [AccountConfirmationPasskey] {
        passkeyPagination.items
    }

    let googleAssertion: (any FreshProviderAssertion)?
    public var code = ""
    public var usingEmail = true
    let service: FreshAccountConfirmationService
    let broker: NativeOAuthAuthorizationCoordinator?
    private var context: AccountConfirmationContext?
    private var continuation: CheckedContinuation<String, Error>?
    private var isAdmitting = false
    var generation = UUID()

    public init(
        client: APIClient,
        broker: NativeOAuthAuthorizationCoordinator? = nil,
        googleAssertion: (any FreshProviderAssertion)? = nil
    ) {
        self.googleAssertion = googleAssertion
        self.broker = broker
        service = FreshAccountConfirmationService(client: client)
    }

    public func authorize() async throws -> String {
        guard continuation == nil, !isPresented, !isAdmitting else { throw AccountConfirmationError.invalidProof }
        // Reserve the slot before suspending so a concurrent call cannot overwrite our continuation.
        isAdmitting = true
        let admittedContext = await service.client.accountConfirmationContext()
        isAdmitting = false
        guard let context = admittedContext else {
            throw AccountConfirmationError.contextChanged
        }
        if case .confirmationReady = broker?.result { broker?.acknowledgeResult() }
        broker?.cancelFreshAccountConfirmation()
        self.context = context
        generation = UUID()
        let request = generation
        methods = nil
        errorMessage = nil
        emailSent = false
        appleChallenge = nil
        brokerFlowID = nil
        passkeyPagination.reset(items: [])
        code = ""
        isPresented = true
        do {
            let proof = try await withTaskCancellationHandler {
                try await withCheckedThrowingContinuation { continuation in
                    self.continuation = continuation
                    Task { await self.loadMethods(request: request) }
                }
            } onCancel: {
                Task { @MainActor in self.cancel(request: request) }
            }
            try Task.checkCancellation()
            guard generation == request, await service.client.accountConfirmationContext() == context else {
                throw AccountConfirmationError.contextChanged
            }
            return proof
        } catch {
            cancel(request: request)
            throw error
        }
    }

    public func cancel() {
        cancel(request: generation)
    }

    private func cancel(request: UUID) {
        guard generation == request, isPresented else { return }
        generation = UUID()
        broker?.cancelFreshAccountConfirmation()
        isPresented = false
        isWorking = false
        code = ""
        context = nil
        methods = nil
        appleChallenge = nil
        brokerFlowID = nil
        passkeyPagination.reset(items: [])
        let waiting = continuation
        continuation = nil
        waiting?.resume(throwing: CancellationError())
    }

    func hasCurrentContext(_ request: UUID) async -> Bool {
        guard generation == request, isPresented, let context else { return false }
        let current = await service.client.accountConfirmationContext()
        guard generation == request, isPresented else { return false }
        guard current == context else {
            cancel(request: request)
            return false
        }
        return true
    }

    public func sendEmail() async {
        guard methods?.hasEmail == true else { return }
        let owner = generation
        await verifyOperation {
            try await self.service.sendEmail()
            guard await self.hasCurrentContext(owner) else { throw CancellationError() }
            self.emailSent = true
            self.code = ""
            return nil
        }
    }

    public func verifyCode() async {
        guard canVerifyCode else {
            errorMessage = UiMessage(.extractedAuthMfaStepInvalidVerificationCode4ed23ad3)
            return
        }
        let entered = code
        let email = usingEmail
        await verifyOperation(emailCode: email) {
            if email {
                return try await self.service.verifyEmail(entered)
            }
            return try await self.service.verifyTOTP(entered)
        }
    }

    func verifyOperation(emailCode: Bool = false, _ operation: () async throws -> String?) async {
        guard !isWorking, isPresented, context != nil else { return }
        let request = generation
        isWorking = true
        errorMessage = nil
        defer { if generation == request { isWorking = false } }
        do {
            guard await hasCurrentContext(request) else { return }
            let token = try await operation()
            guard await hasCurrentContext(request) else { return }
            if let token {
                guard !token.isEmpty else { throw AccountConfirmationError.invalidProof }
                let waiting = continuation
                continuation = nil
                isPresented = false
                code = ""
                waiting?.resume(returning: token)
            }
        } catch is CancellationError {
            cancel(request: request)
        } catch {
            guard await hasCurrentContext(request) else { return }
            errorMessage = failureMessage(error, emailCode: emailCode)
        }
    }
}

public extension FreshAccountConfirmationViewModel {
    var canVerifyCode: Bool {
        guard !isWorking else { return false }
        if usingEmail {
            return methods?.hasEmail == true && emailSent && EmailVerificationCode.normalized(code) != nil
        }
        return methods?.hasTotp == true && code.utf8.count == 6 && code.utf8.allSatisfy { (48 ... 57).contains($0) }
    }

}
