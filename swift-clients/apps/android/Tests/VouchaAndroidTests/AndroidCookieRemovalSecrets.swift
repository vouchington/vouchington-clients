import Foundation
@testable import VouchaAndroid

/// Models SharedPreferences.commit(): memory changes first, while a false result leaves disk unchanged.
final class VolatileCookieDeletionSecrets: AndroidEndpointSecretStoring {
    final class Disk {
        let lock = NSLock()
        var values: [String: String] = [:]
    }

    private let disk: Disk
    private var volatile: [String: String]
    var failDeletionCommit = false

    init() {
        let disk = Disk()
        self.disk = disk
        volatile = [:]
    }

    private init(disk: Disk) {
        self.disk = disk
        volatile = disk.lock.withLock { disk.values }
    }

    func reconstructProcess() -> VolatileCookieDeletionSecrets {
        VolatileCookieDeletionSecrets(disk: disk)
    }

    func string(forKey key: String) throws -> String? {
        volatile[key]
    }

    func set(_ value: String, forKey key: String) throws {
        volatile[key] = value
        disk.lock.withLock { disk.values[key] = value }
    }

    func removeValue(forKey key: String) throws {
        volatile.removeValue(forKey: key)
        if failDeletionCommit { throw URLError(.cannotWriteToFile) }
        _ = disk.lock.withLock { disk.values.removeValue(forKey: key) }
    }
}

final class CookieRemovalFailureSecrets: AndroidEndpointSecretStoring {
    private let lock = NSLock()
    private var values: [String: String] = [:]
    private var failRemoval = false

    func snapshotValue(forKey key: String) -> String? {
        lock.lock()
        defer { lock.unlock() }
        return values[key]
    }

    func setRemovalFailure(_ fails: Bool) {
        lock.lock()
        defer { lock.unlock() }
        failRemoval = fails
    }

    func string(forKey key: String) throws -> String? {
        snapshotValue(forKey: key)
    }

    func set(_ value: String, forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        if failRemoval { throw URLError(.cannotWriteToFile) }
        values.removeValue(forKey: key)
    }
}
