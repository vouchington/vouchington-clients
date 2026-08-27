import Foundation
import Observation
import VouchaAuth
import VouchaLocalization

@Observable
@MainActor
public final class MFAChallengeViewModel {
    enum Origin {
        case brokerOAuth
        case other
    }

    public var code: String = ""
    public var isLoading: Bool = false
    public var errorMessage: UiVerbatimText?
    public private(set) var didSucceed: Bool = false

    private let loginAttemptId: String
    let origin: Origin
    private let signInService: SignInService

    /// Creates a view model for the given MFA login attempt.
    public init(loginAttemptId: String, signInService: SignInService) {
        self.loginAttemptId = loginAttemptId
        self.signInService = signInService
        origin = .other
    }

    init(loginAttemptId: String, signInService: SignInService, origin: Origin) {
        self.loginAttemptId = loginAttemptId
        self.signInService = signInService
        self.origin = origin
    }

    /// Submits the TOTP code for the current login attempt.
    public func submitCode() async {
        guard !code.isEmpty, !isLoading else { return }
        isLoading = true
        errorMessage = nil
        do {
            try await signInService.verifyMFATOTP(loginAttemptId: loginAttemptId, code: code)
            didSucceed = true
        } catch {
            errorMessage = .verbatim(error.localizedDescription)
        }
        isLoading = false
    }
}
