import AuthenticationServices
import SwiftUI
import VouchaAuth
import VouchaDesignSystem
import VouchaLocalization

public struct SignInView: View {
    @Environment(\.locale)
    var locale
    @Environment(\.dismiss)
    var dismiss
    @Environment(\.openURL)
    var openURL
    @State
    var showingEmailOTP = false
    @State
    var showingMFAChallenge = false
    @State
    var mfaVM: MFAChallengeViewModel?
    @State
    var appleSignInError: UiVerbatimText?
    @State
    var appleSignInNonce: String?
    @State
    var passkeySignInError: UiVerbatimText?
    @State
    var oauthPresentation = NativeOAuthSignInPresentation()
    @State
    var isPasskeyLoading = false
    @State
    private var emailOTPViewModel: EmailOTPViewModel
    let signInService: SignInService
    let nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator?
    let onNavigateToTargetPath: (String) -> Void
    let onSuccess: () -> Void

    /// Creates the sign-in view backed by the given service.
    public init(
        signInService: SignInService,
        nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator? = nil,
        uiLocale: UiLocale = UiLocaleResolver.resolve(savedUiLocale: nil),
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        initialEmail: String? = nil,
        initialCode: String? = nil,
        initialOAuthMFAAttemptId: String? = nil,
        onSuccess: @escaping () -> Void
    ) {
        self.signInService = signInService
        self.nativeOAuthAuthorizationCoordinator = nativeOAuthAuthorizationCoordinator
        self.onNavigateToTargetPath = onNavigateToTargetPath
        self.onSuccess = onSuccess
        _showingEmailOTP = State(initialValue: initialEmail?.isEmpty == false && initialCode?.isEmpty == false)
        _showingMFAChallenge = State(initialValue: initialOAuthMFAAttemptId != nil)
        _mfaVM = State(initialValue: initialOAuthMFAAttemptId.map {
            MFAChallengeViewModel(
                loginAttemptId: $0,
                signInService: signInService,
                origin: .brokerOAuth
            )
        })
        _emailOTPViewModel = State(
            initialValue: EmailOTPViewModel(
                signInService: signInService,
                uiLocale: uiLocale,
                initialEmail: initialEmail,
                initialCode: initialCode
            )
        )
    }

    public var body: some View {
        NavigationStack {
            VStack(spacing: Spacing.lg) {
                Spacer()
                brandingSection
                    .padding(.bottom, Spacing.xl)
                authButtonsSection
                legalLinksSection
                Spacer()
            }
            .padding(Spacing.lg)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(UiMessages.string(.commonCancel, locale: locale)) { dismiss() }
                }
            }
            .sheet(isPresented: $showingEmailOTP) {
                EmailOTPView(
                    viewModel: emailOTPViewModel,
                    signInService: signInService
                ) {
                    showingEmailOTP = false
                    completeSuccessfulSignIn()
                    dismiss()
                }
            }
            .sheet(isPresented: nativeOAuthMFASheetIsPresented) {
                mfaSheet
            }
            .alert(
                UiMessages.string(.nativeAuthSignInFailed, locale: locale),
                isPresented: Binding(
                    get: { appleSignInError != nil || passkeySignInError != nil || oauthPresentation.error != nil },
                    set: {
                        if !$0 {
                            dismissNativeOAuthError()
                        }
                    }
                )
            ) {
                if nativeOAuthAuthorizationCoordinator?.canRecoverFailedFinalization == true {
                    Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                        Task { await retryNativeOAuthFinalization() }
                    }
                    Button(UiMessages.string(.commonCancel, locale: locale), role: .cancel) {
                        cancelNativeOAuthAuthorization()
                    }
                } else if nativeOAuthAuthorizationCoordinator?.canRetryCapabilityLoading == true {
                    Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                        Task { await retryNativeOAuthCapabilityLoading() }
                    }
                    Button(UiMessages.string(.commonCancel, locale: locale), role: .cancel) {
                        dismissNativeOAuthError()
                    }
                } else {
                    Button(UiMessages.string(.nativeCommonOk, locale: locale), role: .cancel) {
                        dismissNativeOAuthError()
                    }
                }
            } message: {
                if let error = appleSignInError ?? passkeySignInError ?? oauthPresentation.error {
                    Text(verbatim: UiMessages.string(error, locale: locale))
                }
            }
            .task {
                consumeNativeOAuthResult(nativeOAuthAuthorizationCoordinator?.result)
                await nativeOAuthAuthorizationCoordinator?.loadCapabilities()
                synchronizeNativeOAuthError()
            }
            .onChange(of: nativeOAuthAuthorizationCoordinator?.result) { _, result in
                consumeNativeOAuthResult(result)
            }
            .onChange(of: nativeOAuthAuthorizationCoordinator?.errorMessage) { _, _ in
                synchronizeNativeOAuthError()
            }
            .onChange(of: nativeOAuthAuthorizationCoordinator?.capabilityErrorMessage) { _, _ in
                synchronizeNativeOAuthError()
            }
        }
    }

}
