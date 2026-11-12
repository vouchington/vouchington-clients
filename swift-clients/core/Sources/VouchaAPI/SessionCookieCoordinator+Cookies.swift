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
            guard rotated || establishesSession || (stamp.epoch == epoch && stamp.revision == revision) else { return }
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
            if !Self.sameSnapshot(previous, snapshot) {
                try persistSnapshot(
                    snapshot,
                    action: persist,
                    clear: clearPersistence ?? persist,
                    preservingAuthentication: rotated
                )
            }
            if rotated { revision &+= 1
                confirmedIdentityRevision = authenticated ? revision : nil
            }
            if accountBoundary {
                epoch &+= 1
                authenticationOrder &+= 1
                confirmedIdentityRevision = nil
                rotation = nil
            }
        }
    }

    func clear(
        ifCurrent stamp: Stamp? = nil, preservingAuthentication: Bool = false, persist: (() throws -> Void)?
    ) throws {
        try lock.withLock {
            if let stamp {
                try check(stamp)
                guard stamp.revision == revision else { throw CancellationError() }
            }
            epoch &+= 1
            if !preservingAuthentication { authenticationOrder &+= 1 }
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

    private static func sameSnapshot(_ first: [HTTPCookie], _ second: [HTTPCookie]) -> Bool {
        guard first.count == second.count else { return false }
        return first.allSatisfy { cookie in
            guard let other = second.first(where: {
                $0.name == cookie.name && $0.domain == cookie.domain && $0.path == cookie.path
            }) else { return false }
            var firstProperties = cookie.properties ?? [:]
            var secondProperties = other.properties ?? [:]
            // OriginURL and creation metadata are not Set-Cookie attributes. Creation
            // participates only in Max-Age expiry, whose original timestamp must survive.
            firstProperties.removeValue(forKey: .originURL)
            secondProperties.removeValue(forKey: .originURL)
            if firstProperties[.maximumAge] == nil, secondProperties[.maximumAge] == nil {
                firstProperties.removeValue(forKey: HTTPCookiePropertyKey("Created"))
                secondProperties.removeValue(forKey: HTTPCookiePropertyKey("Created"))
            }
            // String keys avoid platform-specific bridging of HTTPCookiePropertyKey.
            let firstValues = Dictionary(uniqueKeysWithValues: firstProperties.map { ($0.key.rawValue, $0.value) })
            let secondValues = Dictionary(uniqueKeysWithValues: secondProperties.map { ($0.key.rawValue, $0.value) })
            return NSDictionary(dictionary: firstValues).isEqual(NSDictionary(dictionary: secondValues))
        }
    }

    private func persistSnapshot(
        _ snapshot: [HTTPCookie],
        action: (() throws -> Void)?,
        clear: (() throws -> Void)?,
        preservingAuthentication: Bool
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
            if !preservingAuthentication { authenticationOrder &+= 1 }
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
