import SwiftUI
import VouchaLocalization
import VouchaModels

struct PaymentCardRow: View {
    @Environment(\.locale)
    private var locale
    let card: PaymentCard
    let viewModel: PaymentCardsViewModel
    @State
    private var editorState: PaymentCardRowEditorState
    @State
    private var confirmsDeletion = false

    init(
        card: PaymentCard,
        viewModel: PaymentCardsViewModel,
        editorState: PaymentCardRowEditorState? = nil
    ) {
        self.card = card
        self.viewModel = viewModel
        _editorState = State(initialValue: editorState ?? PaymentCardRowEditorState(card: card))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(verbatim: UiMessages.string(.userContent(card.card.name), locale: locale))
                .font(.headline)
            if let openedOn = card.openedOn {
                detail(.nativeSwiftHouseholdsBookmarksPaymentCardsOpenedOn, value: formatted(openedOn))
            }
            if let closedOn = card.closedOn {
                detail(.nativeSwiftHouseholdsBookmarksPaymentCardsClosedOn, value: formatted(closedOn))
            }
            if let bonusOn = card.receivedSignUpBonusOn {
                detail(.nativeSwiftHouseholdsBookmarksPaymentCardsSignUpBonusOn, value: formatted(bonusOn))
            }
            if let creditLimit = card.creditLimit {
                detail(
                    .nativeSwiftHouseholdsBookmarksPaymentCardsCreditLimit,
                    value: formattedCreditLimit(creditLimit)
                )
            }
            if let parentName = card.authorizedUserOfCard?.card.name {
                detail(.nativeSwiftHouseholdsBookmarksPaymentCardsAuthorizedUserOf, value: parentName)
            } else if card.isAuthorizedUser {
                Text(UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsAuthorizedUser, locale: locale))
            }
            if let note = card.note {
                detail(.nativeSwiftHouseholdsBookmarksPaymentCardsNote, value: note)
            }
            if editorState.isEditing {
                PaymentCardEditor(
                    draft: $editorState.draft,
                    parentChoices: viewModel.parentChoices(
                        for: card,
                        selectedParentId: editorState.draft.authorizedUserOfId
                    ),
                    canLoadMoreParents: viewModel.hasNextPage,
                    isSaving: viewModel.mutatingIds.contains(card.id),
                    loadMoreParents: { Task { await viewModel.loadMore() } },
                    save: {
                        Task {
                            if await viewModel.save(card: card, draft: editorState.draft) {
                                editorState.isEditing = false
                            }
                        }
                    },
                    cancel: {
                        editorState.draft = PaymentCardDraft(card: card, locale: locale)
                        editorState.isEditing = false
                    }
                )
            } else {
                HStack {
                    Button(UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsEdit, locale: locale)) {
                        editorState.draft = PaymentCardDraft(card: card, locale: locale)
                        editorState.isEditing = true
                    }
                    Button(
                        UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsRemove, locale: locale),
                        role: .destructive
                    ) { confirmsDeletion = true }
                        .disabled(viewModel.mutatingIds.contains(card.id))
                }
            }
        }
        .padding(.vertical, 8)
        .confirmationDialog(
            UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsRemoveConfirmation, locale: locale),
            isPresented: $confirmsDeletion
        ) {
            Button(
                UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsConfirm, locale: locale),
                role: .destructive
            ) { Task { _ = await viewModel.delete(card) } }
            Button(
                UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsCancel, locale: locale),
                role: .cancel
            ) { confirmsDeletion = false }
        }
        .onChange(of: card.authorizedUserOfId) { previousParentId, currentParentId in
            guard editorState.isEditing else { return }
            editorState.draft.synchronizeAuthorizedUserParent(
                from: previousParentId, to: currentParentId
            )
        }
        .onChange(of: viewModel.cards.map(\.id)) { _, _ in
            guard editorState.isEditing else { return }
            viewModel.reconcileDeletedParentSelection(in: &editorState.draft)
        }
    }

    private func detail(_ key: UiMessageKey, value: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 4) {
            Text(UiMessages.string(key, locale: locale) + ":")
            Text(verbatim: UiMessages.string(.userContent(value), locale: locale))
        }
    }

    private func formatted(_ value: LocalDate) -> String {
        UiMessages.date(
            value.utcGregorianDate ?? Date(),
            date: .abbreviated,
            locale: locale,
            timeZone: TimeZone(secondsFromGMT: 0) ?? .current
        )
    }

    private func formattedCreditLimit(_ value: Money) -> String {
        guard let majorUnits = value.knownCurrencyMajorUnitDecimal else { return "" }
        return UiMessages.currency(
            majorUnits,
            code: value.currency.uppercased(),
            locale: locale
        )
    }
}

private extension LocalDate {
    var utcGregorianDate: Date? {
        var calendar = Calendar(identifier: .gregorian)
        guard let utc = TimeZone(secondsFromGMT: 0) else { return nil }
        calendar.timeZone = utc
        return calendar.date(from: DateComponents(year: year, month: month, day: day))
    }
}
