import VouchaAPI
import VouchaModels

struct NativeChatMCPOutputContract {
    let root: DecodedJSONValue
    private let options: [String: DecodedJSONValue]?

    init(fields: [String: DecodedJSONValue]) throws {
        guard let schema = fields["outputSchema"] else { throw NativeChatMCPToolBridge.Failure.invalidCatalog }
        try NativeChatMCPInputSchema.validateSchema(schema, output: true)
        root = schema
        if let meta = fields["_meta"], case .object = meta {} else if fields["_meta"] != nil {
            throw NativeChatMCPToolBridge.Failure.invalidCatalog
        }
        let names = Self.optionNames(fields["inputSchema"])
        guard case let .object(meta)? = fields["_meta"], let pointers = meta["voucha/optionOutputPointers"] else {
            guard names == nil else { throw NativeChatMCPToolBridge.Failure.invalidCatalog }
            options = nil
            return
        }
        guard case let .object(mapping) = pointers, let names, names == Set(mapping.keys) else {
            throw NativeChatMCPToolBridge.Failure.invalidCatalog
        }
        options = try mapping.mapValues { pointer in
            guard case let .string(path) = pointer else { throw NativeChatMCPToolBridge.Failure.invalidCatalog }
            return try Self.resolve(path, root: schema)
        }
    }

    func validate(_ result: MemberMCPClient.ToolResult, arguments: [String: DecodedJSONValue]) throws {
        guard result.isError != true else { return }
        var selected = root
        if let options {
            guard case let .string(option)? = arguments["option"], let schema = options[option] else {
                throw NativeChatMCPToolBridge.Failure.invalidOutput
            }
            selected = schema
        }
        guard let value = result.structuredContent,
              try NativeChatMCPInputSchema.acceptsOutput(value, schema: selected, root: root) else {
            throw NativeChatMCPToolBridge.Failure.invalidOutput
        }
    }

    static func optionNames(_ input: DecodedJSONValue?) -> Set<String>? {
        guard case let .object(root)? = input, case let .array(branches)? = root["oneOf"], !branches.isEmpty else {
            return nil
        }
        var names = Set<String>()
        for branch in branches {
            guard case let .object(body) = branch, case .string("object")? = body["type"],
                  case .bool(false)? = body["additionalProperties"],
                  case let .array(required)? = body["required"],
                  required.count == 2,
                  required.contains(where: { if case .string("option") = $0 { true } else { false } }),
                  required.contains(where: { if case .string("arguments") = $0 { true } else { false } }),
                  case let .object(properties)? = body["properties"],
                  Set(properties.keys) == Set(["option", "arguments"]),
                  case let .object(option)? = properties["option"] else { return nil }
            let name: String
            if case let .string(literal)? = option["const"] {
                name = literal
            } else if case let .array(values)? = option["enum"], values.count == 1,
                      case let .string(literal)? = values.first {
                name = literal
            } else {
                return nil
            }
            names.insert(name)
        }
        return names
    }

    private static func resolve(_ path: String, root: DecodedJSONValue) throws -> DecodedJSONValue {
        if path == "#" { return root }
        let parts = path.split(separator: "/", omittingEmptySubsequences: false)
        guard parts.count == 3, parts[0] == "#", parts[1] == "anyOf", let index = Int(parts[2]),
              index >= 0, String(index) == parts[2], case let .object(fields) = root,
              case let .array(branches)? = fields["anyOf"], branches.indices.contains(index) else {
            throw NativeChatMCPToolBridge.Failure.invalidCatalog
        }
        return branches[index]
    }
}
