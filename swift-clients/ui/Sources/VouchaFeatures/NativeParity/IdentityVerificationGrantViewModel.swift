import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class IdentityVerificationGrantViewModel {
    let client: APIClient?
    let idOrUsername: String
    let isAdministrator: Bool
    var note = ""
    var targetUserId: String?
    var isLoading = false
    var isSubmitting = false
    var loadError: UiVerbatimText?
    var submissionMessage: UiVerbatimText?
    var didGrant = false

    init(client: APIClient?, idOrUsername: String, isAdministrator: Bool) {
        self.client = client
        self.idOrUsername = idOrUsername
        self.isAdministrator = isAdministrator
    }

    var canSubmit: Bool {
        isAdministrator
            && targetUserId != nil
            && !note.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && note.trimmingCharacters(in: .whitespacesAndNewlines).count <= 2_000
            && !isSubmitting
            && !isLoading
    }

    func load() async {
        guard isAdministrator, let client, !idOrUsername.isEmpty else { return }
        isLoading = true
        loadError = nil
        defer { isLoading = false }
        do {
            let response: UserProfileResponse = try await client.send(.user(idOrSlug: idOrUsername))
            targetUserId = response.user.id
        } catch {
            targetUserId = nil
            loadError = .message(.nativeSwiftIdentityVerificationLoadFailure)
        }
    }

    func grant() async {
        let trimmed = note.trimmingCharacters(in: .whitespacesAndNewlines)
        guard isAdministrator, let client, let targetUserId else {
            submissionMessage = .message(.nativeSwiftIdentityVerificationValidation)
            return
        }
        guard !trimmed.isEmpty, trimmed.count <= 2_000 else {
            submissionMessage = .message(.nativeSwiftIdentityVerificationValidation)
            return
        }
        isSubmitting = true
        submissionMessage = nil
        defer { isSubmitting = false }
        do {
            let response: IdentityVerificationAttemptGrantResponse = try await client.send(
                .grantIdentityVerificationAttempt(userId: targetUserId, note: trimmed)
            )
            guard response.granted else {
                submissionMessage = .message(.nativeSwiftIdentityVerificationFailure)
                return
            }
            note = ""
            didGrant = true
            submissionMessage = .message(.nativeSwiftIdentityVerificationSuccess)
        } catch {
            submissionMessage = grantFailureMessage(for: error)
        }
    }

    private func grantFailureMessage(for error: Error) -> UiVerbatimText {
        guard let apiError = error as? VouchaError,
              case let .apiMessage(statusCode, _, message) = apiError,
              (400 ..< 500).contains(statusCode),
              !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else {
            return .message(.nativeSwiftIdentityVerificationFailure)
        }
        return .userContent(message)
    }
}
