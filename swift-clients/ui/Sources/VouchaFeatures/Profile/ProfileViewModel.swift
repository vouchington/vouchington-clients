import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels

struct IdentityResponse: Decodable {
    let identity: PrivateUser
}

private struct ProfileResponse: Decodable {
    let profile: Profile
}

@Observable
@MainActor
public final class ProfileViewModel {
    public internal(set) var identity: PrivateUser?
    public internal(set) var bio: String?
    public internal(set) var bioHtml: String?
    public internal(set) var isUploadingAvatar = false
    public internal(set) var avatarUploadErrorMessage: UiVerbatimText?
    public private(set) var state: LoadState = .idle

    /// The signed-in user's avatar URL, or `nil` if no image is set.
    public var avatarURL: String? {
        config.imageURL(forImageId: identity?.profileImageId, width: 144)
    }

    /// `true` when the profile has loaded successfully.
    public var isLoaded: Bool {
        if case .loaded = state {
            return true
        }
        return false
    }

    let client: APIClient
    let imageUploadService: ImageUploadService?
    private let config: AppConfig
    private let sessionManager: SessionManager
    /// Tracks the current load task so reload() can cancel an in-flight load before restarting.
    private var loadTask: Task<Void, Never>?

    public init(
        client: APIClient,
        config: AppConfig,
        sessionManager: SessionManager,
        imageUploadProtocolClasses: [AnyClass]? = nil
    ) {
        self.client = client
        imageUploadService = ImageUploadService(
            client: client,
            uploadProtocolClasses: imageUploadProtocolClasses
        )
        self.config = config
        self.sessionManager = sessionManager
    }

    /// Fetch identity and profile concurrently.
    /// Stores the created task in `loadTask` so `reload()` can cancel an in-flight load.
    public func load() async {
        guard case .idle = state else { return }
        state = .loading
        loadTask = Task { await self.performLoad() }
        await loadTask?.value
    }

    /// Reload identity and profile from scratch.
    /// Cancels any in-flight load task to prevent two concurrent fetches from racing.
    public func reload() async {
        loadTask?.cancel()
        identity = nil
        bio = nil
        bioHtml = nil
        state = .idle
        loadTask = Task { await self.performLoad() }
        await loadTask?.value
    }

    private func performLoad() async {
        do {
            // Fetch identity and profile concurrently; bio failure is non-fatal.
            async let identityFetch: IdentityResponse = client.send(.myIdentity)
            async let profileFetch: ProfileResponse = client.send(.myProfile)

            let identityResp = try await identityFetch
            let profileResp = try? await profileFetch
            // Cancelled after identity succeeded — profileFetch error is silently swallowed by
            // try?; return without touching state so a concurrent reload() result is not clobbered.
            guard !Task.isCancelled else { return }

            identity = identityResp.identity
            await setBio(profileResp?.profile.markdown)
            state = .loaded
        } catch is CancellationError {
            // Cancelled by reload(); state was already reset — do not overwrite it.
        } catch let error as VouchaError {
            if case let .network(urlError) = error, urlError.code == .cancelled {
                // URLSession cancelled by task cancellation; state already reset by reload().
            } else if !Task.isCancelled {
                state = .error(error)
            }
        } catch {
            if !Task.isCancelled {
                state = .error(.api(statusCode: 0, preconditionCode: nil))
            }
        }
    }

    /// Sign out the current user.
    public func signOut() async {
        await sessionManager.signOut()
    }

    /// Update the current user's bio markdown.
    ///
    /// Updates `bio` locally on success.
    public func updateBio(_ markdown: String) async throws {
        let response: ProfileResponse = try await client.send(.updateProfile(markdown: markdown))
        await setBio(response.profile.markdown)
    }

    private func setBio(_ markdown: String?) async {
        guard let markdown, !markdown.isEmpty else {
            bio = nil
            bioHtml = nil
            return
        }
        bio = markdown
        let preview: MarkdownPreviewResponse? = try? await client.send(.markdownPreview(markdown: markdown))
        bioHtml = preview?.html
    }
}
