import Foundation

public extension SettingsViewModel {
    var dataRequestDownloadURL: URL? {
        guard
            let dataRequest,
            dataRequest.status == .ready,
            let downloadURL = dataRequest.downloadURL,
            dataRequest.expiresAt.map({ $0 > Date() }) ?? false,
            let url = URL(string: downloadURL),
            url.scheme != nil
        else {
            return nil
        }
        return url
    }

    var shouldPollDataRequest: Bool {
        guard let dataRequest else { return false }
        switch dataRequest.status {
        case .pending, .processing:
            return true
        case .ready:
            return dataRequestDownloadURL == nil
        case .failed, .expired:
            return false
        }
    }

    var dataRequestPollingTaskID: String? {
        shouldPollDataRequest ? dataRequest?.id : nil
    }
}

extension SettingsViewModel {
    func refreshDataRequest() async {
        guard let userIdOrSlug else { return }
        do {
            dataRequest = try await loadDataRequest(userIdOrSlug: userIdOrSlug)
        } catch {
            return
        }
    }

    func pollDataRequestIfNeeded(
        sleep: (UInt64) async throws -> Void = { try await Task.sleep(nanoseconds: $0) }
    ) async {
        guard shouldPollDataRequest else { return }

        while !Task.isCancelled, shouldPollDataRequest {
            do {
                try await sleep(5_000_000_000)
            } catch {
                return
            }
            guard !Task.isCancelled, shouldPollDataRequest else { return }
            await refreshDataRequest()
        }
    }
}
