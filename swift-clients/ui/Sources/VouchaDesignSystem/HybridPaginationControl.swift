import SwiftUI
import VouchaLocalization

/// A visible pagination fallback that continues automatically while it remains in the viewport.
public struct HybridPaginationControl: View {
    @Environment(\.locale)
    private var locale
    private let hasMore: Bool
    private let isLoading: Bool
    private let hasError: Bool
    private let isDisabled: Bool
    private let accessibilityIdentifier: String
    private let loadNextPage: () async -> Void
    @State
    private var automaticVisibility = AutomaticPaginationVisibilityState()

    public init(
        hasMore: Bool,
        isLoading: Bool,
        hasError: Bool,
        isDisabled: Bool = false,
        accessibilityIdentifier: String = "hybrid-pagination-control",
        loadNextPage: @escaping () async -> Void
    ) {
        self.hasMore = hasMore
        self.isLoading = isLoading
        self.hasError = hasError
        self.isDisabled = isDisabled
        self.accessibilityIdentifier = accessibilityIdentifier
        self.loadNextPage = loadNextPage
    }

    public var body: some View {
        if hasMore || hasError {
            Button {
                Task { await loadNextPage() }
            } label: {
                HStack(spacing: Spacing.sm) {
                    if isLoading {
                        ProgressView()
                            .controlSize(.small)
                    }
                    Text(UiMessages.string(labelKey, locale: locale))
                }
                .frame(maxWidth: .infinity, minHeight: 44)
            }
            .buttonStyle(.plain)
            .disabled(isLoading || isDisabled)
            .onGeometryChange(for: Bool.self) { geometry in
                let localBounds = CGRect(origin: .zero, size: geometry.size)
                guard let scrollViewport = geometry.bounds(of: .scrollView(axis: .vertical)) else {
                    return false
                }
                return localBounds.intersects(scrollViewport)
            } action: { isVisible in
                guard automaticVisibility.shouldLoad(
                    isVisible: isVisible,
                    hasMore: hasMore,
                    isLoading: isLoading,
                    hasError: hasError,
                    isDisabled: isDisabled
                ) else { return }
                Task { await loadNextPage() }
            }
            .onChange(of: isLoading) { _, _ in
                guard automaticVisibility.shouldLoad(
                    hasMore: hasMore,
                    isLoading: isLoading,
                    hasError: hasError,
                    isDisabled: isDisabled
                ) else { return }
                Task { await loadNextPage() }
            }
            .accessibilityIdentifier(accessibilityIdentifier)
        }
    }

    private var labelKey: UiMessageKey {
        if isLoading {
            return .nativeSwiftCommonLoadingMore
        }
        return hasError ? .nativeCommonRetry : .nativeSwiftCommonLoadMore
    }

}

struct AutomaticPaginationVisibilityState {
    private var isVisible = false
    private var wasLoading = false

    mutating func shouldLoad(
        isVisible nextIsVisible: Bool? = nil,
        hasMore: Bool,
        isLoading: Bool,
        hasError: Bool,
        isDisabled: Bool = false
    ) -> Bool {
        let nextIsVisible = nextIsVisible ?? isVisible
        let enteredViewport = nextIsVisible && !isVisible
        let finishedLoading = wasLoading && !isLoading
        isVisible = nextIsVisible
        wasLoading = isLoading
        return isVisible && (enteredViewport || finishedLoading)
            && hasMore && !isLoading && !hasError && !isDisabled
    }
}
