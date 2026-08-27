import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension ProfileViewModel {
    func uploadAvatar(from url: URL) async {
        guard let imageUploadService else {
            avatarUploadErrorMessage = .app(UiMessage(.nativeSwiftImageSelectionRequiresSignedInSession))
            return
        }
        guard !isUploadingAvatar else { return }

        avatarUploadErrorMessage = nil
        isUploadingAvatar = true
        defer { isUploadingAvatar = false }

        do {
            let (data, contentType) = try await ImageSelectionLoader.load(from: url)
            try await applyAvatarUpload(data: data, contentType: contentType, service: imageUploadService)
        } catch let error as ImageSelectionError {
            avatarUploadErrorMessage = .app(error.message)
        } catch {
            avatarUploadErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func removeAvatar() async {
        guard !isUploadingAvatar else { return }

        avatarUploadErrorMessage = nil
        isUploadingAvatar = true
        defer { isUploadingAvatar = false }

        do {
            let response: IdentityResponse = try await client.send(.updateIdentity(profileImageId: .null))
            identity = response.identity.withProfileImageId(nil)
        } catch {
            avatarUploadErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    private func applyAvatarUpload(data: Data, contentType: String, service: ImageUploadService) async throws {
        let state = try await service.uploadImage(data: data, contentType: contentType)
        guard state.ready || (state.uploadStatus == .complete && !state.blocked) else {
            avatarUploadErrorMessage = state.blocked
                ? .app(UiMessage(.nativeSwiftImageSelectionBlocked))
                : state.uploadError.map { UiVerbatimText.verbatim($0) }
                ?? .app(UiMessage(.nativeSwiftImageSelectionUploadFailed))
            return
        }

        let response: IdentityResponse = try await client.send(.updateIdentity(profileImageId: .value(state.id)))
        identity = response.identity.withProfileImageId(state.id)
    }

}
