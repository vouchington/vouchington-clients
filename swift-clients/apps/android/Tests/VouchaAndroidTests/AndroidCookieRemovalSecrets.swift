import Foundation
@testable import VouchaAndroid

/// Models Android 15 SharedPreferences: a failed synchronous commit leaves disk generation behind.
final class VolatileCookieDeletionSecrets: AndroidEndpointSecretStoring {
    final class Disk {
        let lock = NSLock()
        var values: [String: String] = [:]
    }

    private let disk: Disk
    private var volatile: [String: String]
    private var memoryGeneration = 0
    private var diskGeneration = 0
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
        if volatile[key] != value { memoryGeneration += 1 }
        volatile[key] = value
        disk.lock.withLock { disk.values = volatile }
        diskGeneration = memoryGeneration
    }

    func removeValue(forKey key: String) throws {
        if volatile.removeValue(forKey: key) != nil { memoryGeneration += 1 }
        if failDeletionCommit { throw URLError(.cannotWriteToFile) }
        if diskGeneration < memoryGeneration {
            disk.lock.withLock { disk.values = volatile }
            diskGeneration = memoryGeneration
        }
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
