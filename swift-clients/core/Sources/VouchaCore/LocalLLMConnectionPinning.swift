import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

enum LocalLLMConnectionPinning {
    static func selectValidatedAddresses(
        _ addresses: [LocalLLMIPAddress],
        requestURL: URL?,
        host: String
    ) throws -> [LocalLLMIPAddress] {
        guard requestURL != nil else {
            throw LocalLLMError.noUsableResolvedAddress(host: host)
        }
        if addresses.isEmpty {
            throw LocalLLMError.noUsableResolvedAddress(host: host)
        }
        guard requestURL?.scheme?.lowercased() == "http" else {
            return addresses
        }
        let privateAddresses = addresses.filter(\.isPrivateNetworkAddress)
        if privateAddresses.isEmpty {
            throw LocalLLMError.resolvedAddressNotPrivate(host: host)
        }
        return privateAddresses
    }

    static func pinnedRequest(
        from request: URLRequest,
        address: LocalLLMIPAddress,
        originalHost: String
    ) throws -> URLRequest {
        guard let url = request.url,
              var components = URLComponents(url: url, resolvingAgainstBaseURL: false)
        else {
            throw LocalLLMError.noUsableResolvedAddress(host: originalHost)
        }
        let connectAddress = address.connectAddress
        if connectAddress.family == .ipv6 {
            components.percentEncodedHost = "[\(connectAddress.textual)]"
        } else {
            components.host = connectAddress.textual
        }
        guard let pinnedURL = components.url else {
            throw LocalLLMError.noUsableResolvedAddress(host: originalHost)
        }
        var pinned = request
        pinned.url = pinnedURL
        if request.value(forHTTPHeaderField: "Host") == nil {
            pinned.setValue(hostHeader(for: url, fallbackHost: originalHost), forHTTPHeaderField: "Host")
        }
        return pinned
    }

    static func hostHeader(for url: URL, fallbackHost: String) -> String {
        let host = url.host ?? fallbackHost
        let formatted = host.contains(":") ? "[\(host)]" : host
        guard let port = url.port else { return formatted }
        let scheme = url.scheme?.lowercased()
        if (scheme == "http" && port == 80) || (scheme == "https" && port == 443) {
            return formatted
        }
        return "\(formatted):\(port)"
    }

    static func data(
        for request: URLRequest,
        session: URLSession,
        resolver: any LocalLLMAddressResolving,
        probe: any LocalLLMConnectProbing
    ) async throws -> (Data, URLResponse) {
        guard let url = request.url, let host = url.host, !host.isEmpty else {
            throw LocalLLMError.noUsableResolvedAddress(host: request.url?.host ?? "")
        }
        if url.scheme?.lowercased() != "http" {
            return try await session.data(for: request)
        }
        if let literal = LocalLLMIPAddress.parse(host) {
            _ = try selectValidatedAddresses([literal], requestURL: url, host: host)
            return try await session.data(for: request)
        }
        try Task.checkCancellation()
        let resolved = try await resolver.addresses(for: host)
        try Task.checkCancellation()
        let selected = try selectValidatedAddresses(resolved, requestURL: url, host: host)
        return try await send(
            SendRequest(
                request: request,
                url: url,
                host: host,
                candidates: selected,
                session: session,
                probe: probe
            )
        )
    }

    private struct SendRequest {
        let request: URLRequest
        let url: URL
        let host: String
        let candidates: [LocalLLMIPAddress]
        let session: URLSession
        let probe: any LocalLLMConnectProbing
    }

    private static func send(_ context: SendRequest) async throws -> (Data, URLResponse) {
        var lastFailure: Error?
        let port = context.url.port ?? 80
        for (index, address) in context.candidates.enumerated() {
            try Task.checkCancellation()
            let isLast = index == context.candidates.count - 1
            if !isLast {
                let reachable = try await context.probe.canConnect(
                    to: address,
                    port: port,
                    timeout: 5
                )
                if !reachable {
                    lastFailure = LocalLLMError.unableToConnect(host: context.host)
                    continue
                }
            }
            try Task.checkCancellation()
            let pinned = try pinnedRequest(
                from: context.request,
                address: address,
                originalHost: context.host
            )
            do {
                return try await context.session.data(for: pinned)
            } catch let error as URLError where isImmediateConnectFailure(error) {
                lastFailure = error
                if isLast {
                    throw LocalLLMError.unableToConnect(host: context.host)
                }
            }
        }
        throw lastFailure ?? LocalLLMError.unableToConnect(host: context.host)
    }

    private static func isImmediateConnectFailure(_ error: URLError) -> Bool {
        switch error.code {
        case .cannotConnectToHost, .cannotFindHost:
            true
        default:
            false
        }
    }
}
