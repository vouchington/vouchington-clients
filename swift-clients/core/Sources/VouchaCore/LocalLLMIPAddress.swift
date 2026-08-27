#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Android)
    import Android
#elseif canImport(Musl)
    import Musl
#endif
import Foundation

struct LocalLLMIPAddress: Equatable {
    enum Family {
        case ipv4
        case ipv6
    }

    let family: Family
    let octets: [UInt8]
    let textual: String

    var isIPv4MappedToIPv6: Bool {
        family == .ipv6
            && octets.count == 16
            && octets.prefix(10).allSatisfy { $0 == 0 }
            && octets[10] == 0xFF
            && octets[11] == 0xFF
    }

    var mappedIPv4: LocalLLMIPAddress? {
        guard isIPv4MappedToIPv6 else { return nil }
        let ipv4 = Array(octets.suffix(4))
        return LocalLLMIPAddress(
            family: .ipv4,
            octets: ipv4,
            textual: ipv4.map(String.init).joined(separator: ".")
        )
    }

    var connectAddress: LocalLLMIPAddress {
        mappedIPv4 ?? self
    }

    var isPrivateNetworkAddress: Bool {
        if let mapped = mappedIPv4 {
            return mapped.isLoopbackIPv4
        }
        switch family {
        case .ipv4:
            return isPrivateIPv4
        case .ipv6:
            return isLoopbackIPv6 || isUniqueLocalIPv6 || isLinkLocalIPv6
        }
    }

    private var isPrivateIPv4: Bool {
        octets[0] == 10
            || (octets[0] == 172 && (16 ... 31).contains(octets[1]))
            || (octets[0] == 192 && octets[1] == 168)
            || (octets[0] == 169 && octets[1] == 254)
            || octets[0] == 127
    }

    private var isLoopbackIPv4: Bool {
        octets[0] == 127
    }

    private var isLoopbackIPv6: Bool {
        octets.dropLast().allSatisfy { $0 == 0 } && octets.last == 1
    }

    private var isUniqueLocalIPv6: Bool {
        (octets[0] & 0xFE) == 0xFC
    }

    private var isLinkLocalIPv6: Bool {
        octets[0] == 0xFE && (octets[1] & 0xC0) == 0x80
    }

    static func parse(_ raw: String) -> LocalLLMIPAddress? {
        let host = raw.trimmingCharacters(in: CharacterSet(charactersIn: "[]"))
        if let address = parseIPv4(host) {
            return address
        }
        return parseIPv6(host)
    }

    private static func parseIPv4(_ host: String) -> LocalLLMIPAddress? {
        var address = in_addr()
        guard inet_pton(AF_INET, host, &address) == 1 else { return nil }
        let octets = withUnsafeBytes(of: address) { Array($0.prefix(4)) }
        return LocalLLMIPAddress(family: .ipv4, octets: octets, textual: host)
    }

    private static func parseIPv6(_ host: String) -> LocalLLMIPAddress? {
        var address = in6_addr()
        guard inet_pton(AF_INET6, host, &address) == 1 else { return nil }
        let octets = withUnsafeBytes(of: address) { Array($0.prefix(16)) }
        return LocalLLMIPAddress(family: .ipv6, octets: octets, textual: host)
    }
}
