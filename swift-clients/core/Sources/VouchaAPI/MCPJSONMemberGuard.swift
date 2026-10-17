import Foundation

/// Runs only after Foundation has accepted the frame's JSON syntax and nesting.
/// It checks raw keys before any collapsed dictionary can drive MCP behavior.
enum MCPJSONMemberGuard {
    private struct Context {
        let isObject: Bool
        var expectingKey: Bool
        var keys = Set<Data>()
    }

    static func validate(_ data: Data) throws {
        let bytes = Array(data)
        let decoder = JSONDecoder()
        var contexts: [Context] = []
        var index = 0
        while index < bytes.count {
            switch bytes[index] {
            case 123: contexts.append(.init(isObject: true, expectingKey: true))
            case 91: contexts.append(.init(isObject: false, expectingKey: false))
            case 125, 93: _ = contexts.popLast()
            case 44:
                if contexts.last?.isObject == true { contexts[contexts.count - 1].expectingKey = true }
            case 34:
                let start = index
                index = try stringEnd(bytes, from: start)
                if contexts.last?.expectingKey == true {
                    let key = try decoder.decode(String.self, from: Data(bytes[start ... index]))
                    // String equality canonicalizes Unicode; JSON member names must not.
                    let rawKey = Data(key.utf8)
                    guard contexts[contexts.count - 1].keys.insert(rawKey).inserted else {
                        throw MemberMCPClient.Failure.invalidResponse
                    }
                    contexts[contexts.count - 1].expectingKey = false
                }
            default: break
            }
            index += 1
        }
    }

    private static func stringEnd(_ bytes: [UInt8], from start: Int) throws -> Int {
        var index = start + 1
        while index < bytes.count {
            if bytes[index] == 34 { return index }
            index += bytes[index] == 92 ? 2 : 1
        }
        throw MemberMCPClient.Failure.invalidResponse
    }
}
