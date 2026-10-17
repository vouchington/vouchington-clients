import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
#if SKIP
    import android.content.pm.PackageManager
#endif

@Observable @MainActor
final class AndroidAccount {
    let config: AppConfig
    let cookieStorage: AndroidSessionCookieStorage
    let apiClient: APIClient
    let session: SessionManager
    let signIn: SignInService
    var isRestoring = true
    var email = ""
    var otp = ""
    var totp = ""
    var turnstileToken: String?
    var loginAttemptID: String?
    var otpRequested = false
    var isAuthenticating = false
    var authError: String?

    init(
        config: AppConfig? = nil,
        cookieStorage: AndroidSessionCookieStorage = AndroidSessionCookieStorage(),
        protocolClasses: [AnyClass]? = nil
    ) {
        let config = config ?? Self.applicationConfig
        self.config = config
        self.cookieStorage = cookieStorage
        let client = APIClient(
            config: config,
            cookieStorage: cookieStorage.cookieStorage,
            protocolClasses: protocolClasses,
            metadata: ClientMetadata(platform: .android, appVersion: Self.appVersion()),
            sessionConfiguration: cookieStorage.sessionConfiguration,
            persistCookies: { cookieStorage.persistCookies() }
        )
        apiClient = client
        session = SessionManager(
            client: client,
            cookieStorage: cookieStorage.cookieStorage,
            clearPersistedCookies: { cookieStorage.clearPersistedCookies() }
        )
        signIn = SignInService(client: client, sessionManager: session)
    }

    func restore() async {
        await session.restoreSession()
        isRestoring = false
    }

    func signOut() async {
        await session.signOut()
    }

    func requestOTP() async {
        guard !isAuthenticating, turnstileToken != nil else { return }
        isAuthenticating = true
        authError = nil
        defer { isAuthenticating = false }
        do {
            let token = turnstileToken
            defer { turnstileToken = nil }
            try await signIn.requestEmailOTP(email: email, turnstileToken: token)
            otpRequested = true
        } catch {
            turnstileToken = nil
            authError = error.localizedDescription
        }
    }

    func turnstileTokenDidChange(_ token: String?, locale: Locale = .current) {
        turnstileToken = token
        authError = token == nil ? UiMessages.string(.nativeAuthVerificationTokenRequired, locale: locale) : nil
    }

    func verifyOTP() async {
        guard !isAuthenticating else { return }
        isAuthenticating = true
        authError = nil
        defer { isAuthenticating = false }
        do {
            if let challenge = try await signIn.verifyEmailOTP(email: email, code: otp) {
                loginAttemptID = challenge.loginAttemptId
            }
        } catch {
            authError = error.localizedDescription
        }
    }

    func verifyMFA() async {
        guard let loginAttemptID, !isAuthenticating else { return }
        isAuthenticating = true
        authError = nil
        defer { isAuthenticating = false }
        do {
            try await signIn.verifyMFATOTP(loginAttemptId: loginAttemptID, code: totp)
            self.loginAttemptID = nil
        } catch {
            authError = error.localizedDescription
        }
    }

    private static func appVersion() -> String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String
        return [version, build].compactMap { $0 }.joined(separator: "+")
    }

    private static var applicationConfig: AppConfig {
        #if os(Android)
            AppConfig.from(environment: [
                "VOUCHA_API_BASE_URL": configuredAndroidApiBaseURL(),
                "VOUCHA_TURNSTILE_SITE_KEY": configuredAndroidTurnstileSiteKey()
            ])
        #else
            .shared
        #endif
    }

    #if SKIP
        static func configuredAndroidApiBaseURL() -> String {
            androidManifestValue("ai.voucha.VOUCHA_API_BASE_URL") ?? "https://voucha.ai"
        }

        static func configuredAndroidTurnstileSiteKey() -> String {
            androidManifestValue("ai.voucha.VOUCHA_TURNSTILE_SITE_KEY") ?? ""
        }

        private static func androidManifestValue(_ name: String) -> String? {
            let context = ProcessInfo.processInfo.androidContext
            let info = context.getPackageManager().getApplicationInfo(
                context.getPackageName(),
                PackageManager.GET_META_DATA
            )
            return info.metaData?.getString(name)
        }
    #endif
}
