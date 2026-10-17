import VouchaAPI
import VouchaModels

struct NativeChatMCPOutputContract {
    let root: DecodedJSONValue
    private let options: [String: DecodedJSONValue]?
    private let rootOptions: Set<String>
    private let rootAnyOfOptions: [String: Int]

    init(fields: [String: DecodedJSONValue]) throws {
        guard let schema = fields["outputSchema"] else { throw NativeChatMCPToolBridge.Failure.invalidCatalog }
        try NativeChatMCPInputSchema.validateSchema(schema, output: true)
        root = schema
        rootAnyOfOptions = Self.rootAnyOfIndices(fields)
        rootOptions = Self.rootPointerOptions(fields)
        if let meta = fields["_meta"], case .object = meta {} else if fields["_meta"] != nil {
            throw NativeChatMCPToolBridge.Failure.invalidCatalog
        }
        let names = Self.optionNames(fields["inputSchema"])
        if names?.isEmpty == true { throw NativeChatMCPToolBridge.Failure.invalidCatalog }
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
        var selected: DecodedJSONValue?
        var selectedIndex: Int?
        if let options {
            guard case let .string(option)? = arguments["option"], let schema = options[option] else {
                throw NativeChatMCPToolBridge.Failure.invalidOutput
            }
            selected = rootOptions.contains(option) ? nil : schema
            selectedIndex = rootAnyOfOptions[option]
        }
        guard let value = result.structuredContent,
              try NativeChatMCPInputSchema.acceptsOutput(
                  value, schema: root, root: root, selectedSchema: selected, selectedRootAnyOfIndex: selectedIndex
              ) else {
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
            // An empty set is an invalid-canonical-options sentinel: this shape always has at
            // least one option, while nil means the schema is an ordinary, unselected oneOf.
            guard names.insert(name).inserted else { return [] }
        }
        return names
    }

    private static func pointerMapping(_ fields: [String: DecodedJSONValue]) -> [String: DecodedJSONValue] {
        guard case let .object(meta)? = fields["_meta"],
              case let .object(mapping)? = meta["voucha/optionOutputPointers"] else { return [:] }
        return mapping
    }

    private static func rootAnyOfIndices(_ fields: [String: DecodedJSONValue]) -> [String: Int] {
        pointerMapping(fields).reduce(into: [:]) { indices, entry in
            if case let .string(path) = entry.value, path.hasPrefix("#/anyOf/"),
               let index = Int(path.dropFirst("#/anyOf/".count)) { indices[entry.key] = index }
        }
    }

    private static func rootPointerOptions(_ fields: [String: DecodedJSONValue]) -> Set<String> {
        Set(pointerMapping(fields).compactMap { key, value in
            if case .string("#") = value { key } else { nil }
        })
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
