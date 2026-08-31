import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

struct ResponseLines {
    let lines: AsyncThrowingStream<String, Error>
    let response: URLResponse
    let body: Data
}

private final class ResponseLinesTaskHolder: @unchecked Sendable {
    var task: Task<Void, Never>?
}

extension APIClient {
    func responseLines(for request: URLRequest) async throws -> ResponseLines {
        #if canImport(FoundationNetworking)
            return try await responseLinesUsingURLSessionDataDelegate(for: request)
        #else
            let (bytes, response) = try await session.bytes(for: request)
            let holder = ResponseLinesTaskHolder()
            return ResponseLines(lines: AsyncThrowingStream { continuation in
                holder.task = Task { [holder] in
                    defer { holder.task = nil }
                    do {
                        for try await line in BoundedResponseLineReader.lines(from: bytes) {
                            continuation.yield(line)
                        }
                        continuation.finish()
                    } catch {
                        continuation.finish(throwing: error)
                    }
                }
                continuation.onTermination = { [weak holder] _ in holder?.task?.cancel() }
            }, response: response, body: Data())
        #endif
    }
}
