import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension AndroidSessionCookieStorage {
    var cookies: [HTTPCookie]? {
        cookieStorage.cookies
    }

    func cookies(for url: URL) -> [HTTPCookie]? {
        cookieStorage.cookies(for: url)
    }

    func setCookie(_ cookie: HTTPCookie) {
        cookieStorage.setCookie(cookie)
        persistCookies()
    }

    func deleteCookie(_ cookie: HTTPCookie) {
        cookieStorage.deleteCookie(cookie)
        persistCookies()
    }
}
