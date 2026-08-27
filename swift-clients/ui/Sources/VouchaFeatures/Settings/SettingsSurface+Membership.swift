import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    var membershipSection: some View {
        section(.nativeSwiftMembershipMembership, systemImage: "creditcard") {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(viewModel.membershipSummaryText)
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)

                LazyVGrid(
                    columns: [GridItem(.adaptive(minimum: 220), spacing: Spacing.md, alignment: .top)],
                    alignment: .leading,
                    spacing: Spacing.md
                ) {
                    ForEach(viewModel.membershipPlanPresentations) { plan in
                        membershipPlanCard(plan)
                    }
                }
            }
        }
    }

    private func membershipPlanCard(_ plan: MembershipPlanPresentation) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            membershipPlanHeader(plan)

            Text(plan.priceSummaryText)
                .font(Typography.subheadline)
                .fontWeight(.semibold)

            membershipPriceOptions(plan.priceOptions)
            membershipFeatureBullets(plan.featureBullets)
            membershipPendingAction(plan)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.md)
        .background(Colors.background)
        .overlay(
            RoundedRectangle(cornerRadius: Spacing.sm, style: .continuous)
                .strokeBorder(Colors.separator, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: Spacing.sm, style: .continuous))
    }

    private func membershipPlanHeader(_ plan: MembershipPlanPresentation) -> some View {
        HStack(alignment: .top, spacing: Spacing.sm) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(plan.title)
                    .font(Typography.headline)
                if let currentStateText = plan.currentStateText {
                    Text(currentStateText)
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }

            Spacer(minLength: 0)

            if plan.isCurrent {
                Text(UiMessages.string(.nativeSwiftCommonCurrent, locale: nativeUiLocale))
                    .font(Typography.caption2)
                    .padding(.horizontal, Spacing.xs)
                    .padding(.vertical, 2)
                    .background(Colors.primary.opacity(0.12))
                    .foregroundStyle(Colors.primary)
                    .clipShape(Capsule())
            }
        }
    }

    @ViewBuilder
    private func membershipPriceOptions(_ options: [MembershipPlanPresentation.PriceOption]) -> some View {
        if !options.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                ForEach(options) { option in
                    HStack(alignment: .firstTextBaseline, spacing: Spacing.sm) {
                        Text(option.priceText)
                            .font(Typography.body)
                        Spacer(minLength: 0)
                        Text(option.intervalText)
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                }
            }
        }
    }

    private func membershipFeatureBullets(_ bullets: [String]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(bullets, id: \.self) { feature in
                HStack(alignment: .top, spacing: Spacing.xs) {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 12))
                        .foregroundStyle(Colors.primary)
                    Text(feature)
                        .font(Typography.caption)
                        .foregroundStyle(.primary)
                }
            }
        }
    }

    private func membershipPendingAction(_ plan: MembershipPlanPresentation) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Button {} label: {
                Text(plan.purchaseButtonTitle)
            }
            .buttonStyle(.bordered)
            .disabled(true)
            .accessibilityLabel(plan.purchaseButtonTitle)

            Text(plan.purchaseButtonHint)
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
        }
    }
}
