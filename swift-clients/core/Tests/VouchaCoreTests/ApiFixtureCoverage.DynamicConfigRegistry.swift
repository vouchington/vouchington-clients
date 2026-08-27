import VouchaAPI
import VouchaModels

let dynamicConfigApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.feature-flags.default") {
        try assertFixtureCoversDTO($0, as: FeatureFlagsResponse.self)
    },
    RegisteredFixture(id: "native.captcha-config.default") {
        try assertFixtureCoversDTO($0, as: CaptchaConfigResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.namespaces.developer") {
        try assertFixtureCoversDTO($0, as: DynamicConfigNamespacesResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.namespace.typed") {
        try assertFixtureCoversDTO($0, as: DynamicConfigNamespaceResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.namespace.string") {
        try assertFixtureCoversDTO($0, as: DynamicConfigNamespaceResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.namespace.integer") {
        try assertFixtureCoversDTO($0, as: DynamicConfigNamespaceResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.update.changed") {
        try assertFixtureCoversDTO($0, as: DynamicConfigUpdateResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.update.no-op") {
        try assertFixtureCoversDTO($0, as: DynamicConfigUpdateResponse.self)
    },
    RegisteredFixture(id: "native.dynamic-config.history.default") {
        try assertFixtureCoversDTO($0, as: DynamicConfigHistoryResponse.self)
    }
]

let dynamicConfigEndpointFixtureCoverage: [String: Endpoint] = [
    "native.feature-flags.default": Endpoint.featureFlags,
    "native.captcha-config.default": Endpoint.captchaConfig,
    "native.dynamic-config.namespaces.developer": Endpoint.dynamicConfigNamespaces,
    "native.dynamic-config.namespace.typed": Endpoint.dynamicConfigNamespace("recaptcha-config"),
    "native.dynamic-config.namespace.string": Endpoint.dynamicConfigNamespace("app-attestation-config"),
    "native.dynamic-config.namespace.integer": Endpoint.dynamicConfigNamespace("post-content-limits-config"),
    "native.dynamic-config.update.changed": Endpoint.updateDynamicConfigNamespace(
        "feature-flags",
        field: "fediverse",
        value: .boolean(true)
    ),
    "native.dynamic-config.update.no-op": Endpoint.updateDynamicConfigNamespace(
        "feature-flags",
        field: "fediverse",
        value: .boolean(false)
    ),
    "native.dynamic-config.history.default": Endpoint.dynamicConfigHistory("feature-flags")
]
