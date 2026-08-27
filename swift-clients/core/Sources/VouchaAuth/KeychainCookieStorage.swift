import Foundation
#if canImport(Security)
    import Security

    /// An HTTPCookieStorage that persists cookies to the system Keychain
    /// so they survive app relaunches.
    public final class KeychainCookieStorage: HTTPCookieStorage, @unchecked Sendable {
        private let savePersistedCookies: ([HTTPCookie]) -> Void
        private let deletePersistedCookies: () -> Void
        private let queue = DispatchQueue(label: "ai.voucha.KeychainCookieStorage")
        private var _cookies: [HTTPCookie] = []

        init(
            loadPersistedCookies: @escaping () -> [HTTPCookie],
            savePersistedCookies: @escaping ([HTTPCookie]) -> Void,
            deletePersistedCookies: @escaping () -> Void
        ) {
            self.savePersistedCookies = savePersistedCookies
            self.deletePersistedCookies = deletePersistedCookies
            super.init()
            _cookies = loadPersistedCookies()
        }

        override public var cookies: [HTTPCookie]? {
            queue.sync {
                if pruneExpiredCookies() {
                    savePersistedCookies(_cookies)
                }
                return _cookies
            }
        }

        override public func setCookie(_ cookie: HTTPCookie) {
            queue.sync {
                let cookie = cookieByStampingCreatedDateIfNeeded(cookie, now: Date())
                pruneExpiredCookies()
                _cookies.removeAll {
                    $0.name == cookie.name && $0.domain == cookie.domain && $0.path == cookie.path
                }
                if !isExpired(cookie) {
                    _cookies.append(cookie)
                }
                savePersistedCookies(_cookies)
            }
        }

        override public func setCookies(
            _ cookies: [HTTPCookie],
            for _: URL?,
            mainDocumentURL _: URL?
        ) {
            queue.sync {
                let now = Date()
                pruneExpiredCookies()
                for cookie in cookies {
                    let cookie = cookieByStampingCreatedDateIfNeeded(cookie, now: now)
                    _cookies.removeAll {
                        $0.name == cookie.name && $0.domain == cookie.domain
                            && $0.path == cookie.path
                    }
                    if !isExpired(cookie) {
                        _cookies.append(cookie)
                    }
                }
                savePersistedCookies(_cookies)
            }
        }

        override public func cookies(for URL: URL) -> [HTTPCookie]? {
            queue.sync {
                if pruneExpiredCookies() {
                    savePersistedCookies(_cookies)
                }
                return _cookies.filter { isValid(cookie: $0, for: URL) }
            }
        }

        override public func deleteCookie(_ cookie: HTTPCookie) {
            queue.sync {
                pruneExpiredCookies()
                _cookies.removeAll {
                    $0.name == cookie.name && $0.domain == cookie.domain && $0.path == cookie.path
                }
                savePersistedCookies(_cookies)
            }
        }

        public func clearAll() {
            queue.sync {
                _cookies = []
                deletePersistedCookies()
            }
        }

        @discardableResult
        private func pruneExpiredCookies() -> Bool {
            let cleaned = _cookies.filter { !isExpired($0) }
            guard cleaned.count != _cookies.count else { return false }
            _cookies = cleaned
            return true
        }

        private func isExpired(_ cookie: HTTPCookie) -> Bool {
            KeychainCookieExpiration.isExpired(
                expiresDate: cookie.expiresDate,
                properties: cookie.properties,
                now: Date()
            )
        }

        private func cookieByStampingCreatedDateIfNeeded(_ cookie: HTTPCookie, now: Date) -> HTTPCookie {
            guard let properties = KeychainCookieExpiration.propertiesByStampingCreatedDateIfNeeded(
                cookie.properties,
                now: now
            ) else { return cookie }
            return HTTPCookie(properties: properties) ?? cookie
        }

        private func isValid(cookie: HTTPCookie, for url: URL) -> Bool {
            guard let host = url.host else { return false }
            // Secure flag: secure cookies must only be sent over HTTPS
            if cookie.isSecure && url.scheme?.lowercased() != "https" {
                return false
            }
            // Domain matching (RFC 6265)
            let domain = cookie.domain
            if domain.hasPrefix(".") {
                let bare = String(domain.dropFirst())
                guard host == bare || host.hasSuffix("." + bare) else { return false }
            } else {
                guard host == domain else { return false }
            }
            // Path matching: URL path must start with cookie path
            let cookiePath = cookie.path.isEmpty ? "/" : cookie.path
            if cookiePath != "/" {
                let urlPath = url.path.isEmpty ? "/" : url.path
                guard urlPath.hasPrefix(cookiePath) else { return false }
                if urlPath.count > cookiePath.count {
                    let idx = urlPath.index(urlPath.startIndex, offsetBy: cookiePath.count)
                    guard urlPath[idx] == "/" else { return false }
                }
            }
            return true
        }
    }
#endif
