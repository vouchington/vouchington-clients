import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

/// One immutable, account-scoped snapshot of the server-advertised tool catalog.
struct NativeChatMCPToolBridge {
    enum Failure: Error { case invalidCatalog, unknownTool, invalidArguments, invalidOutput, resultTooLarge }

    let tools: [LocalLLMFunctionTool]
    private let schemas: [String: DecodedJSONValue]
    private let outputs: [String: NativeChatMCPOutputContract]

    init(catalog: DecodedJSONValue) throws {
        let catalogData = try catalog.exactJSONData()
        guard catalogData.count <= 512_000 else { throw Failure.invalidCatalog }
        guard case let .object(body) = catalog,
              case let .array(entries)? = body["tools"], entries.count <= NativeChatMCPToolCatalog.maximumTools else {
            throw Failure.invalidCatalog
        }
        var outputs: [String: NativeChatMCPOutputContract] = [:]
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
            let parameters = try value.exactJSONData()
            guard parameters.count <= 32_000 else { throw Failure.invalidCatalog }
            projected.append(.init(
                name: name, description: description,
                parameters: parameters
            ))
            schemas[name] = value
            let hasOutputMapping: Bool = if case let .object(meta)? = fields["_meta"] {
                meta["voucha/optionOutputPointers"] != nil
            } else { false }
            if NativeChatMCPOutputContract.optionNames(fields["inputSchema"]) != nil,
               fields["outputSchema"] == nil || !hasOutputMapping { throw Failure.invalidCatalog }
            if fields["outputSchema"] != nil || hasOutputMapping {
                outputs[name] = try NativeChatMCPOutputContract(fields: fields)
            }
        }
        tools = projected
        self.schemas = schemas
        self.outputs = outputs
    }

    private static func validModelFunctionName(_ name: String) -> Bool {
        !name.isEmpty && name.utf8.count <= 64 && name.utf8.allSatisfy { byte in
            (65 ... 90).contains(byte) || (97 ... 122).contains(byte) ||
                (48 ... 57).contains(byte) || byte == 95 || byte == 45
        }
    }

    struct ValidatedResult {
        let envelope: String
        let isError: Bool
        let fact: NativeChatMCPFact?
    }

    func execute(
        name: String, rawArguments: String,
        session: NativeChatMCPAgentSession,
        accountId: String, revision: Int,
        beforeAdmission: (@MainActor () throws -> Void)? = nil,
        onStarted: (@MainActor () -> Void)? = nil
    ) async throws -> String {
        try await executeResult(
            name: name, rawArguments: rawArguments, session: session,
            accountId: accountId, revision: revision,
            beforeAdmission: beforeAdmission, onStarted: onStarted
        ).envelope
    }

    func executeResult(
        name: String, rawArguments: String,
        session: NativeChatMCPAgentSession,
        accountId: String, revision: Int,
        beforeAdmission: (@MainActor () throws -> Void)? = nil,
        onStarted: (@MainActor () -> Void)? = nil
    ) async throws -> ValidatedResult {
        let object = try validatedArguments(name: name, rawArguments: rawArguments)
        let result = try await session.callTool(
            name: name, arguments: object, accountId: accountId, revision: revision,
            beforeAdmission: beforeAdmission, onStarted: onStarted
        )
        try outputs[name]?.validate(result, arguments: object)
        let envelope: DecodedJSONValue = .object([
            "structuredContent": result.structuredContent ?? .null,
            "content": .array(result.content),
            "isError": .bool(result.isError == true)
        ])
        let output = try envelope.exactJSONData(withoutEscapingSlashes: true)
        guard output.count <= OpenAICompatibleResponsesClient.maximumToolResultBytes,
              let text = String(data: output, encoding: .utf8) else {
            throw Failure.resultTooLarge
        }
        let fact: NativeChatMCPFact? = if name == "read_my_preferences", case .string("email")? = object["option"],
                                          result.isError != true,
                                          let structuredContent = result.structuredContent {
            NativeChatMCPFact.makeEmailPreferences(structuredContent)
        } else {
            nil
        }
        return ValidatedResult(envelope: text, isError: result.isError == true, fact: fact)
    }

    func validate(name: String, rawArguments: String) throws {
        _ = try validatedArguments(name: name, rawArguments: rawArguments)
    }

    private func validatedArguments(name: String, rawArguments: String) throws -> DecodedJSONObject {
        guard let schema = schemas[name] else { throw Failure.unknownTool }
        guard rawArguments.utf8.count <= 64_000,
              let data = rawArguments.data(using: .utf8),
              let arguments = try? DecodedJSONValue(exactJSONData: data),
              case let .object(object) = arguments,
              (try? NativeChatMCPInputSchema.accepts(arguments, schema: schema)) == true
        else { throw Failure.invalidArguments }
        return object
    }
}
