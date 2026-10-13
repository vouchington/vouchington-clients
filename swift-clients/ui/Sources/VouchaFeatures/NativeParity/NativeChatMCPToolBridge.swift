import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

/// One immutable, account-scoped snapshot of the server-advertised tool catalog.
struct NativeChatMCPToolBridge {
    enum Failure: Error { case invalidCatalog, unknownTool, invalidArguments, resultTooLarge }

    let tools: [LocalLLMFunctionTool]
    private let schemas: [String: DecodedJSONValue]

    init(catalog: DecodedJSONValue) throws {
        let catalogData = try JSONEncoder().encode(catalog)
        guard catalogData.count <= 512_000 else { throw Failure.invalidCatalog }
        guard case let .object(body) = catalog,
              case let .array(entries)? = body["tools"], entries.count <= 128 else {
            throw Failure.invalidCatalog
        }
        var projected: [LocalLLMFunctionTool] = []
        var schemas: [String: DecodedJSONValue] = [:]
        for entry in entries {
            guard case let .object(fields) = entry,
                  case let .string(name)? = fields["name"], Self.validModelFunctionName(name),
                  case let .object(schema)? = fields["inputSchema"],
                  schemas[name] == nil else { throw Failure.invalidCatalog }
            let description: String = if case let .string(value)? = fields["description"] { value } else { "" }
            guard description.utf8.count <= 8_192 else { throw Failure.invalidCatalog }
            let value = DecodedJSONValue.object(schema)
            try NativeChatMCPInputSchema.validateSchema(value)
            let parameters = try JSONEncoder().encode(value)
            guard parameters.count <= 32_000 else { throw Failure.invalidCatalog }
            projected.append(.init(
                name: name, description: description,
                parameters: parameters
            ))
            schemas[name] = value
        }
        tools = projected
        self.schemas = schemas
    }

    private static func validModelFunctionName(_ name: String) -> Bool {
        !name.isEmpty && name.utf8.count <= 64 && name.utf8.allSatisfy { byte in
            (65 ... 90).contains(byte) || (97 ... 122).contains(byte) ||
                (48 ... 57).contains(byte) || byte == 95 || byte == 45
        }
    }

    func execute(
        name: String, rawArguments: String,
        session: NativeChatMCPAgentSession,
        accountId: String, revision: Int
    ) async throws -> String {
        let object = try validatedArguments(name: name, rawArguments: rawArguments)
        let result = try await session.callTool(
            name: name, arguments: object, accountId: accountId, revision: revision
        )
        let envelope: DecodedJSONValue = .object([
            "structuredContent": result.structuredContent ?? .null,
            "content": .array(result.content),
            "isError": .bool(result.isError == true)
        ])
        let output = try JSONEncoder().encode(envelope)
        guard output.count <= 256_000, let text = String(data: output, encoding: .utf8) else {
            throw Failure.resultTooLarge
        }
        return text
    }

    func validate(name: String, rawArguments: String) throws {
        _ = try validatedArguments(name: name, rawArguments: rawArguments)
    }

    private func validatedArguments(name: String, rawArguments: String) throws -> [String: DecodedJSONValue] {
        guard let schema = schemas[name] else { throw Failure.unknownTool }
        guard rawArguments.utf8.count <= 64_000,
              let data = rawArguments.data(using: .utf8),
              let arguments = try? JSONDecoder().decode(DecodedJSONValue.self, from: data),
              case let .object(object) = arguments,
              (try? NativeChatMCPInputSchema.accepts(arguments, schema: schema)) == true
        else { throw Failure.invalidArguments }
        return object
    }
}
