import Foundation
import VouchaModels

struct MCPRPCReply<Result: Decodable & Sendable>: Decodable {
    struct RPCError: Decodable {
        let code: Int
        let message: String
    }

    let jsonrpc: String
    let id: String?
    let method: String?
    let result: Result?
    let error: RPCError?
}

struct MCPInitializationResult: Decodable {
    let protocolVersion: String
}

enum MCPResponseReader {
    /// The producer permits a 1 MiB result; leave room for its JSON-RPC wrapper.
    private static let maximumBodyBytes = 1_100_000

    static func read<Result: Decodable & Sendable>(
        _ lines: AsyncThrowingStream<String, Error>, contentType: String?, id: String,
        sendServerReply: @escaping @Sendable (Data) async throws -> Void
    ) async throws -> Result {
        let type = contentType?.split(separator: ";", maxSplits: 1).first
        guard let mediaType = type.map(String.init)?.trimmingCharacters(in: .whitespaces) else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        switch mediaType.lowercased() {
        case "application/json":
            return try await readJSON(lines, id: id)
        case "text/event-stream":
            return try await readSSE(lines, id: id, sendServerReply: sendServerReply)
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
        _ lines: AsyncThrowingStream<String, Error>, id: String,
        sendServerReply: @escaping @Sendable (Data) async throws -> Void
    ) async throws -> Result {
        var data = ""
        for try await rawLine in lines {
            let line = rawLine.hasSuffix("\r") ? String(rawLine.dropLast()) : rawLine
            if line.isEmpty {
                if !data.isEmpty {
                    if let result: Result = try await eventResult(Data(data.utf8), id: id, send: sendServerReply) {
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
        if !data.isEmpty {
            if let result: Result = try await eventResult(Data(data.utf8), id: id, send: sendServerReply) {
                return result
            }
        }
        throw MemberMCPClient.Failure.invalidResponse
    }

    private static func eventResult<Result: Decodable & Sendable>(
        _ data: Data, id: String, send: @escaping @Sendable (Data) async throws -> Void
    ) async throws -> Result? {
        if try await replyToServerRequest(data, send: send) { return nil }
        return try decode(data, id: id, allowUnmatched: true)
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
