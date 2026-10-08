import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaCore

public extension APIClient {
    func sendAllowingNotModified<T: Decodable>(_ endpoint: Endpoint) async throws -> T? {
        if endpoint.path != "/api/v1/session" {
            try await ensureSessionBootstrap()
        }
        let rawRequest = try buildRequest(endpoint)
        let request = await applySigningIfNeeded(rawRequest, method: endpoint.method.rawValue)
        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await session.data(for: request)
        } catch let error as URLError {
            throw VouchaError.network(error)
        }
        if let http = response as? HTTPURLResponse, http.statusCode == 304 {
            return nil
        }
        try validate(response: response, data: data)
        let decodeData = data.isEmpty ? Data("{}".utf8) : data
        do {
            return try decoder.decode(T.self, from: decodeData)
        } catch let error as DecodingError {
            throw VouchaError.decodingFailed(error)
        }
    }
}
