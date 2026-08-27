import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension APIClient {
    func responseLinesUsingURLSessionDataDelegate(for request: URLRequest) async throws -> ResponseLines {
        let transport = ResponseLinesTransport()
        return try await transport.start(request: request, configuration: session.configuration)
    }
}
