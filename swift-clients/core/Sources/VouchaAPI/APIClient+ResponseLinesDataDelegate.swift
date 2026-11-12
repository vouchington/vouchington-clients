import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension APIClient {
    func responseLinesUsingURLSessionDataDelegate(for request: URLRequest) async throws -> ResponseLines {
        let (request, stamp) = try cookieCoordinator.prepare(request, origin: baseURL)
        let coordinator = cookieCoordinator
        let persist = checkedPersistence
        let transport = ResponseLinesTransport(
            followRedirects: false,
            persistCookies: { try? coordinator.persist(stamp: stamp, action: persist) }
        )
        let result = try await transport.start(request: request, configuration: session.configuration)
        try acceptCookies(result.response, stamp: stamp)
        return result
    }
}
