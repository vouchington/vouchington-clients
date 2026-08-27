import Foundation

public actor MemoryDiskCacheStore: CacheStore {
    private struct Entry: Codable {
        let data: Data
        let expiresAt: Date?
    }

    private var memory: [String: Entry] = [:]
    private let diskURL: URL
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    public init(directory: URL? = nil) {
        let base =
            directory
                ?? FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first
                ?? FileManager.default.temporaryDirectory
        diskURL = base.appendingPathComponent("ai.voucha.cache", isDirectory: true)
        try? FileManager.default.createDirectory(at: diskURL, withIntermediateDirectories: true)
    }

    public func get<T: Codable & Sendable>(key: String, type _: T.Type) async -> T? {
        let file = diskURL.appendingPathComponent(safeKey(key))
        if let entry = memory[key] {
            if isExpired(entry) {
                memory.removeValue(forKey: key)
                try? FileManager.default.removeItem(at: file)
                return nil
            }
            return try? decoder.decode(T.self, from: entry.data)
        }
        guard let data = try? Data(contentsOf: file),
              let entry = try? decoder.decode(Entry.self, from: data)
        else { return nil }
        if isExpired(entry) {
            try? FileManager.default.removeItem(at: file)
            return nil
        }
        memory[key] = entry
        return try? decoder.decode(T.self, from: entry.data)
    }

    public func set(key: String, value: some Codable & Sendable, ttl: TimeInterval? = nil) async {
        guard let data = try? encoder.encode(value) else { return }
        let expires = ttl.map { Date().addingTimeInterval($0) }
        let entry = Entry(data: data, expiresAt: expires)
        memory[key] = entry
        let file = diskURL.appendingPathComponent(safeKey(key))
        try? encoder.encode(entry).write(to: file, options: .atomic)
    }

    public func invalidate(key: String) async {
        memory.removeValue(forKey: key)
        try? FileManager.default.removeItem(
            at: diskURL.appendingPathComponent(safeKey(key))
        )
    }

    public func invalidateAll() async {
        memory = [:]
        try? FileManager.default.removeItem(at: diskURL)
        try? FileManager.default.createDirectory(at: diskURL, withIntermediateDirectories: true)
    }

    private func isExpired(_ entry: Entry) -> Bool {
        guard let expires = entry.expiresAt else { return false }
        return Date() > expires
    }

    private func safeKey(_ key: String) -> String {
        key.replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: ":", with: "_")
    }
}
