import Foundation

public protocol FeatureFlagOverridePersisting: Sendable {
    func load() -> [String: Bool]
    func set(_ value: Bool, for key: String) throws
    func remove(_ key: String) throws
    func clear() throws
}

public final class FeatureFlagOverrideFileStore: FeatureFlagOverridePersisting, @unchecked Sendable {
    private let fileURL: URL
    private let fileManager: FileManager
    private let lock = NSLock()

    public init(fileURL: URL, fileManager: FileManager = .default) {
        self.fileURL = fileURL
        self.fileManager = fileManager
    }

    public static func applicationSupport(fileManager: FileManager = .default) -> FeatureFlagOverrideFileStore {
        let baseURL = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? fileManager.temporaryDirectory
        return FeatureFlagOverrideFileStore(
            fileURL: baseURL.appendingPathComponent("Voucha/feature-flag-overrides.json"),
            fileManager: fileManager
        )
    }

    public func load() -> [String: Bool] {
        lock.withLock {
            guard let data = try? Data(contentsOf: fileURL) else { return [:] }
            return (try? JSONDecoder().decode([String: Bool].self, from: data)) ?? [:]
        }
    }

    public func set(_ value: Bool, for key: String) throws {
        try mutate { $0[key] = value }
    }

    public func remove(_ key: String) throws {
        try mutate { $0.removeValue(forKey: key) }
    }

    public func clear() throws {
        try mutate { $0.removeAll() }
    }

    private func mutate(_ update: (inout [String: Bool]) -> Void) throws {
        try lock.withLock {
            var values: [String: Bool] = if let data = try? Data(contentsOf: fileURL) {
                (try? JSONDecoder().decode([String: Bool].self, from: data)) ?? [:]
            } else {
                [:]
            }
            update(&values)
            try fileManager.createDirectory(
                at: fileURL.deletingLastPathComponent(),
                withIntermediateDirectories: true
            )
            try JSONEncoder().encode(values).write(to: fileURL, options: .atomic)
        }
    }
}
