import Foundation
import VouchaCore
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public extension APIClient {
    /// Sends an endpoint through the normal bootstrap, signing, and HTTP-error pipeline without decoding its body.
    func data(for endpoint: Endpoint) async throws -> Data {
        if endpoint.path != "/api/v1/session" {
            try await ensureSessionBootstrap()
        }
        let rawRequest = try buildRequest(endpoint)
        let request = await applySigningIfNeeded(rawRequest, method: endpoint.method.rawValue)
        do {
            let (data, response) = try await session.data(for: request)
            try validate(response: response, data: data)
            return data
        } catch let error as URLError {
            throw VouchaError.network(error)
        }
    }
}
