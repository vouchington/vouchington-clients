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
                            account.otpRequested = false
                            account.otp = ""
                        }
                    } else {
                        TextField(UiMessages.string(.nativeAuthEmailAddress, locale: locale), text: $account.email)
                        #if os(iOS) || os(Android) || SKIP
                            .textInputAutocapitalization(.never)
                            .keyboardType(.emailAddress)
                        #endif
                        if account.config.turnstileSiteKey.isEmpty {
                            Text(UiMessages.string(.nativeAuthVerificationTokenRequired, locale: locale))
                                .foregroundStyle(.red)
                        } else {
                            AndroidTurnstileChallenge(
                                siteKey: account.config.turnstileSiteKey,
                                baseURL: account.config.baseURL
                            ) {
                                account.turnstileTokenDidChange($0, locale: locale)
                            }
                            .frame(minHeight: 72)
                        }
                        Button(UiMessages.string(.nativeAuthSendCode, locale: locale)) {
                            Task { await account.requestOTP(locale: locale) }
                        }.disabled(account.isAuthenticating || account.email.isEmpty || account.turnstileToken == nil)
                    }
                    if let authError = account.authError {
                        Text(authError).foregroundStyle(.red)
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
                    view.loadDataWithBaseURL(origin, turnstileHTML(siteKey), "text/html", "UTF-8", nil)
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

    private func turnstileHTML(_ key: String) -> String {
        let escaped = key.replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "\"", with: "&quot;")
            .replacingOccurrences(of: "<", with: "&lt;")
        return """
        <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1">
        <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script></head>
        <body><div class="cf-turnstile" data-sitekey="\(escaped)" data-callback="tokenReady"
        data-expired-callback="tokenExpired" data-error-callback="tokenError"></div>
        <script>
        function tokenReady(token){location.href='voucha://turnstile?token='+encodeURIComponent(token);}
        function tokenExpired(){location.href='voucha://turnstile?expired';}
        function tokenError(){location.href='voucha://turnstile?error';}
        </script></body></html>
        """
    }
#endif
