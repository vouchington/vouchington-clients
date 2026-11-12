import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

/// First-party credentials are never forwarded through HTTP redirects.
class FirstPartySessionDelegate: NSObject, URLSessionTaskDelegate {
    func urlSession(
        _: URLSession, task _: URLSessionTask, willPerformHTTPRedirection _: HTTPURLResponse,
        newRequest _: URLRequest, completionHandler: @escaping (URLRequest?) -> Void
    ) {
        completionHandler(nil)
    }
}
