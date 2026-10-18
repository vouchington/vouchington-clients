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
    let cookiePersistenceNotice = AndroidCookiePersistenceNotice()
    let config: AppConfig
    let cookieStorage: AndroidSessionCookieStorage
    let session: SessionManager
    let signIn: SignInService
    var isRestoring = true
    private var editableEmail = ""
    var email: String {
        get { editableEmail }
        set {
            guard !isAuthenticating else { return }
            editableEmail = newValue
        }
    }

    var turnstileChallengeID = UUID()
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
        session = SessionManager(
            client: client,
            cookieStorage: cookieStorage.cookieStorage,
            beforeClearSessionCookies: { try cookieStorage.clearPersistedCookies() }
        )
        signIn = SignInService(client: client, sessionManager: session)
        let notice = cookiePersistenceNotice
        cookieStorage.observePersistence { [weak notice] status in
            Task { @MainActor in notice?.accept(status) }
        }
    }

    func retryCookiePersistence() {
        cookieStorage.persistCookies()
        cookiePersistenceNotice.accept(cookieStorage.persistenceStatus)
    }

    func restore() async {
        await session.restoreSession()
        isRestoring = false
    }

    @discardableResult
    func signOut() async -> Bool {
        await session.signOut()
        return !session.isSignedIn && session.lastSignOutErrorMessage == nil
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
}

// Top-level Kotlin helpers are bridged into the native Swift Android module.
#if SKIP
    func configuredAndroidApiBaseURL() -> String {
        androidManifestValue("ai.voucha.VOUCHA_API_BASE_URL") ?? "https://voucha.ai"
    }

    func configuredAndroidTurnstileSiteKey() -> String {
        androidManifestValue("ai.voucha.VOUCHA_TURNSTILE_SITE_KEY") ?? ""
    }

    private func androidManifestValue(_ name: String) -> String? {
        let context = ProcessInfo.processInfo.androidContext
        let info = context.getPackageManager().getApplicationInfo(
            context.getPackageName(),
            PackageManager.GET_META_DATA
        )
        return info.metaData?.getString(name)
    }
#endif
