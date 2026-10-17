import Foundation
import VouchaModels

extension MCPResponseReader {
    static func replyToServerRequest(
        _ data: Data, send: @escaping @Sendable (Data) async throws -> Void
    ) async throws -> Bool {
        guard let message = try? JSONDecoder().decode(DecodedJSONValue.self, from: data),
              case let .object(fields) = message,
              let methodValue = fields["method"] else { return false }
        try MCPJSONMemberGuard.validate(data)
        guard case let .string(method) = methodValue,
              case .string("2.0")? = fields["jsonrpc"] else {
            throw MemberMCPClient.Failure.invalidResponse
        }
        guard let id = fields["id"] else { return true }
        switch id {
        case .string, .integer, .unsignedInteger, .decimal, .number: break
        default: throw MemberMCPClient.Failure.invalidResponse
        }
        var response: [String: DecodedJSONValue] = [
            "jsonrpc": .string("2.0"), "id": id
        ]
        if method == "ping" {
            response["result"] = .object([:])
        } else {
            response["error"] = .object([
                "code": .number(-32_601), "message": .string("Method not found")
            ])
        }
        try await send(JSONEncoder().encode(DecodedJSONValue.object(response)))
        return true
    }
}
