// This gradle project is part of a conventional Skip app project.
pluginManagement {
    // Initialize the Skip plugin folder and perform a pre-build for non-Xcode builds
    val pluginPath = File.createTempFile("skip-plugin-path", ".tmp")

    // overriding outputs for an Android IDE can be done by un-commenting and setting the Xcode path:
    //System.setProperty("BUILT_PRODUCTS_DIR", "${System.getProperty("user.home")}/Library/Developer/Xcode/DerivedData/MySkipProject-HASH/Build/Products/Debug-iphonesimulator")

    val skipPluginResult = providers.exec {
        commandLine(
            "skip",
            "plugin",
            "--prebuild",
            "--package-path",
            settings.rootDir.parent,
            "--plugin-ref",
            pluginPath.absolutePath,
        )
        environment("PATH", "${System.getenv("PATH")}:/opt/homebrew/bin")
    }
    val skipPluginOutput = skipPluginResult.standardOutput.asText.get()
    print(skipPluginOutput)
    val skipPluginError = skipPluginResult.standardError.asText.get()
    print(skipPluginError)

    includeBuild(pluginPath.readText()) {
        name = "skip-plugins"
    }
}

plugins {
    id("skip-plugin") apply true
}

buildCache {
    local {
        // Resolved by pre-push.sh / CI from the OS temp directory, shared across runners on the
        // same host. Left at its default (GRADLE_USER_HOME/caches/build-cache-1) when unset, e.g.
        // under Android Studio. macOS's own periodic sweep of that directory (~3 days) bounds
        // retention; Gradle 9 removed the equivalent `removeUnusedEntriesAfterDays` project setting.
        System.getenv("GRADLE_BUILD_CACHE_DIR")?.let { directory = File(it) }
    }
}
