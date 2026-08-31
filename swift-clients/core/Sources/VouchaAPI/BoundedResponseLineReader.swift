import Foundation
import VouchaModels

enum BoundedResponseLineReader {
    static let maximumLineBytes = ChatSSEParser.maximumFrameCharacters

    static func lines<S: AsyncSequence>(from bytes: S) -> AsyncThrowingStream<String, Error>
        where S.Element == UInt8 {
        AsyncThrowingStream { continuation in
            let task = Task {
                do {
                    var line: [UInt8] = []
                    for try await byte in bytes {
                        if byte == 0x0A {
                            if line.last == 0x0D {
                                line.removeLast()
                            }
                            guard let decodedLine = String(bytes: line, encoding: .utf8) else {
                                throw URLError(.cannotDecodeContentData)
                            }
                            continuation.yield(decodedLine)
                            line.removeAll(keepingCapacity: true)
                            continue
                        }
                        guard line.count < maximumLineBytes else {
                            throw ChatSSEParserError.frameTooLarge
                        }
                        line.append(byte)
                    }
                    if !line.isEmpty {
                        if line.last == 0x0D {
                            line.removeLast()
                        }
                        guard let decodedLine = String(bytes: line, encoding: .utf8) else {
                            throw URLError(.cannotDecodeContentData)
                        }
                        continuation.yield(decodedLine)
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
