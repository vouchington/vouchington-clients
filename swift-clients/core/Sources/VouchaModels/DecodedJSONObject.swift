import Foundation

/// An ordered JSON object whose member names are compared by their exact UTF-8
/// spelling. Unlike `Dictionary<String, Value>`, this can represent distinct
/// canonically equivalent Unicode names without silently merging them.
///
/// Codable decoding uses the decoder's keyed container and cannot recover
/// distinct names after a decoder canonicalizes them. Use
/// `DecodedJSONValue(exactJSONData:)` at arbitrary JSON boundaries.
public struct DecodedJSONObject: Codable, Sendable, ExpressibleByDictionaryLiteral, Sequence {
    public typealias Element = (String, DecodedJSONValue)

    private var members: [Element]
    private var exactIndex: [Data: Int]

    public init(dictionaryLiteral elements: (String, DecodedJSONValue)...) {
        members = elements
        exactIndex = Self.index(members)
    }

    public init(_ dictionary: [String: DecodedJSONValue]) {
        members = dictionary.map { ($0.key, $0.value) }
        exactIndex = Self.index(members)
    }

    public init(members: [Element]) {
        self.members = members
        exactIndex = Self.index(members)
    }

    public var count: Int {
        members.count
    }

    public var isEmpty: Bool {
        members.isEmpty
    }

    public var keys: [String] {
        members.map(\.0)
    }

    public var hasCanonicallyEquivalentMemberNames: Bool {
        var names = Set<String>()
        return members.contains { !names.insert($0.0).inserted }
    }

    public var hasDuplicateMemberNames: Bool {
        var names = Set<Data>()
        return members.contains { !names.insert(Data($0.0.utf8)).inserted }
    }

    public subscript(_ key: String) -> DecodedJSONValue? {
        get {
            guard let index = exactIndex[Data(key.utf8)] else { return nil }
            return members[index].1
        }
        set {
            let bytes = Data(key.utf8)
            if let index = exactIndex[bytes] {
                if let newValue {
                    members[index] = (members[index].0, newValue)
                } else {
                    members.remove(at: index)
                    exactIndex = Self.index(members)
                }
            } else if let newValue {
                members.append((key, newValue))
                exactIndex[bytes] = members.count - 1
            }
        }
    }

    @discardableResult
    public mutating func removeValue(forKey key: String) -> DecodedJSONValue? {
        let oldValue = self[key]
        self[key] = nil
        return oldValue
    }

    public func makeIterator() -> IndexingIterator<[Element]> {
        members.makeIterator()
    }

    public func mapValues<T>(_ transform: (DecodedJSONValue) throws -> T) throws -> [String: T] {
        guard !hasCanonicallyEquivalentMemberNames, !hasDuplicateMemberNames else {
            throw EncodingError.invalidValue(self, .init(
                codingPath: [], debugDescription: "A String-keyed dictionary cannot retain exact JSON member names"
            ))
        }
        var result: [String: T] = [:]
        for (key, value) in members {
            result[key] = try transform(value)
        }
        return result
    }

    public func encode(to encoder: Encoder) throws {
        guard !hasCanonicallyEquivalentMemberNames, !hasDuplicateMemberNames else {
            throw EncodingError.invalidValue(self, .init(
                codingPath: encoder.codingPath,
                debugDescription: "A keyed Codable container cannot encode canonically equivalent JSON names"
            ))
        }
        var dictionary: [String: DecodedJSONValue] = [:]
        for (key, value) in members {
            dictionary[key] = value
        }
        var container = encoder.singleValueContainer()
        try container.encode(dictionary)
    }

    public init(from decoder: Decoder) throws {
        let dictionary = try decoder.singleValueContainer().decode([String: DecodedJSONValue].self)
        members = dictionary.map { ($0.key, $0.value) }
        exactIndex = Self.index(members)
    }

    private static func index(_ members: [Element]) -> [Data: Int] {
        var result: [Data: Int] = [:]
        for (offset, member) in members.enumerated() {
            result[Data(member.0.utf8)] = offset
        }
        return result
    }
}
