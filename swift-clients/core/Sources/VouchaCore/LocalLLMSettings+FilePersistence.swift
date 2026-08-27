import Foundation

enum LocalLLMConfigurationReadResult {
    case missing
    case data(Data)
    case failed

    var isMissing: Bool {
        if case .missing = self {
            return true
        }
        return false
    }
}

extension LocalLLMSettingsStore {
    func write(_ configuration: LocalLLMConfiguration) -> Bool {
        configurationWriter(fileURL, configuration)
    }

    static func write(_ configuration: LocalLLMConfiguration, to fileURL: URL) -> Bool {
        do {
            try FileManager.default.createDirectory(
                at: fileURL.deletingLastPathComponent(),
                withIntermediateDirectories: true
            )
            let data = try JSONEncoder().encode(configuration)
            try data.write(to: fileURL, options: .atomic)
            return true
        } catch {
            return false
        }
    }

    static func remove(at fileURL: URL) -> Bool {
        do {
            if FileManager.default.fileExists(atPath: fileURL.path) {
                try FileManager.default.removeItem(at: fileURL)
            }
            return true
        } catch {
            return false
        }
    }

    static func read(from fileURL: URL) -> LocalLLMConfigurationReadResult {
        guard FileManager.default.fileExists(atPath: fileURL.path) else { return .missing }
        do {
            let data = try Data(contentsOf: fileURL)
            return .data(data)
        } catch {
            return .failed
        }
    }
}
