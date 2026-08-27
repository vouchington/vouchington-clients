// swift-tools-version: 6.1
// This is a Skip (https://skip.dev) package.
import PackageDescription

let package = Package(
    name: "voucha-android",
    defaultLocalization: "en",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "VouchaAndroid", type: .dynamic, targets: ["VouchaAndroid"])
    ],
    dependencies: [
        .package(path: "../../core"),
        .package(path: "../../ui"),
        .package(url: "https://source.skip.tools/skip.git", exact: "1.9.7"),
        .package(url: "https://source.skip.tools/skip-fuse-ui.git", from: "1.0.0"),
        .package(url: "https://source.skip.tools/skip-keychain.git", exact: "0.3.2")
    ],
    targets: [
        .target(name: "VouchaAndroid", dependencies: [
            .product(name: "VouchaCore", package: "core"),
            .product(name: "VouchaLocalization", package: "ui"),
            .product(name: "SkipFuseUI", package: "skip-fuse-ui"),
            .product(name: "SkipKeychain", package: "skip-keychain")
        ], resources: [.process("Resources")], plugins: [.plugin(name: "skipstone", package: "skip")]),
        .testTarget(name: "VouchaAndroidTests", dependencies: [
            "VouchaAndroid",
            .product(name: "VouchaLocalization", package: "ui")
        ])
    ]
)
