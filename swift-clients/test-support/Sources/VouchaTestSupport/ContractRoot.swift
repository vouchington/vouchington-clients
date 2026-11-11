import Foundation

public enum ContractRoot {
    public static let environmentKey = "VOUCHA_CONTRACT_ROOT"

    public static func url(
        environment: [String: String] = ProcessInfo.processInfo.environment,
        requiredPaths: [String] = []
    ) throws -> URL {
        guard
            let value = environment[environmentKey]?.trimmingCharacters(in: .whitespacesAndNewlines),
            !value.isEmpty
        else {
            throw ContractRootError.missingEnvironment(environmentKey)
        }

        let root = URL(fileURLWithPath: value).standardizedFileURL
        try rejectSymbolicLinks(beneath: root, components: [])
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw ContractRootError.invalidRoot(root.path)
        }

        for requiredPath in requiredPaths {
            _ = try requiredURL(requiredPath, root: root)
        }
        return root
    }

    public static func requiredURL(_ requiredPath: String, root: URL) throws -> URL {
        let components = requiredPath.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
        guard !components.isEmpty, components.allSatisfy(isSafePathComponent) else {
            throw ContractRootError.missingPath(requiredPath)
        }

        try rejectSymbolicLinks(beneath: root, components: components)
        let candidate = root.appendingPathComponent(requiredPath)
        guard FileManager.default.fileExists(atPath: candidate.path) else {
            throw ContractRootError.missingPath(requiredPath)
        }
        return candidate
    }

    public static func apiContractURL(_ relativePath: String, root: URL) throws -> URL {
        try dataURL(relativePath, directory: "api-contracts/v1", root: root)
    }

    public static func apiContractURL(_ relativePath: String) throws -> URL {
        try apiContractURL(relativePath, root: url(requiredPaths: ["api-contracts/v1"]))
    }

    public static func fixtureURL(_ bodyFile: String, root: URL) throws -> URL {
        try dataURL(bodyFile, directory: "api-fixtures/v1", root: root)
    }

    private static func dataURL(_ bodyFile: String, directory: String, root: URL) throws -> URL {
        let components = bodyFile.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
        guard !components.isEmpty, components.allSatisfy(isSafePathComponent) else {
            throw directory == "api-fixtures/v1"
                ? ContractRootError.invalidFixturePath(bodyFile)
                : ContractRootError.invalidContractPath(bodyFile)
        }

        let fixtureRoot = root.appendingPathComponent(directory, isDirectory: true)
        let fixture = try requiredURL("\(directory)/\(bodyFile)", root: root)
        let resolvedRoot = fixtureRoot.resolvingSymlinksInPath().standardizedFileURL
        let resolvedFixture = fixture.resolvingSymlinksInPath().standardizedFileURL
        guard isDescendant(resolvedFixture, of: resolvedRoot) else {
            throw directory == "api-fixtures/v1"
                ? ContractRootError.invalidFixturePath(bodyFile)
                : ContractRootError.invalidContractPath(bodyFile)
        }
        return fixture
    }

    private static func isSafePathComponent(_ component: String) -> Bool {
        !component.isEmpty &&
            component != "." &&
            component != ".." &&
            !component.contains("\\") &&
            !component.contains(":")
    }

    private static func rejectSymbolicLinks(beneath root: URL, components: [String]) throws {
        if (try? FileManager.default.destinationOfSymbolicLink(atPath: root.path)) != nil {
            throw ContractRootError.symbolicLink(root.path)
        }

        var candidate = root
        for component in components {
            candidate = candidate.appendingPathComponent(component)
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: candidate.path)) != nil {
                throw ContractRootError.symbolicLink(candidate.path)
            }
        }
    }

    private static func isDescendant(_ candidate: URL, of root: URL) -> Bool {
        let rootComponents = root.pathComponents
        let candidateComponents = candidate.pathComponents
        return candidateComponents.count > rootComponents.count && candidateComponents.starts(with: rootComponents)
    }
}

public enum ContractRootError: Error, CustomStringConvertible {
    case missingEnvironment(String)
    case invalidRoot(String)
    case missingPath(String)
    case invalidFixturePath(String)
    case invalidContractPath(String)
    case symbolicLink(String)

    public var description: String {
        switch self {
        case let .missingEnvironment(key):
            "Missing required \(key). Fetch Vouchington contracts before running native contract tests."
        case let .invalidRoot(path):
            "\(ContractRoot.environmentKey) must name an existing directory, got \(path)."
        case let .missingPath(path):
            "Vouchington contract root is missing required path \(path)."
        case let .invalidFixturePath(path):
            "API fixture bodyFile must be a relative path beneath api-fixtures/v1, got \(path)."
        case let .invalidContractPath(path):
            "API contract path must be a relative path beneath api-contracts/v1, got \(path)."
        case let .symbolicLink(path):
            "Vouchington contract paths must not contain symbolic links, found \(path)."
        }
    }
}
