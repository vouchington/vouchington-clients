import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

final class AndroidCookieSnapshotRecovery {
    private let secrets: any AndroidEndpointSecretStoring
    private let key: String
    private let cookieStorage: HTTPCookieStorage
    private(set) var unreadableSnapshotCanBeDiscarded = false

    init(secrets: any AndroidEndpointSecretStoring, key: String, cookieStorage: HTTPCookieStorage) {
        self.secrets = secrets
        self.key = key
        self.cookieStorage = cookieStorage
    }

    func loadPersistedCookies() throws {
        unreadableSnapshotCanBeDiscarded = false
        let json: String?
        do {
            json = try secrets.string(forKey: key)
        } catch {
            unreadableSnapshotCanBeDiscarded = true
            throw error
        }
        guard let json else { return }
        guard let data = json.data(using: .utf8) else {
            unreadableSnapshotCanBeDiscarded = true
            throw CocoaError(.fileReadInapplicableStringEncoding)
        }
        let records: [AndroidSessionCookieStorage.CookieRecord]
        do {
            records = try JSONDecoder().decode([AndroidSessionCookieStorage.CookieRecord].self, from: data)
        } catch {
            unreadableSnapshotCanBeDiscarded = true
            throw error
        }
        unreadableSnapshotCanBeDiscarded = false
        for record in records where record.expires.map({ $0 > Date() }) ?? true {
            if let cookie = record.cookie {
                cookieStorage.setCookie(cookie)
            }
        }
    }

    func discardUnreadableSnapshot() throws {
        guard unreadableSnapshotCanBeDiscarded else { return }
        try secrets.removeValue(forKey: key)
        cookieStorage.cookies?.forEach { cookieStorage.deleteCookie($0) }
        unreadableSnapshotCanBeDiscarded = false
    }
}
