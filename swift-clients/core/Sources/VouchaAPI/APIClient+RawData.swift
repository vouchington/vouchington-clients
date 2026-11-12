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
        let (data, response) = try await executeBuffered(endpoint)
        try validate(response: response, data: data)
        return data
    }

    /// Downloads an export into the client's owned temporary directory without buffering its body.
    func download(for endpoint: Endpoint, filename: String) async throws -> URL {
        try await download(endpoint, filename: filename, mayRecover: true)
    }

    private func download(
        _ endpoint: Endpoint,
        filename: String,
        mayRecover: Bool,
        expectedEpoch: UInt64? = nil
    ) async throws -> URL {
        if endpoint.path != "/api/v1/session" {
            try await ensureSessionBootstrap()
        }
        let rawRequest = try buildRequest(endpoint)
        let (prepared, stamp) = try cookieCoordinator.prepare(
            rawRequest, origin: baseURL, establishingSession: endpoint.establishesSession
        )
        if let expectedEpoch, expectedEpoch != stamp.epoch { throw CancellationError() }
        let request = await applySigningIfNeeded(prepared, method: endpoint.method.rawValue)
        try cookieCoordinator.check(stamp)
        do {
            let (temporaryURL, response) = try await session.download(for: request)
            defer { try? FileManager.default.removeItem(at: temporaryURL) }
            let canRecover = mayRecover && canReplaySessionRequest(rawRequest, path: endpoint.path)
            if canRecover, let http = response as? HTTPURLResponse,
               http.statusCode == 401,
               try await recoverSessionIfNeeded(
                   response: response,
                   data: responseErrorData(at: temporaryURL),
                   stamp: stamp,
                   path: endpoint.path
               ) {
                return try await download(endpoint, filename: filename, mayRecover: false, expectedEpoch: stamp.epoch)
            }
            let errorData = try (response as? HTTPURLResponse)
                .map { (200 ... 299).contains($0.statusCode) ? Data() : try responseErrorData(at: temporaryURL) } ??
                Data()
            if try clearDefinitiveSessionFailure(
                response: response,
                data: errorData,
                stamp: stamp,
                afterRecovery: !mayRecover
            ) {
                try validate(response: response, data: errorData)
            }
            try acceptCookies(response, stamp: stamp, establishesSession: endpoint.establishesSession)
            try validate(response: response, data: errorData)
            return try ownDownloadedFile(temporaryURL, filename: filename)
        } catch let error as URLError {
            throw VouchaError.network(error)
        }
    }

    private func ownDownloadedFile(_ temporaryURL: URL, filename: String) throws -> URL {
        try Task.checkCancellation()
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(
            "voucha-import-export",
            isDirectory: true
        )
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
    }

    private func responseErrorData(at url: URL) throws -> Data {
        let file = try FileHandle(forReadingFrom: url)
        defer { try? file.close() }
        return try file.read(upToCount: 64 * 1_024) ?? Data()
    }
}
