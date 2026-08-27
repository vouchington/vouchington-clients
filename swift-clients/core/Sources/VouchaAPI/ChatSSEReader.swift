import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaCore
import VouchaModels

public enum ChatSSEReaderError: Error, Equatable {
    case incompleteStream
}

public enum ChatSSEReader {
    public static func validateContentType(_ response: URLResponse) throws {
        guard let http = response as? HTTPURLResponse else {
            throw VouchaError.unexpected("Unexpected response format")
        }
        let contentType = (http.value(forHTTPHeaderField: "Content-Type") ?? "").lowercased()
        guard contentType.contains("text/event-stream") else {
            throw VouchaError.unexpected("Unexpected response format")
        }
    }

    public static func events<S: AsyncSequence & Sendable>(from lines: S) -> AsyncThrowingStream<ChatStreamEvent, Error>
        where S.Element == String {
        AsyncThrowingStream { continuation in
            let task = Task {
                do {
                    var parser = ChatSSEParser()
                    for try await line in lines {
                        if Task.isCancelled {
                            throw CancellationError()
                        }
                        for frame in parser.processLine(line) {
                            guard let event = ChatStreamEvent(eventType: frame.eventType, rawData: frame.rawData)
                            else { continue }
                            continuation.yield(event)
                            if event.isTerminal {
                                continuation.finish()
                                return
                            }
                        }
                    }
                    for frame in parser.flush() {
                        if let event = ChatStreamEvent(eventType: frame.eventType, rawData: frame.rawData) {
                            continuation.yield(event)
                            if event.isTerminal {
                                continuation.finish()
                                return
                            }
                        }
                    }
                    continuation.finish(throwing: ChatSSEReaderError.incompleteStream)
                } catch is CancellationError {
                    continuation.finish()
                } catch {
                    continuation.finish(throwing: error)
                }
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }
}

private extension ChatStreamEvent {
    var isTerminal: Bool {
        switch self {
        case .done, .error: true
        default: false
        }
    }
}
