private struct DynamicConfigUpdateBody: Encodable {
    let config: [String: DynamicConfigValue]
}

public extension Endpoint {
    static var dynamicConfigNamespaces: Endpoint {
        Endpoint(.GET, path: "/api/v1/dynamic-config/namespaces")
    }

    static func dynamicConfigNamespace(_ namespace: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/dynamic-config/namespaces/\(pathSegment(namespace))")
    }

    static func updateDynamicConfigNamespace(
        _ namespace: String,
        field: String,
        value: DynamicConfigValue
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/dynamic-config/namespaces/\(pathSegment(namespace))",
            body: DynamicConfigUpdateBody(config: [field: value])
        )
    }

    static func dynamicConfigHistory(_ namespace: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/dynamic-config/namespaces/\(pathSegment(namespace))/history")
    }
}
