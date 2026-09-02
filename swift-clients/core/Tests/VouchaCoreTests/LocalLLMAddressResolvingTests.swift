#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#endif
import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMAddressResolvingTests: XCTestCase {
    func testSystemResolverResolvesLocalhostToPrivateAddresses() async throws {
        let addresses = try await SystemLocalLLMAddressResolver().addresses(for: "localhost")
        XCTAssertFalse(addresses.isEmpty)
        XCTAssertTrue(
            addresses.contains(where: \.isPrivateNetworkAddress),
            "localhost should resolve to at least one loopback address, got \(addresses.map(\.textual))"
        )
    }

    func testSystemResolverFailsForAnUnresolvableHost() async {
        do {
            _ = try await SystemLocalLLMAddressResolver()
                .addresses(for: "no-such-host.invalid")
            XCTFail("Expected DNS failure")
        } catch {
            XCTAssertEqual(error as? LocalLLMError, .dnsResolutionFailed)
        }
    }

    func testSystemProbeAcceptsAListeningLoopbackPortAndRejectsAClosedOne() async throws {
        let listener = try LoopbackListener()
        let port = listener.port
        let probe = SystemLocalLLMConnectProbe()
        let reachable = try await probe.canConnect(
            to: XCTUnwrap(LocalLLMIPAddress.parse("127.0.0.1")),
            port: port,
            timeout: 1
        )
        XCTAssertTrue(reachable)
        listener.close()
        let closed = try await probe.canConnect(
            to: XCTUnwrap(LocalLLMIPAddress.parse("127.0.0.1")),
            port: port,
            timeout: 0.3
        )
        XCTAssertFalse(closed)
    }
}

private final class LoopbackListener {
    private var fileDescriptor: Int32
    let port: Int

    init() throws {
        #if canImport(Darwin)
            let socketFD = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP)
        #else
            let socketFD = socket(AF_INET, Int32(SOCK_STREAM.rawValue), Int32(IPPROTO_TCP))
        #endif
        guard socketFD >= 0 else { throw POSIXError(.EPERM) }
        var reuse: Int32 = 1
        _ = setsockopt(socketFD, SOL_SOCKET, SO_REUSEADDR, &reuse, socklen_t(MemoryLayout<Int32>.size))
        var address = sockaddr_in()
        #if canImport(Darwin)
            address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        #endif
        address.sin_family = sa_family_t(AF_INET)
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        address.sin_port = 0
        let bindResult = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                bind(socketFD, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bindResult == 0, listen(socketFD, 1) == 0 else {
            closeSocket(socketFD)
            throw POSIXError(.EADDRINUSE)
        }
        var bound = sockaddr_in()
        var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        let nameResult = withUnsafeMutablePointer(to: &bound) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                getsockname(socketFD, $0, &length)
            }
        }
        guard nameResult == 0 else {
            closeSocket(socketFD)
            throw POSIXError(.EINVAL)
        }
        fileDescriptor = socketFD
        port = Int(UInt16(bigEndian: bound.sin_port))
    }

    func close() {
        if fileDescriptor >= 0 {
            closeSocket(fileDescriptor)
            fileDescriptor = -1
        }
    }

    deinit {
        close()
    }
}

private func closeSocket(_ socketFD: Int32) {
    #if canImport(Darwin)
        Darwin.close(socketFD)
    #elseif canImport(Glibc)
        Glibc.close(socketFD)
    #endif
}
