import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaModels

public extension APIClient {
    func streamChatConversation(
        conversationId: String,
        message: String,
        provider: String? = nil
    ) -> AsyncThrowingStream<ChatStreamEvent, Error> {
        streamChatEvents(.chatConversationStream(conversationId: conversationId, message: message, provider: provider))
    }
}

private extension APIClient {
    func streamChatEvents(_ endpoint: Endpoint) -> AsyncThrowingStream<ChatStreamEvent, Error> {
        AsyncThrowingStream { continuation in
            let task = Task {
                do {
                    let rawRequest = try self.buildRequest(endpoint)
                    let request = await self.applySigningIfNeeded(rawRequest, method: endpoint.method.rawValue)
                    #if !canImport(Darwin)
                        let lineResponse = try await self.responseLines(for: request)
                        if let http = lineResponse.response as? HTTPURLResponse,
                           !(200 ... 299).contains(http.statusCode) {
                            try self.validate(response: lineResponse.response, data: lineResponse.body)
                        }
                        try self.validate(response: lineResponse.response, data: lineResponse.body)
                        try ChatSSEReader.validateContentType(lineResponse.response)
                        for try await event in ChatSSEReader
                            .events(from: lineResponse.lines) {
                            continuation.yield(event)
                        }
                    #else
                        let (bytes, response) = try await self.session.bytes(for: request)
                        if let http = response as? HTTPURLResponse, !(200 ... 299).contains(http.statusCode) {
                            let data = try await ResponseBodyLimit.collect(bytes)
                            try self.validate(response: response, data: data)
                        }
                        try self.validate(response: response, data: Data())
                        try ChatSSEReader.validateContentType(response)
                        for try await event in ChatSSEReader.events(from: bytes.lines) {
                            continuation.yield(event)
                        }
                    #endif
                    continuation.finish()
                } catch is CancellationError {
                    continuation.finish()
                } catch { continuation.finish(throwing: error) }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }
}
