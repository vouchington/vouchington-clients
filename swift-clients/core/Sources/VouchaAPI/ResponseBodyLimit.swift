import Foundation

enum ResponseBodyLimit {
    static let maximumDiagnosticBytes = 64 * 1_024

    static func collect<S: AsyncSequence>(_ bytes: S) async throws -> Data where S.Element == UInt8 {
        var data = Data()
        for try await byte in bytes {
            guard data.count < maximumDiagnosticBytes else {
                throw ResponseLineBufferError.bodyTooLarge
            }
            data.append(byte)
        }
        return data
    }
}
