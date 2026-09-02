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

protocol LocalLLMAddressResolving: Sendable {
    func addresses(for host: String) async throws -> [LocalLLMIPAddress]
}

protocol LocalLLMConnectProbing: Sendable {
    func canConnect(to address: LocalLLMIPAddress, port: Int, timeout: TimeInterval) async throws
        -> Bool
}

struct SystemLocalLLMAddressResolver: LocalLLMAddressResolving {
    private static let queue = DispatchQueue(label: "voucha.local-llm.dns", qos: .userInitiated)

    func addresses(for host: String) async throws -> [LocalLLMIPAddress] {
        try await withCheckedThrowingContinuation { continuation in
            Self.queue.async {
                continuation.resume(with: Result { try Self.resolveBlocking(host) })
            }
        }
    }

    private static func resolveBlocking(_ host: String) throws -> [LocalLLMIPAddress] {
        var hints = addrinfo()
        hints.ai_family = AF_UNSPEC
        hints.ai_socktype = DarwinOrGlibc.streamSocketType
        var result: UnsafeMutablePointer<addrinfo>?
        let status = getaddrinfo(host, nil, &hints, &result)
        guard status == 0, let first = result else {
            throw LocalLLMError.dnsResolutionFailed
        }
        defer { freeaddrinfo(first) }
        var addresses: [LocalLLMIPAddress] = []
        var current: UnsafeMutablePointer<addrinfo>? = first
        while let info = current?.pointee {
            if let parsed = parse(info), !addresses.contains(parsed) {
                addresses.append(parsed)
            }
            current = info.ai_next
        }
        return addresses
    }

    private static func parse(_ info: addrinfo) -> LocalLLMIPAddress? {
        guard let raw = info.ai_addr else { return nil }
        switch Int32(info.ai_family) {
        case AF_INET:
            let address = raw.withMemoryRebound(to: sockaddr_in.self, capacity: 1) { $0.pointee }
            return textual(family: AF_INET, bytes: withUnsafeBytes(of: address.sin_addr) {
                Array($0.prefix(4))
            }).flatMap(LocalLLMIPAddress.parse)
        case AF_INET6:
            let address = raw.withMemoryRebound(to: sockaddr_in6.self, capacity: 1) { $0.pointee }
            if address.sin6_scope_id != 0 {
                return nil
            }
            return textual(family: AF_INET6, bytes: withUnsafeBytes(of: address.sin6_addr) {
                Array($0.prefix(16))
            }).flatMap(LocalLLMIPAddress.parse)
        default:
            return nil
        }
    }

    private static func textual(family: Int32, bytes: [UInt8]) -> String? {
        let length = family == AF_INET ? INET_ADDRSTRLEN : INET6_ADDRSTRLEN
        var buffer = [CChar](repeating: 0, count: Int(length))
        let converted = bytes.withUnsafeBytes { raw -> Bool in
            guard let base = raw.baseAddress else { return false }
            return inet_ntop(family, base, &buffer, socklen_t(length)) != nil
        }
        guard converted else { return nil }
        return String(cString: buffer)
    }
}

struct SystemLocalLLMConnectProbe: LocalLLMConnectProbing {
    private static let queue = DispatchQueue(label: "voucha.local-llm.connect-probe", qos: .userInitiated)

    func canConnect(to address: LocalLLMIPAddress, port: Int, timeout: TimeInterval) async throws
        -> Bool {
        try await withCheckedThrowingContinuation { continuation in
            Self.queue.async {
                continuation.resume(returning: Self.probeBlocking(
                    address: address,
                    port: port,
                    timeout: timeout
                ))
            }
        }
    }

    private static func probeBlocking(
        address: LocalLLMIPAddress,
        port: Int,
        timeout: TimeInterval
    ) -> Bool {
        let target = address.connectAddress
        let socketFD = socket(
            Int32(target.family == .ipv4 ? AF_INET : AF_INET6),
            DarwinOrGlibc.streamSocketType,
            Int32(IPPROTO_TCP)
        )
        guard socketFD >= 0 else { return false }
        defer { close(socketFD) }
        let flags = fcntl(socketFD, F_GETFL, 0)
        guard flags >= 0, fcntl(socketFD, F_SETFL, flags | O_NONBLOCK) == 0 else { return false }
        let started = connect(socketFD: socketFD, address: target, port: port)
        if started == 0 {
            return true
        }
        if errno != EINPROGRESS {
            return false
        }
        var pollFile = pollfd(fd: socketFD, events: Int16(POLLOUT), revents: 0)
        let milliseconds = Int32((timeout * 1_000).rounded(.up))
        guard poll(&pollFile, 1, milliseconds) > 0 else { return false }
        var error: Int32 = 0
        var length = socklen_t(MemoryLayout<Int32>.size)
        guard getsockopt(socketFD, SOL_SOCKET, SO_ERROR, &error, &length) == 0 else { return false }
        return error == 0
    }

    private static func connect(socketFD: Int32, address: LocalLLMIPAddress, port: Int) -> Int32 {
        if address.family == .ipv4 {
            var socketAddress = sockaddr_in()
            #if canImport(Darwin)
                socketAddress.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
            #endif
            socketAddress.sin_family = sa_family_t(AF_INET)
            socketAddress.sin_port = in_port_t(UInt16(port).bigEndian)
            withUnsafeMutableBytes(of: &socketAddress.sin_addr) { buffer in
                address.octets.withUnsafeBytes { buffer.copyMemory(from: $0) }
            }
            return withUnsafePointer(to: &socketAddress) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                    DarwinOrGlibc.connect(socketFD, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
                }
            }
        }
        var socketAddress = sockaddr_in6()
        #if canImport(Darwin)
            socketAddress.sin6_len = UInt8(MemoryLayout<sockaddr_in6>.size)
        #endif
        socketAddress.sin6_family = sa_family_t(AF_INET6)
        socketAddress.sin6_port = in_port_t(UInt16(port).bigEndian)
        withUnsafeMutableBytes(of: &socketAddress.sin6_addr) { buffer in
            address.octets.withUnsafeBytes { buffer.copyMemory(from: $0) }
        }
        return withUnsafePointer(to: &socketAddress) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                DarwinOrGlibc.connect(socketFD, $0, socklen_t(MemoryLayout<sockaddr_in6>.size))
            }
        }
    }
}

private enum DarwinOrGlibc {
    static var streamSocketType: Int32 {
        #if canImport(Darwin)
            SOCK_STREAM
        #else
            Int32(SOCK_STREAM.rawValue)
        #endif
    }

    static func connect(
        _ socketFD: Int32,
        _ address: UnsafePointer<sockaddr>,
        _ length: socklen_t
    ) -> Int32 {
        #if canImport(Darwin)
            Darwin.connect(socketFD, address, length)
        #elseif canImport(Glibc)
            Glibc.connect(socketFD, address, length)
        #elseif canImport(Android)
            Android.connect(socketFD, address, length)
        #else
            Musl.connect(socketFD, address, length)
        #endif
    }
}
