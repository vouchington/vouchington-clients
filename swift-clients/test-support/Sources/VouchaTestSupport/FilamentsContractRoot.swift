import Foundation

public enum FilamentsContractRoot {
    public static let environmentKey = "VOUCHA_FILAMENTS_CONTRACT_ROOT"

    public static func url(
        environment: [String: String] = ProcessInfo.processInfo.environment,
        requiredPaths: [String] = []
    ) throws -> URL {
        guard
            let value = environment[environmentKey]?.trimmingCharacters(in: .whitespacesAndNewlines),
            !value.isEmpty
        else {
            throw FilamentsContractRootError.missingEnvironment(environmentKey)
        }

        let root = URL(fileURLWithPath: value).standardizedFileURL
        try rejectSymbolicLinks(beneath: root, components: [])
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw FilamentsContractRootError.invalidRoot(root.path)
        }

        for requiredPath in requiredPaths {
            _ = try requiredURL(requiredPath, root: root)
        }
        return root
    }

    public static func requiredURL(_ requiredPath: String, root: URL) throws -> URL {
        let components = requiredPath.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
        guard !components.isEmpty, components.allSatisfy(isSafePathComponent) else {
            throw FilamentsContractRootError.missingPath(requiredPath)
        }

        try rejectSymbolicLinks(beneath: root, components: components)
        let candidate = root.appendingPathComponent(requiredPath)
        guard FileManager.default.fileExists(atPath: candidate.path) else {
            throw FilamentsContractRootError.missingPath(requiredPath)
        }
        return candidate
    }

    public static func fixtureURL(_ bodyFile: String, root: URL) throws -> URL {
        let components = bodyFile.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
        guard !components.isEmpty, components.allSatisfy(isSafePathComponent) else {
            throw FilamentsContractRootError.invalidFixturePath(bodyFile)
        }

        let fixtureRoot = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        let fixture = try requiredURL("api-fixtures/v1/\(bodyFile)", root: root)
        let resolvedRoot = fixtureRoot.resolvingSymlinksInPath().standardizedFileURL
        let resolvedFixture = fixture.resolvingSymlinksInPath().standardizedFileURL
        guard isDescendant(resolvedFixture, of: resolvedRoot) else {
            throw FilamentsContractRootError.invalidFixturePath(bodyFile)
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
            throw FilamentsContractRootError.symbolicLink(root.path)
        }

        var candidate = root
        for component in components {
            candidate = candidate.appendingPathComponent(component)
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: candidate.path)) != nil {
                throw FilamentsContractRootError.symbolicLink(candidate.path)
            }
        }
    }

    private static func isDescendant(_ candidate: URL, of root: URL) -> Bool {
        let rootComponents = root.pathComponents
        let candidateComponents = candidate.pathComponents
        return candidateComponents.count > rootComponents.count && candidateComponents.starts(with: rootComponents)
    }
}

public enum FilamentsContractRootError: Error, CustomStringConvertible {
    case missingEnvironment(String)
    case invalidRoot(String)
    case missingPath(String)
    case invalidFixturePath(String)
    case symbolicLink(String)

    public var description: String {
        switch self {
        case let .missingEnvironment(key):
            "Missing required \(key). Fetch Filaments contracts before running native contract tests."
        case let .invalidRoot(path):
            "\(FilamentsContractRoot.environmentKey) must name an existing directory, got \(path)."
        case let .missingPath(path):
            "Filaments contract root is missing required path \(path)."
        case let .invalidFixturePath(path):
            "API fixture bodyFile must be a relative path beneath api-fixtures/v1, got \(path)."
        case let .symbolicLink(path):
            "API fixture paths must not contain symbolic links, found \(path)."
        }
    }
}
