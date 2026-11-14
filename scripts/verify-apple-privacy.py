#!/usr/bin/env python3
"""Validate source and exported Apple privacy manifests with Python's plist parser."""

import argparse
import plistlib
import sys
from pathlib import Path
from xml.parsers.expat import ExpatError


APP_DATA = {
    "NSPrivacyCollectedDataTypeEmailAddress": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypeUserID": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality", "NSPrivacyCollectedDataTypePurposeAnalytics"]),
    "NSPrivacyCollectedDataTypeDeviceID": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypePhotosorVideos": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypeOtherUserContent": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypeOtherFinancialInfo": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypeCrashData": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypePerformanceData": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
    "NSPrivacyCollectedDataTypeProductInteraction": (True, ["NSPrivacyCollectedDataTypePurposeAnalytics"]),
    "NSPrivacyCollectedDataTypeEmailsOrTextMessages": (True, ["NSPrivacyCollectedDataTypePurposeAppFunctionality"]),
}
APP_REASONS = {
    "NSPrivacyAccessedAPICategoryUserDefaults": ["CA92.1"],
    "NSPrivacyAccessedAPICategorySystemBootTime": ["35F9.1"],
}
PACKAGE_REASONS = {
    "VouchaCore_VouchaCore": {"NSPrivacyAccessedAPICategoryUserDefaults": ["CA92.1"]},
    "VouchaCore_VouchaAPI": {"NSPrivacyAccessedAPICategorySystemBootTime": ["35F9.1"]},
    "VouchaUI_VouchaFeatures": {"NSPrivacyAccessedAPICategoryUserDefaults": ["CA92.1"]},
}
SENTRY_REASONS = {
    "NSPrivacyAccessedAPICategoryUserDefaults": ["CA92.1"],
    "NSPrivacyAccessedAPICategorySystemBootTime": ["35F9.1"],
    "NSPrivacyAccessedAPICategoryFileTimestamp": ["C617.1"],
}


def fail(message):
    raise ValueError(message)


def load_manifest(path):
    try:
        with path.open("rb") as stream:
            value = plistlib.load(stream)
    except (OSError, plistlib.InvalidFileException, ValueError, ExpatError) as error:
        fail(f"cannot parse {path}: {error}")
    if not isinstance(value, dict):
        fail(f"{path} is not a property-list dictionary")
    return value


def reason_map(manifest, path):
    entries = manifest.get("NSPrivacyAccessedAPITypes")
    if not isinstance(entries, list):
        fail(f"{path} has no accessed-API array")
    result = {}
    for entry in entries:
        if not isinstance(entry, dict):
            fail(f"{path} has a malformed accessed-API entry")
        category = entry.get("NSPrivacyAccessedAPIType")
        reasons = entry.get("NSPrivacyAccessedAPITypeReasons")
        if not isinstance(category, str) or not isinstance(reasons, list) or not all(
            isinstance(reason, str) for reason in reasons
        ):
            fail(f"{path} has a malformed accessed-API entry")
        if category in result:
            fail(f"{path} repeats accessed-API category {category}")
        result[category] = reasons
    return result


def validate_manifest(path, expected_reasons, expected_data):
    manifest = load_manifest(path)
    if manifest.get("NSPrivacyTracking") is not False:
        fail(f"{path} must set NSPrivacyTracking to false")
    if "NSPrivacyTrackingDomains" in manifest:
        fail(f"{path} must not declare tracking domains")
    if reason_map(manifest, path) != expected_reasons:
        fail(f"{path} has unexpected required-reason API declarations")
    entries = manifest.get("NSPrivacyCollectedDataTypes")
    if not isinstance(entries, list):
        fail(f"{path} has no collected-data array")
    actual = {}
    for entry in entries:
        if not isinstance(entry, dict):
            fail(f"{path} has a malformed collected-data entry")
        kind = entry.get("NSPrivacyCollectedDataType")
        if not isinstance(kind, str) or kind in actual:
            fail(f"{path} has a missing or duplicate collected-data type")
        purposes = entry.get("NSPrivacyCollectedDataTypePurposes")
        if not isinstance(purposes, list):
            fail(f"{path} has malformed purposes for {kind}")
        linked = entry.get("NSPrivacyCollectedDataTypeLinked")
        tracking = entry.get("NSPrivacyCollectedDataTypeTracking")
        if type(linked) is not bool or type(tracking) is not bool or not all(
            isinstance(purpose, str) for purpose in purposes
        ):
            fail(f"{path} has malformed privacy flags or purposes for {kind}")
        actual[kind] = (linked, tracking, purposes)
    expected = {
        kind: (linked, False, purposes) for kind, (linked, purposes) in expected_data.items()
    }
    if actual != expected:
        fail(f"{path} has unexpected collected-data flags, purposes, or types")


