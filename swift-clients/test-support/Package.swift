// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "VouchaTestSupport",
    products: [
        .library(name: "VouchaTestSupport", targets: ["VouchaTestSupport"])
    ],
    targets: [
        .target(name: "VouchaTestSupport"),
        .testTarget(name: "VouchaTestSupportTests", dependencies: ["VouchaTestSupport"])
    ]
)
