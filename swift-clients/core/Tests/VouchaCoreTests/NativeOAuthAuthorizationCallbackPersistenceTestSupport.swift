import Foundation
@testable import VouchaCore

final class FlakyOAuthSecureState: NativeOAuthSecureStatePersisting, @unchecked Sendable {
    private let lock = NSLock()
    private var data: Data?
    var readsUnavailable = false
    var failNextCallbackRead = false
    var failNextCallbackWrite = false
    var failNextWrite = false
    private(set) var deleteCount = 0

    func read() throws -> Data? {
        if readsUnavailable || failNextCallbackRead {
            failNextCallbackRead = false
            throw NativeOAuthSecureStateError.unavailable
        }
        return lock.withLock { data }
    }

    func write(_ data: Data) -> Bool {
        if failNextWrite {
            failNextWrite = false
            return false
        }
        lock.withLock { self.data = data }
        return true
    }

    func delete() throws {
        lock.withLock {
            data = nil
            deleteCount += 1
        }
    }

    func seed(_ data: Data) {
        lock.withLock { self.data = data }
    }

    func writeForOAuthCallbackClaim(_ data: Data) throws {
        if failNextCallbackWrite {
            failNextCallbackWrite = false
            throw NativeOAuthSecureStateError.unavailable
        }
        _ = write(data)
    }
}