def validate_source(root):
    for platform in ("iOS", "macOS"):
        path = root / "swift-clients" / "apps" / platform / "PrivacyInfo.xcprivacy"
        validate_manifest(path, APP_REASONS, APP_DATA)
    for bundle, reasons in PACKAGE_REASONS.items():
        if bundle == "VouchaUI_VouchaFeatures":
            path = root / "swift-clients/ui/Sources/VouchaFeatures/PrivacyInfo.xcprivacy"
        else:
            target = bundle.removeprefix("VouchaCore_")
            path = root / "swift-clients/core/Sources" / target / "PrivacyInfo.xcprivacy"
        validate_manifest(path, reasons, {})


def app_from_artifact(artifact):
    if artifact.suffix == ".app":
        return artifact
    if artifact.suffix == ".xcarchive":
        apps = list((artifact / "Products/Applications").glob("*.app"))
        if len(apps) != 1:
            fail(f"{artifact} must contain exactly one app in Products/Applications")
        return apps[0]
    fail("artifact must be an .app bundle or .xcarchive")


def validate_artifact(artifact, source_root):
    app = app_from_artifact(artifact)
    if not app.is_dir():
        fail(f"app bundle does not exist: {app}")
    app_contents = app / "Contents"
    app_resources = app / "Contents/Resources" if app_contents.is_dir() else app
    app_manifest = app_resources / "PrivacyInfo.xcprivacy"
    misplaced_app_manifest = (
        app / "PrivacyInfo.xcprivacy" if app_contents.is_dir() else app / "Contents/Resources/PrivacyInfo.xcprivacy"
    )
    if not app_manifest.is_file() or misplaced_app_manifest.is_file():
        fail(f"{app} must contain exactly one app PrivacyInfo.xcprivacy in its platform resource location")
    validate_manifest(app_manifest, APP_REASONS, APP_DATA)
    for bundle, reasons in PACKAGE_REASONS.items():
        bundles = [path for path in app.rglob(f"{bundle}.bundle") if path.is_dir()]
        if len(bundles) != 1:
            fail(f"{app} must contain exactly one {bundle} privacy resource bundle")
        resource_bundle = bundles[0]
        bundle_resources = (
            resource_bundle / "Contents/Resources"
            if (resource_bundle / "Contents").is_dir()
            else resource_bundle
        )
        package_manifest = bundle_resources / "PrivacyInfo.xcprivacy"
        package_manifests = list(resource_bundle.rglob("PrivacyInfo.xcprivacy"))
        if not package_manifest.is_file() or package_manifests != [package_manifest]:
            fail(f"{app} has a misplaced or duplicate {bundle} privacy manifest")
        validate_manifest(package_manifest, reasons, {})
    sentry_manifests = [
        path
        for path in app.rglob("PrivacyInfo.xcprivacy")
        if any(
            part == "Sentry.framework" or (part.startswith("Sentry") and part.endswith(".bundle"))
            for part in path.relative_to(app).parts
        )
    ]
    if not sentry_manifests:
        fail(f"{app} has no embedded Sentry privacy manifest")
    for path in sentry_manifests:
        sentry = load_manifest(path)
        if reason_map(sentry, path) != SENTRY_REASONS:
            fail(f"{path} does not contain the pinned Sentry required-reason declarations")
    if source_root is not None:
        validate_source(source_root)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path)
    parser.add_argument("--artifact", type=Path)
    args = parser.parse_args()
    if args.source_root is None and args.artifact is None:
        parser.error("provide --source-root and/or --artifact")
    if args.source_root is not None:
        validate_source(args.source_root)
    if args.artifact is not None:
        validate_artifact(args.artifact, args.source_root)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except ValueError as error:
        print(f"apple privacy verification failed: {error}", file=sys.stderr)
        sys.exit(1)
