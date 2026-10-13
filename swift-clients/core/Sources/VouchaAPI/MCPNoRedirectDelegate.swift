import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

/// OAuth metadata and bearer traffic must not follow a redirect to another origin.
final class MCPNoRedirectDelegate: NSObject, URLSessionTaskDelegate, @unchecked Sendable {
    static let shared = MCPNoRedirectDelegate()
    func urlSession(
        _: URLSession,
        task _: URLSessionTask,
        willPerformHTTPRedirection _: HTTPURLResponse,
        newRequest _: URLRequest,
        completionHandler: @escaping (URLRequest?) -> Void
    ) {
        completionHandler(nil)
    }
}
