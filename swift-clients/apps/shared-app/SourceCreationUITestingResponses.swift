#if DEBUG
    import Foundation

    /// One app launch owns a response script. Consuming it is atomic across URLSession requests.
    enum SourceCreationUITestingResponses {
        private static let lock = NSLock()
        private static var responses: [String: [String]] = {
            let encoded = ProcessInfo.processInfo.environment["VOUCHA_UI_TESTING_SOURCE_CREATION_RESPONSES"] ?? "{}"
            return (try? JSONDecoder().decode([String: [String]].self, from: Data(encoded.utf8))) ?? [:]
        }()

        static func data(for request: URLRequest) -> Data? {
            lock.lock()
            defer { lock.unlock() }
            let key = "\(request.httpMethod ?? "GET"):\(request.url?.path ?? "")"
            guard var sequence = responses[key], !sequence.isEmpty else { return nil }
            let encoded = sequence.removeFirst()
            responses[key] = sequence
            return Data(base64Encoded: encoded)
        }
    }
#endif
