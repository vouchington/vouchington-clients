import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

enum NativeReferralLinksSurfaceMode: Hashable {
    case management
    case analytics
    case combined

    static func fromRoutePath(_ path: String?) -> Self {
        path == "/my/referrals" ? .analytics : .management
    }
}

struct NativeReferralLinksManagementView: View {
    var client: APIClient?
    let mode: NativeReferralLinksSurfaceMode
    @State
    private var viewModel: NativeReferralLinksManagementViewModel?
    @State
    private var searchQuery = ""
    @State
    private var selectedProgram: NativeReferralProgramChoice?
    @State
    private var referralURL = ""
    @State
    private var label = ""

    init(
        client: APIClient? = nil,
        viewModel: NativeReferralLinksManagementViewModel? = nil,
        mode: NativeReferralLinksSurfaceMode = .combined
    ) {
        self.client = client
        self.mode = mode
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        Group {
            if let viewModel {
                if viewModel.hasLoadedInitialContent {
                    content(viewModel)
                } else {
                    switch viewModel.state {
                    case .loading:
                        ProgressView()
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    case let .error(error):
                        ErrorStateView(error: error) {
                            await reload(viewModel)
                        }
                    default:
                        content(viewModel)
                    }
                }
            } else {
                EmptyStateView(
                    icon: "person.crop.circle.badge.exclamationmark",
                    title: .message(.nativeSwiftEmptyStateSignInRequired),
                    message: .message(.nativeSwiftEmptyStateSignInReferralLinksMessage)
                )
            }
        }
        .task(id: client.map { "\(ObjectIdentifier($0).hashValue)-\(mode)" }) {
            guard let client else {
                resetState()
                return
            }
            let nextViewModel = NativeReferralLinksManagementViewModel(client: client)
            viewModel = nextViewModel
            await load(nextViewModel)
        }
    }

    private func reload(_ viewModel: NativeReferralLinksManagementViewModel) async {
        await load(viewModel)
    }

    private func load(_ viewModel: NativeReferralLinksManagementViewModel) async {
        switch mode {
        case .management:
            await viewModel.loadLinks()
        case .analytics:
            await viewModel.loadClicks()
        case .combined:
            await viewModel.load()
        }
    }

    private func resetState() {
        viewModel = nil
        searchQuery = ""
        selectedProgram = nil
        referralURL = ""
        label = ""
    }

    private func content(_ viewModel: NativeReferralLinksManagementViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if mode != .analytics {
                NativeReferralLinkCreateForm(
                    viewModel: viewModel,
                    searchQuery: $searchQuery,
                    selectedProgram: $selectedProgram,
                    referralURL: $referralURL,
                    label: $label
                )
                NativeReferralLinksRows(viewModel: viewModel)
            }
            if mode != .management {
                NativeReferralClickRows(viewModel: viewModel)
            }
        }
    }
}
