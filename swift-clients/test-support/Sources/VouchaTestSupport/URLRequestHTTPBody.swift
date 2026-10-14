import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

public enum URLRequestHTTPBody {
    public static func data(from request: URLRequest) -> Data? {
        if let body = request.httpBody {
            return body
        }
        guard let stream = request.httpBodyStream else { return nil }
        return read(stream)
    }

    public static func utf8String(from request: URLRequest) -> String? {
        guard let body = data(from: request) else { return nil }
        return String(data: body, encoding: .utf8)
    }

    private static func read(_ stream: InputStream) -> Data {
        stream.open()
        defer { stream.close() }
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 4_096)
        while stream.hasBytesAvailable {
            let count = stream.read(&buffer, maxLength: buffer.count)
            if count <= 0 {
                break
            }
            data.append(buffer, count: count)
        }
        return data
    }
}
