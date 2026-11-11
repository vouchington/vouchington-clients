// swift-tools-version: 5.9
import Foundation
import PackageDescription

// Generated localization is pulled from the pinned Vouchington contract and never committed.
let pulledLocalizationKey = URL(fileURLWithPath: #filePath)
    .deletingLastPathComponent()
    .appendingPathComponent("Sources/VouchaLocalization/Generated/UiMessageKey.swift")
guard FileManager.default.fileExists(atPath: pulledLocalizationKey.path) else {
    fatalError("Generated native localization is missing. Run `pnpm run contracts:pull` from the repository root.")
}

let package = Package(
    name: "VouchaUI",
    defaultLocalization: "en",
    platforms: [
        .macOS(.v14),
        .iOS(.v17)
    ],
    products: [
        .library(name: "VouchaLocalization", targets: ["VouchaLocalization"]),
        .library(name: "VouchaDesignSystem", targets: ["VouchaDesignSystem"]),
        .library(name: "VouchaFeatures", targets: ["VouchaFeatures"])
    ],
    dependencies: [
        .package(name: "VouchaCore", path: "../core"),
        .package(name: "VouchaTestSupport", path: "../test-support"),
        .package(url: "https://github.com/scinfu/SwiftSoup.git", .upToNextMajor(from: "2.13.9")),
        .package(url: "https://github.com/nalexn/ViewInspector.git", .upToNextMinor(from: "0.10.3"))
    ],
    targets: [
        .target(
            name: "VouchaLocalization",
            resources: [.process("Generated/Resources")]
        ),
        .target(
            name: "VouchaDesignSystem",
            dependencies: [
                "VouchaLocalization",
                .product(name: "VouchaCore", package: "VouchaCore"),
                .product(name: "VouchaModels", package: "VouchaCore"),
                .product(name: "SwiftSoup", package: "SwiftSoup")
            ]
        ),
        .target(
            name: "VouchaFeatures",
            dependencies: [
                "VouchaDesignSystem",
                "VouchaLocalization",
                .product(name: "VouchaCore", package: "VouchaCore"),
                .product(name: "VouchaModels", package: "VouchaCore"),
                .product(name: "VouchaAPI", package: "VouchaCore"),
                .product(name: "VouchaAuth", package: "VouchaCore"),
                .product(name: "VouchaPersistence", package: "VouchaCore")
            ],
            resources: [.copy("PrivacyInfo.xcprivacy")]
        ),
        .testTarget(
            name: "VouchaUITests",
            dependencies: [
                "VouchaDesignSystem",
                "VouchaFeatures",
                "VouchaLocalization",
                .product(name: "VouchaCore", package: "VouchaCore"),
                .product(name: "VouchaAPI", package: "VouchaCore"),
                .product(name: "VouchaAuth", package: "VouchaCore"),
                .product(name: "VouchaTestSupport", package: "VouchaTestSupport"),
                .product(name: "ViewInspector", package: "ViewInspector")
            ]
        )
    ]
)
