import Foundation
import SkipFuse

/// Uses SkipKeychain's existing encrypted preference entry, with checked disk acknowledgements.
struct AndroidCheckedCookieSecretStore: AndroidEndpointSecretStoring {
    func string(forKey key: String) throws -> String? {
        #if os(Android)
            try D.voucha.android.CookieSnapshotBridge.INSTANCE().stringValue(key)
        #else
            try SkipKeychainEndpointSecretStore().string(forKey: key)
        #endif
    }

    func set(_ value: String, forKey key: String) throws {
        #if os(Android)
            let _: Void = try D.voucha.android.CookieSnapshotBridge.INSTANCE().setValue(key, value)
        #else
            try SkipKeychainEndpointSecretStore().set(value, forKey: key)
        #endif
    }

    func removeValue(forKey key: String) throws {
        #if os(Android)
            let _: Void = try D.voucha.android.CookieSnapshotBridge.INSTANCE().removeValue(key)
        #else
            try SkipKeychainEndpointSecretStore().removeValue(forKey: key)
        #endif
    }
}
