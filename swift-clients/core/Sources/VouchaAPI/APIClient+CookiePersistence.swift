import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

extension APIClient {
    static func cookieSessionConfiguration(
        _ provided: URLSessionConfiguration?, cookieStorage: HTTPCookieStorage
    ) -> URLSessionConfiguration {
        if let provided {
            guard let copy = provided.copy() as? URLSessionConfiguration else {
                preconditionFailure("API session configuration must support copying")
            }
            // URLSession copies configuration values, while its in-memory cookie jar stays shared.
            copy.httpCookieStorage = provided.httpCookieStorage
            return copy
        }
        let configuration = URLSessionConfiguration.default
        configuration.httpCookieStorage = cookieStorage
        configuration.httpShouldSetCookies = true
        configuration.httpCookieAcceptPolicy = .always
        return configuration
    }
}
