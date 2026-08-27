import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
public final class SettingsViewModel {
    static let siteDefaultUiLocale = "site_default"

    public internal(set) var state: LoadState = .idle
    public internal(set) var identity: PrivateUser?
    public internal(set) var profileLinks: [VouchaModels.ProfileLink] = []
    var apiKeyPagination = CursorPaginationState<ApiKey>()
    var pushSubscriptionPagination = CursorPaginationState<WebPushSubscription>()
    var sessionPagination = CursorPaginationState<AuthSession>()
    @ObservationIgnored
    var settingsLoadGeneration = 0
    @ObservationIgnored
    var revokedApiKeyIds: Set<String> = []
    @ObservationIgnored
    var revokedPushSubscriptionIds: Set<String> = []
    @ObservationIgnored
    var revokedSessionIds: Set<String> = []
    @ObservationIgnored
    var revokedAllSessions = false
    public var apiKeys: [ApiKey] {
        apiKeyPagination.items
    }

    public var pushSubscriptions: [WebPushSubscription] {
        pushSubscriptionPagination.items
    }

    public var sessions: [AuthSession] {
        sessionPagination.items
    }

    public internal(set) var membership: Membership?
    public internal(set) var membershipPlans: [String: [MembershipSkuSummary]] = [:]
    public internal(set) var membershipBenefitCatalog: MembershipBenefitCatalog?
    public internal(set) var dataRequest: UserDataRequest?
    public internal(set) var latestRawAPIKey: String?
    public internal(set) var membershipCheckoutURL: String?
    public internal(set) var membershipPortalURL: String?
    public internal(set) var statusMessage: UiVerbatimText?
    public internal(set) var localLLMStatusMessage: UiVerbatimText?
    public internal(set) var blueskyLinkState: NativeBlueskyLinkState = .idle
    public internal(set) var oauthProviderInFlight: NativeOAuthProvider?
    public internal(set) var oauthErrorMessage: UiVerbatimText?
    public internal(set) var blueskyLinkExpiresAt: Date?
    var savedUiLocale: String?

    public var username = ""
    public var displayNameSource: DisplayNameSource = .username
    public var profileImageId = ""
    public var profileMarkdown = ""
    public var followsVisibility: UserPrivacyAudience = .everyone
    public var topicFollowsVisibility: UserPrivacyAudience = .everyone
    public var rssFeedFollowsVisibility: UserPrivacyAudience = .everyone
    public var communityMembershipsVisibility: UserPrivacyAudience = .everyone
    public var followersVisibility: UserPrivacyAudience = .everyone
    public var likesVisibility: UserPrivacyAudience = .everyone
    public var cardsVisibility: UserPrivacyAudience = .everyone
    public var rewardsProgramStatusesVisibility: UserPrivacyAudience = .everyone
    public var spendingCategoriesVisibility: UserPrivacyAudience = .nobody
    public var directMessagesAudience: UserPrivacyAudience = .everyone
    public var uiLocale = SettingsViewModel.siteDefaultUiLocale
    public var defaultPostBroadcast = "everyone"
    public var defaultPostPrivacy = "public"
    public var processingRestrictedAt = false
    public var thirdPartyMarketing = false
    public var hnDiscussions = false
    public var blueskyHandle = ""

    public var profileLinkType: ProfileLinkType = .url
    public var profileLinkURL = ""
    public var profileLinkHandle = ""
    public var profileLinkName = ""
    public var profileLinkImageId = ""

    public var apiKeyLabel = ""
    public var apiKeyType: ApiKeyType = .rss
    public var deleteConfirmation = ""
    public var localLLMEnabled = false
    public internal(set) var localLLMConfiguration: LocalLLMConfiguration = .disabled
    public var localLLMDisplayName = ""
    public var localLLMEndpoint = ""
    public var localLLMModelsText = ""
    public var localLLMSelectedModel = ""
    public var localLLMAPIKey = ""
    public internal(set) var localLLMDraftEndpointID = UUID()

    let client: APIClient?
    let onLogoutRequired: @MainActor () -> Void
    let localLLMSettingsStore: LocalLLMSettingsStore
    let localLLMFeaturePolicy: LocalLLMFeaturePolicy
    let localLLMResponsesClient: OpenAICompatibleResponsesClient
    let blueskyLinkStore: NativeBlueskyLinkStore
    let nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator?
    let now: @Sendable () -> Date
    @ObservationIgnored
    private var blueskyResultObserver: NSObjectProtocol?
    var uiLocaleController: UiLocaleController?

    public init(
        client: APIClient?,
        uiLocaleController: UiLocaleController? = nil,
        onLogoutRequired: @escaping @MainActor () -> Void = {},
        nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator? = nil,
        localLLMSettingsStore: LocalLLMSettingsStore = LocalLLMSettingsStore(),
        localLLMFeaturePolicy: LocalLLMFeaturePolicy = LocalLLMFeaturePolicy(),
        localLLMResponsesClient: OpenAICompatibleResponsesClient = OpenAICompatibleResponsesClient(),
        blueskyLinkStore: NativeBlueskyLinkStore = NativeBlueskyLinkStore(),
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.client = client
        self.uiLocaleController = uiLocaleController
        self.onLogoutRequired = onLogoutRequired
        self.nativeOAuthAuthorizationCoordinator = nativeOAuthAuthorizationCoordinator
        self.localLLMSettingsStore = localLLMSettingsStore
        self.localLLMFeaturePolicy = localLLMFeaturePolicy
        self.localLLMResponsesClient = localLLMResponsesClient
        self.blueskyLinkStore = blueskyLinkStore
        self.now = now
        if let result = blueskyLinkStore.takeResult() {
            blueskyLinkState = Self.state(for: result)
        } else {
            do {
                switch try blueskyLinkStore.pendingStatus(now: now()) {
                case let .active(pending):
                    blueskyLinkExpiresAt = pending.expiresAt
                    blueskyLinkState = pending.isFinalizing ? .finalizing : .awaitingCallback
                case .expired:
                    blueskyLinkState = .expired
                case .none:
                    blueskyLinkState = .idle
                }
            } catch {
                blueskyLinkState = .error(.message(.nativeSwiftSettingsBlueskyLinkFailed))
            }
        }
        blueskyResultObserver = NotificationCenter.default.addObserver(
            forName: .blueskyLinkResultDidChange,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor [weak self] in
                await self?.consumeBlueskyLinkResult()
            }
        }
    }

    deinit {
        if let blueskyResultObserver {
            NotificationCenter.default.removeObserver(blueskyResultObserver)
        }
    }
}

public enum NativeBlueskyLinkState: Equatable, Sendable {
    case idle
    case linking
    case awaitingCallback
    case finalizing
    case disconnecting
    case cancelled
    case expired
    case success
    case error(UiVerbatimText)
}

extension SettingsViewModel {
    func localized(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        if let uiLocaleController {
            return uiLocaleController.string(key, parameters: parameters)
        }
        return UiMessages.string(UiMessage(key, parameters: parameters), locale: .english)
    }

    func localized(_ text: UiVerbatimText) -> String {
        if let uiLocaleController {
            return uiLocaleController.string(text)
        }
        return UiMessages.string(text, locale: .english)
    }
}
