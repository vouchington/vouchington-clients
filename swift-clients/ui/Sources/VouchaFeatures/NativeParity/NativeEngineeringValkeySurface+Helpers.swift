import SwiftUI
import VouchaAPI
import VouchaLocalization

extension NativeEngineeringValkeySurface {
    func confirmMessage(for action: NativeEngineeringValkeyPendingAction?) -> UiMessage? {
        switch action {
        case let .clearGroup(group):
            UiMessage(.nativeSwiftEngineeringValkeyClearGroupConfirmation, parameters: ["group": group])
        case .clearAll:
            UiMessage(.nativeSwiftEngineeringValkeyClearAllConfirmation)
        case .none:
            nil
        }
    }
}

extension EngineeringValkeyBloomFilterTarget {
    var uiMessage: UiMessage {
        switch self {
        case .urlBlocklist: UiMessage(.nativeSwiftEngineeringValkeyUrlBlocklist)
        case .emailBlocklist: UiMessage(.nativeSwiftEngineeringValkeyEmailBlocklist)
        case .embedding: UiMessage(.nativeSwiftEngineeringValkeyEmbedding)
        case .entityCache: UiMessage(.nativeSwiftEngineeringValkeyEntityCache)
        case .apiKeys: UiMessage(.nativeSwiftSettingsApiKeys)
        }
    }
}

struct ValkeyRebuildConfirmationModifier: ViewModifier {
    @Environment(\.locale)
    var nativeUiLocale
    @Binding
    var pendingFilter: EngineeringValkeyBloomFilterTarget?
    let viewModel: NativeEngineeringValkeyViewModel

    func body(content: Content) -> some View {
        content.confirmationDialog(
            UiMessages.string(.nativeSwiftEngineeringValkeyRebuildBloomFilterConfirmationTitle, locale: nativeUiLocale),
            isPresented: Binding(
                get: { pendingFilter != nil },
                set: {
                    if !$0 {
                        pendingFilter = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(.nativeSwiftEngineeringValkeyRebuild, locale: nativeUiLocale),
                role: .destructive
            ) {
                let filter = pendingFilter
                pendingFilter = nil
                guard let filter else { return }
                Task { await viewModel.rebuild(filter: filter) }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                pendingFilter = nil
            }
        } message: {
            if let message = rebuildMessage(for: pendingFilter) {
                Text(UiMessages.string(message, locale: nativeUiLocale))
            }
        }
    }

    private func rebuildMessage(for filter: EngineeringValkeyBloomFilterTarget?) -> UiMessage? {
        guard let filter else { return nil }
        return UiMessage(
            .nativeSwiftEngineeringValkeyRebuildFilterConfirmation,
            textParameters: ["filter": .app(filter.uiMessage)]
        )
    }
}

struct ValkeyFlushConfirmationModifier: ViewModifier {
    @Environment(\.locale)
    var nativeUiLocale
    @Binding var pendingConcern: EngineeringValkeyFlushConcern?
    let viewModel: NativeEngineeringValkeyViewModel

    func body(content: Content) -> some View {
        content.confirmationDialog(
            UiMessages.string(flushTitle(for: pendingConcern), locale: nativeUiLocale),
            isPresented: Binding(
                get: { pendingConcern != nil },
                set: {
                    if !$0 {
                        pendingConcern = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(flushConfirmTitle(for: pendingConcern), locale: nativeUiLocale),
                role: .destructive
            ) {
                let concern = pendingConcern
                pendingConcern = nil
                guard let concern else { return }
                Task { await viewModel.flush(concern: concern) }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                pendingConcern = nil
            }
        } message: {
            if let message = flushMessage(for: pendingConcern) {
                Text(UiMessages.string(message, locale: nativeUiLocale))
            }
        }
    }

    private func flushTitle(for concern: EngineeringValkeyFlushConcern?) -> UiMessage {
        guard let concern else {
            return UiMessage(.nativeSwiftEngineeringValkeyFlushKeysConfirmationTitle)
        }
        return UiMessage(
            .nativeSwiftEngineeringValkeyFlushConcernConfirmationTitle,
            parameters: ["label": concern.displayName]
        )
    }

    private func flushConfirmTitle(for concern: EngineeringValkeyFlushConcern?) -> UiMessage {
        concern == .sessions
            ? UiMessage(.nativeSwiftEngineeringValkeyForceLogoutConfirm)
            : UiMessage(.nativeSwiftEngineeringValkeyFlush)
    }

    /// Wording mirrors `FLUSH_CONCERN_DESCRIPTIONS` in web/app/admin/valkey/valkey-state.ts for cross-platform parity.
    private func flushMessage(for concern: EngineeringValkeyFlushConcern?) -> UiMessage? {
        guard let concern else { return nil }
        switch concern {
        case .caches:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushCachesDescription)
        case .recentlyViewed:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushRecentlyViewedDescription)
        case .blooms:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushBloomsDescription)
        case .rateLimiter:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushRateLimiterDescription)
        case .dynamicConfig:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushDynamicConfigDescription)
        case .sessions:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushSessionsDescription)
        case .queues:
            return UiMessage(.nativeSwiftEngineeringValkeyFlushQueuesDescription)
        }
    }
}

struct ValkeyActionErrorModifier: ViewModifier {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: NativeEngineeringValkeyViewModel

    func body(content: Content) -> some View {
        content.alert(
            UiMessages.string(.nativeSwiftEngineeringPostgresqlOperationFailed, locale: nativeUiLocale),
            isPresented: errorBinding
        ) {
            Button(UiMessages.string(.nativeSwiftCommonOK, locale: nativeUiLocale), role: .cancel) {}
        } message: {
            Text(UiMessages.string(
                viewModel.actionErrorMessage ?? .verbatim(""),
                locale: nativeUiLocale
            ))
        }
    }

    private var errorBinding: Binding<Bool> {
        Binding(
            get: { viewModel.actionErrorMessage != nil },
            set: {
                if !$0 {
                    viewModel.actionErrorMessage = nil
                }
            }
        )
    }
}
