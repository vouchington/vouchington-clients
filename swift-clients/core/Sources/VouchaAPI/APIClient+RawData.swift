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

    /// Downloads an export into the client's owned temporary directory without buffering its body.
    func download(for endpoint: Endpoint, filename: String) async throws -> URL {
        if endpoint.path != "/api/v1/session" {
            try await ensureSessionBootstrap()
        }
        let rawRequest = try buildRequest(endpoint)
        let request = await applySigningIfNeeded(rawRequest, method: endpoint.method.rawValue)
        do {
            let (temporaryURL, response) = try await session.download(for: request)
            defer { try? FileManager.default.removeItem(at: temporaryURL) }
            if let http = response as? HTTPURLResponse, !(200 ... 299).contains(http.statusCode) {
                try validate(response: response, data: responseErrorData(at: temporaryURL))
            }
            try validate(response: response, data: Data())
            try Task.checkCancellation()
            let directory = FileManager.default.temporaryDirectory
                .appendingPathComponent("voucha-import-export", isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let destination = directory.appendingPathComponent("\(UUID().uuidString)-\(filename)")
            do {
                try FileManager.default.moveItem(at: temporaryURL, to: destination)
                try Task.checkCancellation()
                return destination
            } catch {
                try? FileManager.default.removeItem(at: destination)
                throw error
            }
        } catch let error as URLError {
            throw VouchaError.network(error)
        }
    }

    private func responseErrorData(at url: URL) throws -> Data {
        let file = try FileHandle(forReadingFrom: url)
        defer { try? file.close() }
        return try file.read(upToCount: 64 * 1_024) ?? Data()
    }
}
