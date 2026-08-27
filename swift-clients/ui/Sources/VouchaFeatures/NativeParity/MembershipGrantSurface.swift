import SwiftUI
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct MembershipGrantSurface: View {
    @Environment(\.locale)
    private var locale
    @State
    private var viewModel: MembershipGrantViewModel

    init(client: APIClient?) {
        self.init(viewModel: MembershipGrantViewModel(client: client))
    }

    init(viewModel: MembershipGrantViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        @Bindable
        var viewModel = viewModel
        Form {
            Section {
                Text(verbatim: localized(.nativeSwiftMembershipMembershipGrantDescription))
            } header: {
                Text(verbatim: localized(.nativeSwiftMembershipMembershipGrants))
            }
            Section {
                TextField(localized(.nativeSwiftMembershipSearchUsers), text: $viewModel.query)
                    .onChange(of: viewModel.query) { _, value in viewModel.updateQuery(value) }
                Button(localized(.nativeSwiftMembershipSearch)) { Task { await viewModel.search() } }
                    .disabled(viewModel.query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || viewModel
                        .isSearching)
                ForEach(viewModel.candidates) { user in
                    Button { viewModel.selectUser(user) } label: {
                        Text(verbatim: UiMessages.string(.userContent(user.displayLabel), locale: locale))
                    }
                }
                if let user = viewModel.selectedUser {
                    LabeledContent(localized(.nativeSwiftMembershipSelectedUser)) {
                        Text(verbatim: UiMessages.string(.userContent(user.displayLabel), locale: locale))
                    }
                }
                if let error = viewModel.searchError {
                    Text(verbatim: UiMessages.string(error, locale: locale))
                }
            } header: { Text(verbatim: localized(.nativeSwiftMembershipUser)) }
            Section {
                Picker(localized(.nativeSwiftMembershipSelectPlan), selection: $viewModel.selectedPlan) {
                    Text(verbatim: localized(.nativeSwiftMembershipSelectPlan)).tag(MembershipPlanSlug?.none)
                    ForEach(MembershipPlanSlug.allCases) { plan in
                        Text(verbatim: localized(
                            plan == .plus ? .nativeSwiftMembershipPlus : .nativeSwiftMembershipPro
                        ))
                        .tag(Optional(plan))
                    }
                }
                .onChange(of: viewModel.selectedPlan) { _, plan in viewModel.selectPlan(plan) }
                Picker(localized(.nativeSwiftMembershipSelectSku), selection: $viewModel.selectedSkuId) {
                    Text(verbatim: localized(.nativeSwiftMembershipSelectSku)).tag(String?.none)
                    ForEach(viewModel.availableSkus) { sku in
                        Text(verbatim: UiMessages.string(skuLabel(sku), locale: locale)).tag(Optional(sku.id))
                    }
                }
                .disabled(viewModel.selectedPlan == nil)
                if viewModel.selectedPlan != nil, viewModel.availableSkus.isEmpty, !viewModel.isLoadingPlans {
                    Text(verbatim: localized(.nativeSwiftMembershipNoSkusForPlan))
                }
                if let error = viewModel.plansError {
                    Text(verbatim: UiMessages.string(error, locale: locale))
                    Button(localized(.nativeSwiftMembershipRetry)) { Task { await viewModel.loadPlans() } }
                }
            } header: { Text(verbatim: localized(.nativeSwiftMembershipSku)) }
            Section {
                Button(localized(viewModel
                        .isSubmitting ? .nativeSwiftMembershipSubmitting : .nativeSwiftMembershipMembershipGrant)) {
                    Task { await viewModel.grant() }
                }
                .disabled(!viewModel.canSubmit)
                if let message = viewModel
                    .submissionMessage {
                    Text(verbatim: UiMessages.string(message, locale: locale))
                }
            }
        }
        .task { await viewModel.loadPlans() }
        .navigationTitle(localized(.nativeSwiftMembershipMembershipGrant))
    }

    private func skuLabel(_ sku: MembershipSkuSummary) -> UiVerbatimText {
        let interval: UiVerbatimText = switch sku.interval {
        case "monthly": .message(.nativeSwiftMembershipMonth)
        case "yearly": .message(.nativeSwiftMembershipYear)
        default: .protocolValue(sku.interval)
        }
        let price: UiVerbatimText = if let amount = sku.price.knownCurrencyMajorUnitDecimal {
            .message(
                .nativeSwiftMembershipPerPrice,
                textParameters: ["interval": interval],
                currencyParameters: [
                    "price": .init(amount, code: sku.price.currency.uppercased())
                ]
            )
        } else {
            .message(.nativeSwiftMembershipPriceUnavailable)
        }
        return .message(
            .nativeSwiftMembershipSkuPriceSummary,
            textParameters: ["price": price, "sku": .protocolValue(sku.id)]
        )
    }

    private func localized(_ key: UiMessageKey) -> String {
        UiMessages.string(.message(key), locale: locale)
    }
}
