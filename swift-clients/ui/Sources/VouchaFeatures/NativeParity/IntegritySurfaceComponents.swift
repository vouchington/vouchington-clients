import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct IntegrityAccessGate: View {
    let viewerTier: IntegrityViewerTier

    var body: some View {
        if viewerTier == .anonymous {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftIntegritySignInRequired),
                message: .message(.nativeSwiftIntegritySignInMessage)
            )
        } else {
            EmptyStateView(
                icon: "lock.shield",
                title: .message(.nativeSwiftIntegrityAdministratorRequired),
                message: .message(.nativeSwiftIntegrityAdministratorMessage)
            )
        }
    }
}

struct IntegrityStatusPicker: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let selectedStatus: IntegrityStatusFilter
    let onSelect: (IntegrityStatusFilter) -> Void

    var body: some View {
        Picker(UiMessages.string(.nativeSwiftIntegrityStatus, locale: nativeUiLocale), selection: Binding(
            get: { selectedStatus },
            set: onSelect
        )) {
            ForEach(IntegrityStatusFilter.allCases, id: \.self) { status in
                Text(UiMessages.string(status.titleKey, locale: nativeUiLocale)).tag(status)
            }
        }
        .pickerStyle(.segmented)
    }
}

struct IntegrityInitialState: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let isLoading: Bool
    let hasError: Bool
    let isEmpty: Bool
    let onRetry: () -> Void

    var body: some View {
        if isLoading, isEmpty {
            ProgressView(UiMessages.string(.nativeSwiftIntegrityLoading, locale: nativeUiLocale))
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if hasError, isEmpty {
            EmptyStateView(
                icon: "exclamationmark.triangle",
                title: .message(.nativeSwiftIntegrityLoadFailed),
                message: .message(.nativeSwiftIntegrityLoadFailed),
                actionTitle: .message(.nativeSwiftIntegrityRetry),
                action: onRetry
            )
        } else if isEmpty {
            EmptyStateView(
                icon: "checkmark.shield",
                title: .message(.nativeSwiftIntegrityNoFlags),
                message: .message(.nativeSwiftIntegrityNoFlagsMessage)
            )
        }
    }
}

struct IntegrityContinuationControls: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let hasMore: Bool
    let isLoadingMore: Bool
    let hasError: Bool
    let onLoadMore: () -> Void

    var body: some View {
        if hasError {
            Text(UiMessages.string(.nativeSwiftIntegrityLoadMoreFailed, locale: nativeUiLocale))
                .foregroundStyle(.red)
                .accessibilityIdentifier("integrity-continuation-error")
            Button(UiMessages.string(.nativeSwiftIntegrityRetryLoadingMore, locale: nativeUiLocale), action: onLoadMore)
        }
        if hasMore {
            Button(UiMessages.string(
                isLoadingMore ? .nativeSwiftIntegrityLoadingMore : .nativeSwiftIntegrityLoadMore,
                locale: nativeUiLocale
            ), action: onLoadMore)
                .disabled(isLoadingMore)
                .frame(maxWidth: .infinity)
        }
    }
}

extension IntegrityStatusFilter {
    var titleKey: UiMessageKey {
        switch self {
        case .pending: .nativeSwiftRouteSurfacePending
        case .resolved: .nativeSwiftIntegrityResolved
        case .all: .nativeSwiftIntegrityAll
        }
    }
}
