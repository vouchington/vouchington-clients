// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "VouchaCore",
    platforms: [
        .macOS(.v14),
        .iOS(.v17)
    ],
    products: [
        .library(name: "VouchaCore", targets: ["VouchaCore"]),
        .library(name: "VouchaModels", targets: ["VouchaModels"]),
        .library(name: "VouchaAPI", targets: ["VouchaAPI"]),
        .library(name: "VouchaAuth", targets: ["VouchaAuth"]),
        .library(name: "VouchaPersistence", targets: ["VouchaPersistence"])
    ],
    dependencies: [
        .package(name: "VouchaTestSupport", path: "../test-support"),
        .package(url: "https://github.com/apple/swift-crypto.git", exact: "4.5.2")
    ],
    targets: [
        .target(
            name: "VouchaCore",
            dependencies: [
                .product(name: "Crypto", package: "swift-crypto")
            ]
        ),
        .target(name: "VouchaModels", dependencies: ["VouchaCore"]),
        .target(
            name: "VouchaAPI",
            dependencies: [
                "VouchaModels",
                "VouchaCore",
                .product(name: "Crypto", package: "swift-crypto")
            ]
        ),
        .target(
            name: "VouchaAuth",
            dependencies: [
                "VouchaAPI",
                "VouchaCore",
                "VouchaModels",
                .product(name: "Crypto", package: "swift-crypto")
            ]
        ),
        .target(name: "VouchaPersistence", dependencies: ["VouchaModels", "VouchaCore"]),
        .testTarget(
            name: "VouchaCoreTests",
            dependencies: [
                "VouchaCore",
                "VouchaModels",
                "VouchaAPI",
                "VouchaAuth",
                "VouchaPersistence",
                .product(name: "Crypto", package: "swift-crypto"),
                .product(name: "VouchaTestSupport", package: "VouchaTestSupport")
            ]
        ),
        .testTarget(
            name: "VouchaIntegrationTests",
            dependencies: [
                "VouchaCore",
                "VouchaModels",
                "VouchaAPI",
                .product(name: "VouchaTestSupport", package: "VouchaTestSupport")
            ]
        )
    ]
)
