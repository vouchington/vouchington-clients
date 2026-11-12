import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct FreshAccountConfirmationView: View {
    @Environment(\.locale)
    private var locale
    @Bindable
    var model: FreshAccountConfirmationViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Text(UiMessages.string(.extractedMyMfaReauthDialogReAuthenticate3887afa4, locale: locale))
                .font(.headline)
            Text(UiMessages.string(
                .extractedMyMfaReauthDialogVerifyYourIdentityToRemoveYourE7d1fbdb, locale: locale
            ))
            if let methods = model.methods {
                if methods.hasEmail {
                    Button(UiMessages.string(
                        .extractedMfaReauthDialogMethodSelectorEmailVerification4d225196, locale: locale
                    )) { model.usingEmail = true
                        model.code = ""
                    }
                }
                if methods.hasTotp {
                    Button(UiMessages.string(
                        .extractedMfaReauthDialogMethodSelectorAuthenticatorApp09a12ace, locale: locale
                    )) { model.usingEmail = false
                        model.code = ""
                    }
                }
                codeControls
                FreshAccountConfirmationOtherMethods(model: model)
            } else if model.errorMessage == nil {
                ProgressView()
            }
            if let error = model.errorMessage {
                Text(UiMessages.string(error, locale: locale)).foregroundStyle(.red)
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: locale)) { model.cancel() }
        }
        .padding(Spacing.lg)
        .interactiveDismissDisabled(model.isWorking)
        .onDisappear { model.cancel() }
    }

    @ViewBuilder
    private var codeControls: some View {
        if model.usingEmail, model.methods?.hasEmail == true {
            Text(UiMessages.string(
                .extractedMfaReauthDialogVerificationPanesWeLlSendAVerificationCodeFc0ed3b9, locale: locale
            ))
            Button(UiMessages.string(
                model.emailSent ? .extractedMfaReauthDialogVerificationPanesResend1f948437
                    : .extractedMfaReauthDialogVerificationPanesSendCode66a5b409,
                locale: locale
            )) { Task { await model.sendEmail() } }.disabled(model.isWorking)
        } else if !model.usingEmail, model.methods?.hasTotp == true {
            Text(UiMessages.string(
                .extractedMfaReauthDialogVerificationPanesEnterThe6DigitCodeFromA160dee6, locale: locale
            ))
        }
        if model.usingEmail && model.emailSent || !model.usingEmail && model.methods?.hasTotp == true {
            TextField(UiMessages.string(
                .extractedEmailManagerVerifyEmailFormVerificationCode3ee75029, locale: locale
            ), text: $model.code)
                .textFieldStyle(.roundedBorder)
            #if os(iOS)
                .keyboardType(model.usingEmail ? .asciiCapable : .numberPad)
                .textInputAutocapitalization(.characters)
                .autocorrectionDisabled()
            #endif
            Button(UiMessages.string(
                model.isWorking ? .extractedMfaReauthDialogVerificationPanesVerifying2ec1ac7d
                    : .extractedMfaReauthDialogVerificationPanesVerifyEea2745e,
                locale: locale
            )) { Task { await model.verifyCode() } }.disabled(!model.canVerifyCode)
        }
    }
}
