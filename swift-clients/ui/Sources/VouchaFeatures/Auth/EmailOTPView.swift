import SwiftUI
import VouchaAuth
import VouchaLocalization

public struct EmailOTPView: View {
    @Environment(\.locale)
    private var locale
    @Bindable
    public var viewModel: EmailOTPViewModel
    @Environment(\.dismiss)
    private var dismiss
    @State
    var showingTurnstile = false
    private let onSuccess: () -> Void
    let signInService: SignInService
    let turnstileSiteKey: String?

    public init(
        viewModel: EmailOTPViewModel,
        signInService: SignInService,
        turnstileSiteKey: String? = nil,
        onSuccess: @escaping () -> Void
    ) {
        self.viewModel = viewModel
        self.signInService = signInService
        self.turnstileSiteKey = turnstileSiteKey
        self.onSuccess = onSuccess
    }

    public var body: some View {
        NavigationStack {
            Group {
                switch viewModel.step {
                case .enterEmail:
                    emailStep
                case .enterCode:
                    codeStep
                case let .mfaChallenge(loginAttemptId):
                    mfaChallengeStep(loginAttemptId: loginAttemptId)
                }
            }
            .navigationTitle(UiMessages.string(.nativeAuthSignIn, locale: locale))
            #if os(iOS)
                .navigationBarTitleDisplayMode(.inline)
            #endif
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button(UiMessages.string(.commonCancel, locale: locale)) { dismiss() }
                    }
                }
        }
        .onChange(of: viewModel.didSucceed) { _, succeeded in
            if succeeded {
                onSuccess()
            }
        }
        .emailOTPTurnstileSheet(siteKey: turnstileSiteKey, isPresented: showingTurnstileBinding, viewModel: viewModel)
    }

    private var emailStep: some View {
        EmailOTPEmailStep(viewModel: viewModel, showingTurnstile: showingTurnstileBinding)
    }

    private var codeStep: some View {
        EmailOTPCodeStep(viewModel: viewModel)
    }

    private var showingTurnstileBinding: Binding<Bool> {
        Binding(
            get: { showingTurnstile },
            set: { showingTurnstile = $0 }
        )
    }
}
