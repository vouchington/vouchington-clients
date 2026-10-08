import VouchaAPI
import VouchaCore

public extension NativeRouteSurfaceViewModel {
    func perform(action: NativeRouteSurfaceAction) async {
        guard let client, !isLoading else { return }

        state = .loading
        do {
            let loadedRows: [NativeRouteDestinationRow] = switch action {
            case .compareTopics:
                try await loadTopicCompareRows(client: client)
            case .compareHostnames:
                try await loadHostnameCompareRows(client: client)
            }
            rows = loadedRows
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }
}
