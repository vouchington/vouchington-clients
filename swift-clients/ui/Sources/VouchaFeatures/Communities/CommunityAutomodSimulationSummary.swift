import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityAutomodSimulationSummary: View {
    let result: CommunityAutomodSimulation
    @Environment(\.locale)
    private var locale

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(UiMessages.string(
                .nativeCommunitiesAutomodSampleSummary,
                parameters: [
                    "sampleCount": UiMessages.number(result.simulation.sampleCount, locale: locale),
                    "wouldFlagCount": UiMessages.number(result.simulation.wouldFlagCount, locale: locale)
                ],
                locale: locale
            ))
            if let estimate = result.simulation.falsePositiveEstimate {
                Text(UiMessages.string(
                    .nativeCommunitiesAutomodEstimateSample,
                    parameters: [
                        "sampledFlaggedCount": UiMessages.number(estimate.sampledFlaggedCount, locale: locale),
                        "sampledApprovedCount": UiMessages.number(estimate.sampledApprovedCount, locale: locale)
                    ],
                    locale: locale
                ))
            }
        }
        .font(Typography.caption)
    }
}
