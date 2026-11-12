import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension SessionCookieCoordinator {
    func commit(
        _ cookies: [HTTPCookie], stamp: Stamp, persist: (() throws -> Void)?,
        clearPersistence: (() throws -> Void)? = nil, rotated: Bool = false,
        authenticated: Bool = false, establishingSession: Bool = false
    ) throws {
        try lock.withLock {
            try check(stamp)
            let establishesSession = establishingSession && cookies.contains { ["dt", "st"].contains($0.name) }
            guard rotated || establishesSession || stamp.revision == revision else { return }
            let previous = storage.cookies ?? []
            let accountBoundary = !rotated && cookies.contains { cookie in
                ["dt", "st"].contains(cookie.name) && previous.first {
                    $0.name == cookie.name && $0.domain == cookie.domain && $0.path == cookie.path
                }?.value != cookie.value
            }
            var snapshot = previous
            for cookie in cookies {
                snapshot.removeAll { $0.name == cookie.name && $0.domain == cookie.domain && $0.path == cookie.path }
                if cookie.expiresDate.map({ $0 > Date() }) ?? true { snapshot.append(cookie) }
            }
            try persistSnapshot(snapshot, action: persist, clear: clearPersistence ?? persist)
            if rotated { revision &+= 1
                confirmedIdentityRevision = authenticated ? revision : nil
            }
            if accountBoundary {
                epoch &+= 1
                confirmedIdentityRevision = nil
                rotation = nil
            }
        }
    }

    func persist(stamp: Stamp, action: (() throws -> Void)?) throws {
        try lock.withLock {
            guard epoch == stamp.epoch else { throw CancellationError() }
            try action?()
        }
    }

    func clear(ifCurrent stamp: Stamp? = nil, persist: (() throws -> Void)?) throws {
        try lock.withLock {
            if let stamp {
                try check(stamp)
                guard stamp.revision == revision else { throw CancellationError() }
            }
            epoch &+= 1
            confirmedIdentityRevision = nil
            rotation = nil
            defer { emitInvalidation() }
            do {
                if let transactional = storage as? any TransactionalSessionCookieStorage {
                    try transactional.replaceCookies([])
                } else {
                    for cookie in storage.cookies ?? [] {
                        storage.deleteCookie(cookie)
                    }
                }
                try persist?()
            } catch {
                for cookie in storage.cookies ?? [] {
                    storage.deleteCookie(cookie)
                }
                throw error
            }
        }
    }

    private func persistSnapshot(
        _ snapshot: [HTTPCookie],
        action: (() throws -> Void)?,
        clear: (() throws -> Void)?
    ) throws {
        do {
            if let transactional = storage as? any TransactionalSessionCookieStorage {
                try transactional.replaceCookies(snapshot)
            } else {
                for old in storage.cookies ?? [] {
                    storage.deleteCookie(old)
                }
                for cookie in snapshot {
                    storage.setCookie(cookie)
                }
            }
            try action?()
        } catch {
            epoch &+= 1
            confirmedIdentityRevision = nil
            rotation = nil
            defer { emitInvalidation() }
            for cookie in storage.cookies ?? [] {
                storage.deleteCookie(cookie)
            }
            try? clear?()
            throw error
        }
    }
}
