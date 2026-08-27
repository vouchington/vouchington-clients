import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension StaffSupportSurface {
    func staffSupportSidebar(
        _ viewModel: StaffSupportViewModel,
        revealDetail: @escaping () -> Void = {}
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            staffSupportSearch(viewModel)
            if viewModel.mode == .threads {
                Picker(
                    UiMessages.string(.nativeSwiftSupportStatus, locale: nativeUiLocale),
                    selection: Binding(get: { viewModel.status }, set: { viewModel.status = $0 })
                ) {
                    Text(UiMessages.string(.nativeSwiftCommonAll, locale: nativeUiLocale))
                        .tag(StaffSupportThreadStatusFilter?.none)
                    ForEach([StaffSupportThreadStatusFilter.open, .assigned, .resolved], id: \.self) { status in
                        Text(UiMessages.string(staffSupportThreadStatusFilterText(status), locale: nativeUiLocale))
                            .tag(Optional(status))
                    }
                }
                .onChange(of: viewModel.status) { _, _ in Task { await viewModel.reloadList() } }
            }
            ScrollView {
                LazyVStack(alignment: .leading, spacing: Spacing.xs) {
                    staffSupportRows(viewModel, revealDetail: revealDetail)
                    if canLoadMoreList(viewModel) {
                        Button {
                            Task { await viewModel.loadMoreList() }
                        } label: {
                            Text(UiMessages.string(.nativeSwiftCommonLoadMore, locale: nativeUiLocale))
                        }
                        .buttonStyle(.bordered)
                    }
                }
            }
            if let error = viewModel.errorMessage {
                Text(UiMessages.string(error, locale: nativeUiLocale))
                    .foregroundStyle(Colors.negativeVote)
                Button {
                    Task {
                        if await viewModel.retry() {
                            revealDetail()
                        }
                    }
                } label: {
                    Text(UiMessages.string(.nativeSwiftCommonTryAgain, locale: nativeUiLocale))
                }
            }
        }
    }

    private func staffSupportSearch(_ viewModel: StaffSupportViewModel) -> some View {
        HStack {
            TextField(
                UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale),
                text: Binding(get: { viewModel.query }, set: { viewModel.query = $0 })
            )
            Button {
                Task { await viewModel.reloadList() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale),
                    systemImage: "magnifyingglass"
                )
            }
            .buttonStyle(.bordered)
        }
    }

    @ViewBuilder
    private func staffSupportRows(
        _ viewModel: StaffSupportViewModel,
        revealDetail: @escaping () -> Void
    ) -> some View {
        switch viewModel.mode {
        case .threads:
            ForEach(viewModel.threads) { thread in
                Button {
                    Task {
                        if await viewModel.selectThread(thread.id).didLoad {
                            revealDetail()
                        }
                    }
                } label: {
                    VStack(alignment: .leading) {
                        Text(thread.subject)
                        Text(UiMessages.string(supportThreadStatusText(thread.status), locale: nativeUiLocale))
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .buttonStyle(.plain)
            }
        case .contacts:
            ForEach(viewModel.contacts) { contact in
                Button {
                    Task {
                        if await viewModel.selectContact(contact.id) {
                            revealDetail()
                        }
                    }
                } label: {
                    VStack(alignment: .leading) {
                        Text(contact.emailAddress)
                        if !contact.name.isEmpty {
                            Text(contact.name).font(Typography.caption)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .buttonStyle(.plain)
            }
        }
    }

    private func canLoadMoreList(_ viewModel: StaffSupportViewModel) -> Bool {
        viewModel.canLoadMoreList
    }
}
