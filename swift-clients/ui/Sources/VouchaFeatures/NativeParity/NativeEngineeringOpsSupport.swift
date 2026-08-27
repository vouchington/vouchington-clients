import Foundation
import VouchaCore
import VouchaLocalization

func engineeringActionErrorMessage(for error: Error) -> UiVerbatimText {
    if let error = error as? VouchaError {
        return error.errorDescription.map(UiVerbatimText.verbatim)
            ?? .message(.nativeSwiftEngineeringPostgresqlOperationFailed)
    }
    return .verbatim(error.localizedDescription)
}

@MainActor
protocol NativeEngineeringActionPerforming: AnyObject {
    var actionLoadingKeys: Set<String> { get set }
    var actionErrorMessage: UiVerbatimText? { get set }

    func load() async
}

extension NativeEngineeringActionPerforming {
    func performAction(key: String, operation: @escaping () async throws -> Void) async {
        guard !actionLoadingKeys.contains(key) else { return }
        actionLoadingKeys.insert(key)
        defer { actionLoadingKeys.remove(key) }

        do {
            try await operation()
            actionErrorMessage = nil
            await load()
        } catch {
            actionErrorMessage = engineeringActionErrorMessage(for: error)
        }
    }
}
