import Foundation

public extension DecodedJSONValue {
    private static let maximumExactJSONBytes = 1_100_000

    /// Decodes JSON while preserving exact object member spellings.
    init(exactJSONData data: Data) throws {
        guard data.count <= Self.maximumExactJSONBytes else {
            throw DecodingError.dataCorrupted(.init(codingPath: [], debugDescription: "JSON exceeds byte limit"))
        }
        var parser = Parser(bytes: Array(data))
        let value = try parser.value(depth: 0)
        parser.whitespace()
        guard parser.index == parser.bytes.count else {
            throw DecodingError.dataCorrupted(.init(codingPath: [], debugDescription: "Trailing JSON data"))
        }
        guard (try? JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed])) != nil else {
            throw DecodingError.dataCorrupted(.init(codingPath: [], debugDescription: "Invalid JSON"))
        }
        self = value
    }

    /// Encodes JSON without routing object names through keyed Codable containers.
    func exactJSONData(withoutEscapingSlashes: Bool = false) throws -> Data {
        var output = Data()
        try Self.append(self, to: &output, depth: 0, withoutEscapingSlashes: withoutEscapingSlashes)
        return output
    }

    private static func append(
        _ value: DecodedJSONValue, to output: inout Data, depth: Int, withoutEscapingSlashes: Bool
    ) throws {
        guard depth <= 64 else {
            throw EncodingError.invalidValue(
                value,
                .init(codingPath: [], debugDescription: "JSON nesting exceeds limit")
            )
        }
        switch value {
        case .null, .bool, .number, .integer, .unsignedInteger, .decimal, .string:
            try append(encoder(withoutEscapingSlashes).encode(value), to: &output)
        case let .array(values):
            try append(91, to: &output)
            for index in values.indices {
                if index > 0 { try append(44, to: &output) }
                try append(values[index], to: &output, depth: depth + 1, withoutEscapingSlashes: withoutEscapingSlashes)
            }
            try append(93, to: &output)
        case let .object(object):
            guard !object.hasDuplicateMemberNames else {
                throw EncodingError.invalidValue(value, .init(
                    codingPath: [], debugDescription: "JSON object contains a duplicate member name"
                ))
            }
            try append(123, to: &output)
            for (index, member) in object.enumerated() {
                if index > 0 { try append(44, to: &output) }
                try append(encoder(withoutEscapingSlashes).encode(member.0), to: &output)
                try append(58, to: &output)
                try append(member.1, to: &output, depth: depth + 1, withoutEscapingSlashes: withoutEscapingSlashes)
            }
            try append(125, to: &output)
        }
    }

    private static func append(_ byte: UInt8, to output: inout Data) throws {
        guard output.count < maximumExactJSONBytes else {
            throw EncodingError.invalidValue(
                output.count,
                .init(codingPath: [], debugDescription: "JSON exceeds byte limit")
            )
        }
        output.append(byte)
    }

    private static func append(_ data: Data, to output: inout Data) throws {
        guard data.count <= maximumExactJSONBytes - output.count else {
            throw EncodingError.invalidValue(output.count + data.count, .init(
                codingPath: [], debugDescription: "JSON exceeds byte limit"
            ))
        }
        output.append(data)
    }

    private static func encoder(_ withoutEscapingSlashes: Bool) -> JSONEncoder {
        let encoder = JSONEncoder()
        if withoutEscapingSlashes { encoder.outputFormatting = .withoutEscapingSlashes }
        return encoder
    }

    private struct Parser {
        let bytes: [UInt8]
        var index = 0

        mutating func value(depth: Int) throws -> DecodedJSONValue {
            guard depth <= 64, index < bytes.count else { throw invalidJSON() }
            whitespace()
            guard index < bytes.count else { throw invalidJSON() }
            switch bytes[index] {
            case 123: return try object(depth: depth)
            case 91: return try array(depth: depth)
            case 34: return try .string(string())
            default: return try scalarToken()
            }
        }

        mutating func object(depth: Int) throws -> DecodedJSONValue {
            index += 1
            whitespace()
            var members: [DecodedJSONObject.Element] = []
            var identities = Set<Data>()
            if consume(125) { return .object(DecodedJSONObject(members: members)) }
            while true {
                whitespace()
                guard index < bytes.count, bytes[index] == 34 else { throw invalidJSON() }
                let key = try string()
                guard identities.insert(Data(key.utf8)).inserted else { throw invalidJSON() }
                whitespace()
                guard consume(58) else { throw invalidJSON() }
                try members.append((key, value(depth: depth + 1)))
                whitespace()
                if consume(125) { break }
                guard consume(44) else { throw invalidJSON() }
            }
            return .object(DecodedJSONObject(members: members))
        }

        mutating func array(depth: Int) throws -> DecodedJSONValue {
            index += 1
            whitespace()
            var values: [DecodedJSONValue] = []
            if consume(93) { return .array(values) }
            while true {
                try values.append(value(depth: depth + 1))
                whitespace()
                if consume(93) { break }
                guard consume(44) else { throw invalidJSON() }
            }
            return .array(values)
        }

        mutating func string() throws -> String {
            let start = index
            guard consume(34) else { throw invalidJSON() }
            while index < bytes.count {
                if bytes[index] == 34 {
                    index += 1
                    return try JSONDecoder().decode(String.self, from: Data(bytes[start ..< index]))
                }
                if bytes[index] == 92 { index += 1 }
                index += 1
            }
            throw invalidJSON()
        }

        mutating func scalarToken() throws -> DecodedJSONValue {
            let start = index
            while index < bytes.count, !isDelimiter(bytes[index]) {
                index += 1
            }
            guard index > start else { throw invalidJSON() }
            return try JSONDecoder().decode(DecodedJSONValue.self, from: Data(bytes[start ..< index]))
        }

        mutating func whitespace() {
            while index < bytes.count, [UInt8(32), 9, 10, 13].contains(bytes[index]) {
                index += 1
            }
        }

        mutating func consume(_ byte: UInt8) -> Bool {
            guard index < bytes.count, bytes[index] == byte else { return false }
            index += 1
            return true
        }

        private func isDelimiter(_ byte: UInt8) -> Bool {
            [UInt8(44), 93, 125, 32, 9, 10, 13].contains(byte)
        }

        private func invalidJSON() -> DecodingError {
            .dataCorrupted(.init(codingPath: [], debugDescription: "Invalid exact-key JSON"))
        }
    }
}
