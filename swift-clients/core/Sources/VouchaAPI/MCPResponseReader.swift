import Foundation
import VouchaModels

protocol MCPResponseValue: Sendable {
    static func decodeMCPResponse(_ value: DecodedJSONValue) throws -> Self
}

extension DecodedJSONValue: MCPResponseValue {
    static func decodeMCPResponse(_ value: DecodedJSONValue) throws -> DecodedJSONValue {
        value
    }
}

struct MCPInitializationResult: MCPResponseValue {
    let protocolVersion: String

    static func decodeMCPResponse(_ value: DecodedJSONValue) throws -> Self {
        guard case let .object(fields) = value, case let .string(version)? = fields["protocolVersion"] else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        return Self(protocolVersion: version)
    }
}

enum MCPResponseReader {
    /// The producer permits a 1 MiB result; leave room for its JSON-RPC wrapper.
    private static let maximumBodyBytes = 1_100_000

    static func read<Result: MCPResponseValue>(
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

    private static func readJSON<Result: MCPResponseValue>(
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

    private static func readSSE<Result: MCPResponseValue>(
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

    private static func eventResult<Result: MCPResponseValue>(
        _ data: Data, id: String, send: @escaping @Sendable (Data) async throws -> Void
    ) async throws -> Result? {
        if try await replyToServerRequest(data, send: send) { return nil }
        return try decode(data, id: id, allowUnmatched: true)
    }

    private static func decode<Result: MCPResponseValue>(
        _ data: Data, id: String, allowUnmatched: Bool
    ) throws -> Result? {
        guard case let .object(reply) = try exactJSON(data),
              case .string("2.0")? = reply["jsonrpc"] else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        let method = try optionalStringField("method", in: reply)
        let replyID = try optionalStringField("id", in: reply)
        guard matchesRequest(method: method, replyID: replyID, requestID: id, allowUnmatched: allowUnmatched) else {
            return nil
        }
        try throwIfRPCError(in: reply)
        guard let payload = reply["result"] else { throw MemberMCPClient.Failure.invalidResponse }
        if case .null = payload { throw MemberMCPClient.Failure.invalidResponse }
        return try Result.decodeMCPResponse(payload)
    }

    private static func optionalStringField(_ key: String, in object: DecodedJSONObject) throws -> String? {
        guard let value = object[key] else { return nil }
        return switch value {
        case let .string(string): string
        case .null: nil
        default: throw MemberMCPClient.Failure.invalidResponse
        }
    }

    private static func matchesRequest(
        method: String?, replyID: String?, requestID: String, allowUnmatched: Bool
    ) -> Bool {
        if allowUnmatched, method != nil || replyID != requestID { return false }
        return method == nil && replyID == requestID
    }

    private static func throwIfRPCError(in reply: DecodedJSONObject) throws {
        guard let error = reply["error"] else { return }
        if case .null = error { return }
        try throwRPCError(error)
    }

    private static func throwRPCError(_ error: DecodedJSONValue) throws {
        guard let encoded = try? error.exactJSONData(),
              let rpcError = try? JSONDecoder().decode(RPCError.self, from: encoded) else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        throw MemberMCPClient.Failure.rpc(code: rpcError.code, message: rpcError.message)
    }

    private struct RPCError: Decodable { let code: Int
        let message: String
    }

    static func exactJSON(_ data: Data) throws -> DecodedJSONValue {
        guard data.count <= maximumBodyBytes else { throw MemberMCPClient.Failure.invalidResponse }
        do {
            let value = try DecodedJSONValue(exactJSONData: data)
            try MCPJSONLosslessNumberGuard.validate(data)
            return value
        } catch { throw MemberMCPClient.Failure.invalidResponse }
    }
}
