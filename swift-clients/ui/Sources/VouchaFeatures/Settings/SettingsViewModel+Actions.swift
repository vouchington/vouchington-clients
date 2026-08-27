import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public extension SettingsViewModel {
    func saveIdentity() async {
        guard let client, userIdOrSlug != nil else { return }
        await mutate {
            let response: SettingsIdentityResponse = try await client.send(
                .updateMyIdentity(
                    username: username.isEmpty ? nil : username,
                    useDisplayNameFrom: displayNameSource,
                    profileImageId: profileImageId.isEmpty ? nil : profileImageId
                )
            )
            apply(identity: response.identity)
            statusMessage = .message(.nativeSwiftSettingsIdentitySaved)
        }
    }

    func clearProfileImage() async {
        guard let client, let savedIdentity = identity else { return }
        await mutate {
            let response: SettingsIdentityResponse = try await client.send(
                .updateMyIdentity(
                    username: savedIdentity.username,
                    useDisplayNameFrom: savedIdentity.useDisplayNameFrom ?? .username,
                    clearProfileImage: true
                )
            )
            apply(identity: response.identity)
            statusMessage = .message(.nativeSwiftSettingsProfileImageCleared)
        }
    }

    func savePrivacy() async {
        guard let client, let userIdOrSlug else { return }
        await mutate {
            let response: SettingsUserResponse = try await client.send(
                .updateUser(
                    idOrSlug: userIdOrSlug,
                    cardsVisibility: cardsVisibility,
                    rewardsProgramStatusesVisibility: rewardsProgramStatusesVisibility,
                    spendingCategoriesVisibility: spendingCategoriesVisibility,
                    followsVisibility: followsVisibility,
                    topicFollowsVisibility: topicFollowsVisibility,
                    rssFeedFollowsVisibility: rssFeedFollowsVisibility,
                    communityMembershipsVisibility: communityMembershipsVisibility,
                    followersVisibility: followersVisibility,
                    likesVisibility: likesVisibility,
                    directMessagesAudience: directMessagesAudience,
                    defaultPostBroadcast: defaultPostBroadcast,
                    defaultPostPrivacy: defaultPostPrivacy,
                    processingRestrictedAt: processingRestrictedAt,
                    thirdPartyMarketing: thirdPartyMarketing,
                    hnDiscussions: hnDiscussions,
                    uiLocale: uiLocaleForPrivacySave(),
                    clearUiLocale: shouldClearUiLocaleForPrivacySave()
                )
            )
            apply(identity: response.user)
            statusMessage = .message(.nativeSwiftSettingsPrivacySaved)
        }
    }

    func saveProfile() async {
        guard let client else { return }
        await mutate {
            let response: SettingsProfileResponse = try await client.send(.updateProfile(markdown: profileMarkdown))
            profileMarkdown = response.profile.markdown
            statusMessage = .message(.nativeSwiftSettingsProfileSaved)
        }
    }

    func createProfileLink() async {
        guard let client else { return }
        await mutate {
            let response: SettingsProfileLinkResponse = try await client.send(
                .createMyProfileLink(
                    linkType: profileLinkType,
                    url: profileLinkURL.isEmpty ? nil : profileLinkURL,
                    handle: profileLinkHandle.isEmpty ? nil : profileLinkHandle,
                    name: profileLinkName.isEmpty ? nil : profileLinkName,
                    imageId: profileLinkImageId.isEmpty ? nil : profileLinkImageId
                )
            )
            profileLinks.append(response.profileLink)
            resetProfileLinkDraft()
            statusMessage = .message(.nativeSwiftSettingsProfileLinkAdded)
        }
    }

    func updateProfileLink(
        id: String,
        url: String?,
        handle: String?,
        name: String?,
        imageId: String?
    ) async {
        guard let client else { return }
        await mutate {
            let response: SettingsProfileLinkResponse = try await client.send(
                .updateMyProfileLink(id: id, url: url, handle: handle, name: name, imageId: imageId)
            )
            if let index = profileLinks.firstIndex(where: { $0.id == id }) {
                profileLinks[index] = response.profileLink
            }
            statusMessage = .message(.nativeSwiftSettingsProfileLinkUpdated)
        }
    }

    func deleteProfileLink(id: String) async {
        guard let client else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(.deleteMyProfileLink(id: id))
            profileLinks.removeAll { $0.id == id }
            statusMessage = .message(.nativeSwiftSettingsProfileLinkDeleted)
        }
    }

    func moveProfileLink(id: String, by offset: Int) {
        guard
            offset != 0,
            let index = profileLinks.firstIndex(where: { $0.id == id })
        else { return }
        let targetIndex = max(0, min(profileLinks.count - 1, index + offset))
        guard targetIndex != index else { return }
        let item = profileLinks.remove(at: index)
        profileLinks.insert(item, at: targetIndex)
    }

    func reorderProfileLinks() async {
        guard let client else { return }
        await mutate {
            let response: SettingsListResponse<VouchaModels.ProfileLink> = try await client.send(
                .reorderMyProfileLinks(ids: profileLinks.map(\.id))
            )
            profileLinks = response.results
            statusMessage = .message(.nativeSwiftSettingsProfileLinksReordered)
        }
    }

}

extension SettingsViewModel {
    func uiLocaleForPrivacySave() -> String? {
        guard uiLocale != Self.siteDefaultUiLocale else { return nil }
        return uiLocale == savedUiLocale ? nil : uiLocale
    }

    func shouldClearUiLocaleForPrivacySave() -> Bool {
        savedUiLocale != nil && uiLocale == Self.siteDefaultUiLocale
    }

    func mutate(_ body: () async throws -> Void) async {
        state = .loading
        statusMessage = nil
        do {
            try await body()
            state = .loaded
        } catch let error as VouchaError {
            statusMessage = error.errorDescription.map(UiVerbatimText.verbatim)
            state = .error(error)
        } catch {
            let unexpectedError = VouchaError.unexpected(error.localizedDescription)
            statusMessage = unexpectedError.errorDescription.map(UiVerbatimText.verbatim)
            state = .error(unexpectedError)
        }
    }
}
