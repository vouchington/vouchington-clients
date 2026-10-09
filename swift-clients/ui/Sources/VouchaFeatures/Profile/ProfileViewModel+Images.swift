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

        avatarUploadGeneration += 1
        let generation = avatarUploadGeneration
        avatarUploadErrorMessage = nil
        avatarPreviewData = nil
        avatarPreviewDecodeFailed = false
        isUploadingAvatar = true
        defer { isUploadingAvatar = false }

        do {
            let (data, contentType) = try await ImageSelectionLoader.load(from: url)
            guard generation == avatarUploadGeneration else { return }
            avatarPreviewData = LocalImagePreview.thumbnailData(from: data)
            avatarPreviewDecodeFailed = avatarPreviewData == nil
            try await applyAvatarUpload(
                data: data,
                contentType: contentType,
                service: imageUploadService,
                generation: generation
            )
            guard generation == avatarUploadGeneration else { return }
        } catch let error as ImageSelectionError {
            guard generation == avatarUploadGeneration else { return }
            avatarPreviewData = nil
            avatarPreviewDecodeFailed = false
            avatarUploadErrorMessage = .app(error.message)
        } catch {
            guard generation == avatarUploadGeneration else { return }
            avatarPreviewData = nil
            avatarPreviewDecodeFailed = false
            avatarUploadErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func removeAvatar() async {
        guard !isUploadingAvatar else { return }

        avatarUploadGeneration += 1
        let generation = avatarUploadGeneration
        avatarUploadErrorMessage = nil
        avatarPreviewData = nil
        avatarPreviewDecodeFailed = false
        isUploadingAvatar = true
        defer { isUploadingAvatar = false }

        do {
            let response: IdentityResponse = try await client.send(.updateIdentity(profileImageId: .null))
            guard generation == avatarUploadGeneration else { return }
            identity = response.identity.withProfileImageId(nil)
        } catch {
            guard generation == avatarUploadGeneration else { return }
            avatarUploadErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    private func applyAvatarUpload(
        data: Data,
        contentType: String,
        service: ImageUploadService,
        generation: Int
    ) async throws {
        let state = try await service.uploadImage(data: data, contentType: contentType)
        guard generation == avatarUploadGeneration else { return }
        guard state.ready || (state.uploadStatus == .complete && !state.blocked) else {
            avatarPreviewData = nil
            avatarPreviewDecodeFailed = false
            avatarUploadErrorMessage = state.blocked
                ? .app(UiMessage(.nativeSwiftImageSelectionBlocked))
                : state.uploadError.map { UiVerbatimText.verbatim($0) }
                ?? .app(UiMessage(.nativeSwiftImageSelectionUploadFailed))
            return
        }

        let response: IdentityResponse = try await client.send(.updateIdentity(profileImageId: .value(state.id)))
        guard generation == avatarUploadGeneration else { return }
        identity = response.identity
        avatarPreviewData = nil
        if avatarPreviewDecodeFailed {
            avatarUploadErrorMessage = .app(UiMessage(UiMessageKey.imagesUploadPreviewUnavailable))
        }
        avatarPreviewDecodeFailed = false
    }

}
