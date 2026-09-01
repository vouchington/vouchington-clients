import Foundation

public extension APIClient {
    /// Streams line-delimited JSON objects of type `T`.
    func stream<T: Decodable>(_ endpoint: Endpoint) -> AsyncThrowingStream<T, Error> {
        AsyncThrowingStream { continuation in
            let task = Task {
                do {
                    try await self.ensureSessionBootstrap()
                    let rawRequest = try self.buildRequest(endpoint)
                    let request = await self.applySigningIfNeeded(rawRequest, method: endpoint.method.rawValue)
                    let lineResponse = try await self.responseLines(for: request)
                    try self.validate(response: lineResponse.response, data: lineResponse.body)
                    for try await line in lineResponse.lines {
                        guard !line.isEmpty else { continue }
                        guard let data = line.data(using: .utf8) else { continue }
                        do {
                            let item = try self.decoder.decode(T.self, from: data)
                            continuation.yield(item)
                        } catch {
                            // skip malformed lines without killing the stream
                        }
                    }
                    continuation.finish()
                } catch {
                    continuation.finish(throwing: error)
                }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }
}
