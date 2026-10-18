import SwiftUI
import VouchaCore
import VouchaLocalization

struct AndroidSignInView: View {
    @Bindable var account: AndroidAccount
    @Environment(\.locale) var locale

    var body: some View {
        NavigationStack {
            Form {
                Section(UiMessages.string(.nativeAuthSignInTitle, locale: locale)) {
                    if let loginAttemptID = account.loginAttemptID {
                        Text(UiMessages.string(.nativeAuthTwoFactorTitle, locale: locale))
                        SecureField(
                            UiMessages.string(.nativeAuthAuthenticatorTitle, locale: locale),
                            text: $account.totp
                        )
                        #if os(iOS) || os(Android) || SKIP
                        .keyboardType(.numberPad)
                        #endif
                        Button(UiMessages.string(.nativeAuthVerify, locale: locale)) {
                            Task { await account.verifyMFA() }
                        }.disabled(account.isAuthenticating || account.totp.isEmpty || loginAttemptID.isEmpty)
                        Button(UiMessages.string(.nativeAuthDifferentEmail, locale: locale)) {
                            account.restartSignIn()
                        }.disabled(account.isAuthenticating)
                    } else if account.otpRequested {
                        Text(UiMessages.string(.nativeAuthCheckEmail, locale: locale))
                        TextField(UiMessages.string(.nativeAuthCodePlaceholder, locale: locale), text: $account.otp)
                        #if os(iOS) || os(Android) || SKIP
                            .keyboardType(.numberPad)
                        #endif
                        Button(UiMessages.string(.nativeAuthVerifyCode, locale: locale)) {
                            Task { await account.verifyOTP() }
                        }.disabled(account.isAuthenticating || account.otp.isEmpty)
                        Button(UiMessages.string(.nativeAuthDifferentEmail, locale: locale)) {
                            account.restartSignIn()
                        }.disabled(account.isAuthenticating)
                    } else {
                        TextField(UiMessages.string(.nativeAuthEmailAddress, locale: locale), text: $account.email)
                        #if os(iOS) || os(Android) || SKIP
                            .textInputAutocapitalization(.never)
                            .keyboardType(.emailAddress)
                        #endif
                            .disabled(account.isAuthenticating)
                        if account.config.turnstileSiteKey.isEmpty {
                            Text(UiMessages.string(.nativeAuthVerificationTokenRequired, locale: locale))
                                .foregroundStyle(.red)
                        } else {
                            AndroidTurnstileChallenge(
                                siteKey: account.config.turnstileSiteKey,
                                baseURL: account.config.baseURL
                            ) { [challengeID = account.turnstileChallengeID] token in
                                account.turnstileTokenDidChange(token, challengeID: challengeID, locale: locale)
                            }
                            .id(account.turnstileChallengeID)
                            .frame(minHeight: 72)
                        }
                        if account.turnstileToken == nil, account.authError != nil {
                            Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                                account.retryTurnstileChallenge()
                            }.disabled(account.isAuthenticating)
                        }
                        Button(UiMessages.string(.nativeAuthSendCode, locale: locale)) {
                            Task { await account.requestOTP(locale: locale) }
                        }.disabled(account.isAuthenticating || account.email.isEmpty || account.turnstileToken == nil)
                    }
                    if let authError = account.authError {
                        Text(verbatim: UiMessages.string(authError, locale: locale)).foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(UiMessages.string(.nativeAuthSignIn, locale: locale))
        }
    }
}

struct AndroidTurnstileChallenge: View {
    let siteKey: String
    let baseURL: URL
    let onToken: (String?) -> Void

    // The Skip bridge needs the callback's escaping lifetime in the explicit initializer.
    // swiftformat:disable:next redundantMemberwiseInit
    init(siteKey: String, baseURL: URL, onToken: @escaping (String?) -> Void) {
        self.siteKey = siteKey
        self.baseURL = baseURL
        self.onToken = onToken
    }

    var body: some View {
        #if os(Android)
            if let origin = androidTurnstileOrigin(baseURL) {
                TurnstileWebChallenge(siteKey: siteKey, origin: origin) { token in
                    onToken(token)
                }
            }
        #else
            Text("Turnstile challenge is available in the Android app.")
        #endif
    }
}

#if SKIP
    import android.webkit.CookieManager
    import android.webkit.WebResourceError
    import android.webkit.WebResourceRequest
    import android.webkit.WebView
    import android.webkit.WebViewClient
    import androidx.compose.ui.viewinterop.AndroidView

    struct TurnstileWebChallenge: View {
        let siteKey: String
        let origin: String
        let onToken: (String?) -> Void

        // The Skip bridge needs the callback's escaping lifetime in the explicit initializer.
        // swiftformat:disable:next redundantMemberwiseInit
        init(siteKey: String, origin: String, onToken: @escaping (String?) -> Void) {
            self.siteKey = siteKey
            self.origin = origin
            self.onToken = onToken
        }

        var body: some View {
            ComposeView { context in
                AndroidView(factory: { androidContext in
                    let view = WebView(androidContext)
                    view.settings.javaScriptEnabled = true
                    view.settings.domStorageEnabled = true
                    CookieManager.getInstance().setAcceptThirdPartyCookies(view, true)
                    view.webViewClient = TurnstileNavigationClient(origin: origin, onToken: onToken)
                    view.loadDataWithBaseURL(origin, androidTurnstileHTML(siteKey), "text/html", "UTF-8", nil)
                    return view
                }, modifier: context.modifier, update: { _ in })
            }
        }
    }

    private class TurnstileNavigationClient: WebViewClient {
        let origin: String
        let onToken: (String?) -> Void

        init(origin: String, onToken: (String?) -> Void) {
            self.origin = origin
            self.onToken = onToken
        }

        // Skip needs named override parameters; wildcards emit unsupported Kotlin locals.
        // swiftformat:disable:next unusedArguments
        override func shouldOverrideUrlLoading(view: WebView?, url: String?) -> Bool {
            guard let url else { return true }
            if url.hasPrefix("voucha://turnstile?token=") {
                let raw = String(url.dropFirst("voucha://turnstile?token=".count))
                if let token = raw.removingPercentEncoding, !token.isEmpty {
                    onToken(token)
                } else {
                    onToken(nil)
                }
                return true
            }
            if url == "voucha://turnstile?expired" || url == "voucha://turnstile?error" {
                onToken(nil)
                return true
            }
            if url == origin || url.hasPrefix(origin + "/") ||
                url.hasPrefix("https://challenges.cloudflare.com/") {
                return false
            }
            return true
        }

        // Skip needs named override parameters; wildcards emit unsupported Kotlin locals.
        // swiftformat:disable:next unusedArguments
        override func onReceivedError(view: WebView?, request: WebResourceRequest?, error: WebResourceError?) {
            if request?.isForMainFrame == true { onToken(nil) }
        }
    }

#endif
