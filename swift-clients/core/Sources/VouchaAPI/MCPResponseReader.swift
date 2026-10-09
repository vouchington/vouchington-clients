import Foundation
import VouchaModels

enum MCPResponseReader {
    private static let maximumBodyBytes = 1_024 * 1_024

    static func read<Result: Decodable & Sendable>(
        _ lines: AsyncThrowingStream<String, Error>, contentType: String?, id: String
    ) async throws -> Result {
        let type = contentType?.split(separator: ";", maxSplits: 1).first
        guard let mediaType = type.map(String.init)?.trimmingCharacters(in: .whitespaces) else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        switch mediaType {
        case "application/json":
            return try await readJSON(lines, id: id)
        case "text/event-stream":
            return try await readSSE(lines, id: id)
        default:
            throw MemberMCPClient.Failure.invalidResponse
        }
    }

    private static func readJSON<Result: Decodable & Sendable>(
        _ lines: AsyncThrowingStream<String, Error>, id: String
    ) async throws -> Result {
        var body = ""
        for try await line in lines {
            guard body.utf8.count + line.utf8.count + 1 <= maximumBodyBytes else {
                throw MemberMCPClient.Failure.invalidResponse
            }
            body += line + "\n"
        }
        guard let result: Result = try decode(Data(body.utf8), id: id, allowUnmatched: false) else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        return result
    }

    private static func readSSE<Result: Decodable & Sendable>(
        _ lines: AsyncThrowingStream<String, Error>, id: String
    ) async throws -> Result {
        var data = ""
        for try await rawLine in lines {
            let line = rawLine.hasSuffix("\r") ? String(rawLine.dropLast()) : rawLine
            if line.isEmpty {
                if !data.isEmpty {
                    if let result: Result = try decode(Data(data.utf8), id: id, allowUnmatched: true) {
                        return result
                    }
                    data = ""
                }
            } else if line.hasPrefix("data:") {
                let value = line.dropFirst(5).drop(while: { $0 == " " })
                guard data.utf8.count + value.utf8.count + 1 <= maximumBodyBytes else {
                    throw MemberMCPClient.Failure.invalidResponse
                }
                if !data.isEmpty { data += "\n" }
                data += value
            }
        }
        if !data.isEmpty, let result: Result = try decode(Data(data.utf8), id: id, allowUnmatched: true) {
            return result
        }
        throw MemberMCPClient.Failure.invalidResponse
    }

    private static func decode<Result: Decodable & Sendable>(
        _ data: Data, id: String, allowUnmatched: Bool
    ) throws -> Result? {
        guard let reply = try? JSONDecoder().decode(MCPRPCReply<DecodedJSONValue>.self, from: data),
              reply.jsonrpc == "2.0" else { throw MemberMCPClient.Failure.invalidResponse }
        if allowUnmatched, reply.method != nil || reply.id != id { return nil }
        guard reply.method == nil, reply.id == id else { throw MemberMCPClient.Failure.invalidResponse }
        if let error = reply.error { throw MemberMCPClient.Failure.rpc(code: error.code, message: error.message) }
        guard let payload = reply.result,
              let encoded = try? JSONEncoder().encode(payload),
              let result = try? JSONDecoder().decode(Result.self, from: encoded)
        else { throw MemberMCPClient.Failure.invalidResponse }
        return result
    }
}
