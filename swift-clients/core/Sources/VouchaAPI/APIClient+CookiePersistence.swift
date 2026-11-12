import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public extension APIClient {
    /// Clears the cookie jar used by this client's transports and persists the cleared state.
    func clearSessionCookies() {
        try? clearSessionCookiesChecked()
    }

    func clearSessionCookiesChecked() throws {
        try cookieCoordinator.clear(persist: checkedClearPersistence)
    }

    internal static func cookieSessionConfiguration(
        _ provided: URLSessionConfiguration?, cookieStorage: HTTPCookieStorage
    ) -> URLSessionConfiguration {
        if let provided {
            guard let copy = provided.copy() as? URLSessionConfiguration else {
                preconditionFailure("API session configuration must support copying")
            }
            // URLSession copies configuration values, while its in-memory cookie jar stays shared.
            copy.httpCookieStorage = provided.httpCookieStorage
            copy.httpShouldSetCookies = false
            return copy
        }
        let configuration = URLSessionConfiguration.default
        configuration.httpCookieStorage = cookieStorage
        configuration.httpShouldSetCookies = false
        configuration.httpCookieAcceptPolicy = .always
        return configuration
    }
}
