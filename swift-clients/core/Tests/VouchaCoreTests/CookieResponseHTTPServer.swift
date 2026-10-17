#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#endif
import Foundation

final class CookieResponseHTTPServer: @unchecked Sendable {
    private var fileDescriptor: Int32
    private let requestLock = NSLock()
    private var capturedCookie: String?
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

    var receivedCookie: String? {
        requestLock.withLock { capturedCookie }
    }

    func respondOnce() async {
        let listener = fileDescriptor
        await withCheckedContinuation { continuation in
            DispatchQueue.global().async {
                var readiness = pollfd(fd: listener, events: Int16(POLLIN), revents: 0)
                guard poll(&readiness, 1, 5_000) > 0 else {
                    continuation.resume()
                    return
                }
                let connection = accept(listener, nil, nil)
                guard connection >= 0 else { continuation.resume()
                    return
                }
                defer { closeSocket(connection)
                    continuation.resume()
                }
                var timeout = timeval(tv_sec: 5, tv_usec: 0)
                guard setsockopt(
                    connection,
                    SOL_SOCKET,
                    SO_RCVTIMEO,
                    &timeout,
                    socklen_t(MemoryLayout<timeval>.size)
                ) ==
                    0,
                    setsockopt(
                        connection,
                        SOL_SOCKET,
                        SO_SNDTIMEO,
                        &timeout,
                        socklen_t(MemoryLayout<timeval>.size)
                    ) ==
                    0
                else { return }
                #if canImport(Darwin)
                    var noSignal: Int32 = 1
                    guard setsockopt(
                        connection,
                        SOL_SOCKET,
                        SO_NOSIGPIPE,
                        &noSignal,
                        socklen_t(MemoryLayout<Int32>.size)
                    ) == 0
                    else { return }
                    let sendFlags: Int32 = 0
                #else
                    let sendFlags = Int32(MSG_NOSIGNAL)
                #endif
                guard let headers = Self.receiveHeaders(connection) else { return }
                let headerLines = headers.components(separatedBy: "\r\n")
                let cookie = headerLines.first { $0.lowercased().hasPrefix("cookie:") }
                    .map { String($0.dropFirst("cookie:".count)).trimmingCharacters(in: .whitespaces) }
                self.requestLock.withLock { self.capturedCookie = cookie }
                let response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\nSet-Cookie: response_session=updated; Path=/; HttpOnly\r\n\r\n{}"
                response.withCString { bytes in
                    var offset = 0
                    while offset < response.utf8.count {
                        let sent = send(connection, bytes.advanced(by: offset), response.utf8.count - offset, sendFlags)
                        guard sent > 0 else { return }
                        offset += sent
                    }
                }
            }
        }
    }

    private static func receiveHeaders(_ connection: Int32) -> String? {
        let limit = 4_096
        let delimiter = Data([13, 10, 13, 10])
        var request = Data()
        var buffer = [UInt8](repeating: 0, count: 1_024)
        while request.count < limit {
            let received = recv(connection, &buffer, min(buffer.count, limit - request.count), 0)
            guard received > 0 else { return nil }
            request.append(contentsOf: buffer.prefix(received))
            if let boundary = request.range(of: delimiter) {
                return String(decoding: request[..<boundary.lowerBound], as: UTF8.self)
            }
        }
        return nil
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
