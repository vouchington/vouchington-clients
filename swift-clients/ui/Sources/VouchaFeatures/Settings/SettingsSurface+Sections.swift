import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension SettingsSurface {
    private var uiLocaleController: UiLocaleController {
        viewModel.uiLocaleController ?? UiLocaleController(savedUiLocale: viewModel.savedUiLocale)
    }

    var statusBanner: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            if let message = viewModel.statusMessage {
                Text(verbatim: UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            if let rawKey = viewModel.latestRawAPIKey {
                Text(UiMessages.string(.nativeSwiftSettingsApiKeyShownOnce, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                Text(rawKey)
                    .font(Typography.caption.monospaced())
                    .textSelection(.enabled)
                    .accessibilityHidden(false)
                Button(UiMessages.string(.nativeCommonDone, locale: nativeUiLocale)) {
                    viewModel.dismissRawApiKey()
                }
                .accessibilityIdentifier("dismiss-api-key-secret")
            }
        }
    }

    var identitySection: some View {
        section(.nativeSwiftSettingsAccount, systemImage: "person.crop.circle") {
            HStack(alignment: .top, spacing: Spacing.md) {
                Avatar(
                    imageURL: viewModel.identity?.profileImageId == viewModel.profileImageId.trimmed
                        ? imageURL(forPlacement: viewModel.identity?.profileImagePlacement)
                        : nil,
                    username: viewModel.username,
                    size: 56
                )
                .accessibilityHidden(true)

                VStack(alignment: .leading, spacing: Spacing.sm) {
                    TextField(
                        UiMessages.string(.nativeSwiftSettingsUsername, locale: nativeUiLocale),
                        text: $viewModel.username
                    )
                    .textFieldStyle(.roundedBorder)
                    Picker(
                        UiMessages.string(.nativeSwiftSettingsDisplayName, locale: nativeUiLocale),
                        selection: $viewModel.displayNameSource
                    ) {
                        ForEach(DisplayNameSource.allCases, id: \.self) { source in
                            Text(UiMessages.string(source.titleKey, locale: nativeUiLocale))
                                .tag(source)
                        }
                    }
                    .pickerStyle(.menu)
                    TextField(
                        UiMessages.string(.nativeSwiftSettingsProfileImageId, locale: nativeUiLocale),
                        text: $viewModel.profileImageId
                    )
                    .textFieldStyle(.roundedBorder)
                    HStack {
                        Button(UiMessages.string(.nativeSwiftSettingsSaveIdentity, locale: nativeUiLocale)) {
                            Task { await viewModel.saveIdentity() }
                        }
                        .buttonStyle(.borderedProminent)

                        Button(UiMessages.string(.nativeSwiftSettingsClearAvatar, locale: nativeUiLocale)) {
                            Task { await viewModel.clearProfileImage() }
                        }
                        .buttonStyle(.bordered)
                    }
                    .disabled(viewModel.isLoading)
                }
            }
        }
    }

    func imageURL(forPlacement placement: ImagePlacement?) -> String? {
        AppConfig(baseURL: AppConfig.shared.baseURL, imageBaseURL: imageBaseURL).imageURL(for: placement)
    }

    var privacySection: some View {
        section(.nativeSwiftSettingsPrivacy, systemImage: "lock") {
            privacyPicker(.nativeSwiftSettingsFollows, selection: $viewModel.followsVisibility)
            privacyPicker(.nativeSwiftSettingsTopicFollows, selection: $viewModel.topicFollowsVisibility)
            privacyPicker(.nativeSwiftSettingsRssFollows, selection: $viewModel.rssFeedFollowsVisibility)
            privacyPicker(
                .nativeSwiftSettingsCommunityMemberships,
                selection: $viewModel.communityMembershipsVisibility
            )
            privacyPicker(.nativeSwiftSettingsFollowers, selection: $viewModel.followersVisibility)
            privacyPicker(.nativeSwiftSettingsLikes, selection: $viewModel.likesVisibility)
            privacyPicker(.nativeSwiftSettingsCards, selection: $viewModel.cardsVisibility)
            privacyPicker(.nativeSwiftSettingsRewardStatuses, selection: $viewModel.rewardsProgramStatusesVisibility)
            privacyPicker(.nativeSwiftSettingsSpendingCategories, selection: $viewModel.spendingCategoriesVisibility)
            privacyPicker(.nativeSwiftSettingsDirectMessages, selection: $viewModel.directMessagesAudience)
            Picker(uiLocaleController.string(.settingsLanguageInterfaceLabel), selection: $viewModel.uiLocale) {
                Text(uiLocaleController.string(.settingsLanguageUseSiteDefault))
                    .tag(SettingsViewModel.siteDefaultUiLocale)
                Text(uiLocaleController.string(.nativeLanguageEnglish)).tag("en")
                Text(uiLocaleController.string(.nativeLanguageSpanish)).tag("es")
                Text(uiLocaleController.string(.nativeLanguageFrench)).tag("fr")
                Text(uiLocaleController.string(.nativeLanguagePortuguese)).tag("pt")
            }
            .pickerStyle(.menu)

            Picker(
                UiMessages.string(.nativeSwiftSettingsDefaultPostAudience, locale: nativeUiLocale),
                selection: $viewModel.defaultPostBroadcast
            ) {
                Text(UiMessages.string(.nativeSwiftSettingsEveryone, locale: nativeUiLocale)).tag("everyone")
                Text(UiMessages.string(.nativeSwiftSettingsUsers, locale: nativeUiLocale)).tag("users")
                Text(UiMessages.string(.nativeSwiftSettingsFollowers, locale: nativeUiLocale)).tag("followers")
                Text(UiMessages.string(.nativeSwiftSettingsMutualFollowers, locale: nativeUiLocale))
                    .tag("mutual_followers")
            }
            .pickerStyle(.menu)

            Picker(
                UiMessages.string(.nativeSwiftSettingsDefaultPostPrivacy, locale: nativeUiLocale),
                selection: $viewModel.defaultPostPrivacy
            ) {
                Text(UiMessages.string(.nativeSwiftSettingsPublic, locale: nativeUiLocale)).tag("public")
                Text(UiMessages.string(.nativeSwiftSettingsPrivate, locale: nativeUiLocale)).tag("private")
            }
            .pickerStyle(.menu)

            Toggle(
                UiMessages.string(.nativeSwiftSettingsProcessingRestricted, locale: nativeUiLocale),
                isOn: $viewModel.processingRestrictedAt
            )
            Toggle(
                UiMessages.string(.nativeSwiftSettingsThirdPartyMarketing, locale: nativeUiLocale),
                isOn: $viewModel.thirdPartyMarketing
            )
            Toggle(
                UiMessages.string(
                    .extractedMyPreferencesFormHackerNewsDiscussions065664a4,
                    locale: nativeUiLocale
                ),
                isOn: $viewModel.hnDiscussions
            )
            Text(
                UiMessages.string(
                    .extractedMyPreferencesFormShowRelatedHackerNewsThreadsWhen00754911,
                    locale: nativeUiLocale
                )
            )
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)

            Button(UiMessages.string(.nativeSwiftSettingsSavePrivacy, locale: nativeUiLocale)) {
                Task { await viewModel.savePrivacy() }
            }
            .buttonStyle(.borderedProminent)
            .disabled(viewModel.isLoading)
        }
    }

}
