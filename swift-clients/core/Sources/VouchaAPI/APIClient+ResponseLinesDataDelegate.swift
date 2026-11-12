import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension APIClient {
    func responseLinesUsingURLSessionDataDelegate(for request: URLRequest) async throws -> ResponseLines {
        let (request, stamp) = try cookieCoordinator.prepare(request, origin: baseURL)
        let transport = ResponseLinesTransport(followRedirects: false)
        let result = try await transport.start(request: request, configuration: session.configuration)
        try acceptCookies(result.response, stamp: stamp)
        return result
    }
}
