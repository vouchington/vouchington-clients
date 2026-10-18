import java.util.Properties

plugins {
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.android.application)
    id("skip-build-plugin")
}

val configuredApiBaseUrl = providers.gradleProperty("VOUCHA_API_BASE_URL").orNull
    ?: providers.environmentVariable("VOUCHA_API_BASE_URL").orNull
    ?: "https://voucha.ai"
val configuredTurnstileSiteKey = providers.gradleProperty("VOUCHA_TURNSTILE_SITE_KEY").orNull
    ?: providers.environmentVariable("VOUCHA_TURNSTILE_SITE_KEY").orNull
    ?: providers.gradleProperty("NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY").orNull
    ?: providers.environmentVariable("NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY").orNull
    ?: ""

skip {
}

dependencies {
    implementation("com.google.mlkit:genai-prompt:1.0.0-beta2")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.10.2")
    // Match the pinned SkipKeychain 0.3.2 encrypted preference format for checked cookie commits.
    implementation("androidx.security:security-crypto:1.0.0")
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.fromTarget(libs.versions.jvm.get().toString())
    }
}

android {
    namespace = group as String
    compileSdk = libs.versions.android.sdk.compile.get().toInt()
    compileOptions {
        sourceCompatibility = JavaVersion.toVersion(libs.versions.jvm.get())
        targetCompatibility = JavaVersion.toVersion(libs.versions.jvm.get())
    }
    packaging {
        jniLibs {
            keepDebugSymbols.add("**/*.so")
            pickFirsts.add("**/*.so")
            // this option would compress JNI .so files and reduce overall size for Skip Fuse apps, but cost more at install time
            //useLegacyPackaging = true
        }
    }

    defaultConfig {
        minSdk = 28
        targetSdk = libs.versions.android.sdk.compile.get().toInt()
        // skip.tools.skip-build-plugin will automatically use Skip.env properties for:
        // applicationId = ANDROID_APPLICATION_ID ?? PRODUCT_BUNDLE_IDENTIFIER
        // versionCode = CURRENT_PROJECT_VERSION
        // versionName = MARKETING_VERSION
        manifestPlaceholders["VOUCHA_API_BASE_URL"] = configuredApiBaseUrl
        manifestPlaceholders["VOUCHA_TURNSTILE_SITE_KEY"] = configuredTurnstileSiteKey
    }

    buildFeatures {
        buildConfig = true
    }

    lint {
        disable.add("Instantiatable")
        disable.add("MissingPermission")
    }

    dependenciesInfo {
        // Disables dependency metadata when building APKs.
        includeInApk = false
        // Disables dependency metadata when building Android App Bundles.
        includeInBundle = false
    }

    // default signing configuration tries to load from keystore.properties
    // see: https://skip.dev/docs/deployment/#export-signing
    signingConfigs {
        val keystorePropertiesFile = file("keystore.properties")
        create("release") {
            if (keystorePropertiesFile.isFile) {
                val keystoreProperties = Properties()
                keystoreProperties.load(keystorePropertiesFile.inputStream())
                keyAlias = keystoreProperties.getProperty("keyAlias")
                keyPassword = keystoreProperties.getProperty("keyPassword")
                storeFile = file(keystoreProperties.getProperty("storeFile"))
                storePassword = keystoreProperties.getProperty("storePassword")
            }
        }
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.findByName("release")
            isMinifyEnabled = true
            isShrinkResources = true
            isDebuggable = false // can be set to true for debugging release build, but needs to be false when uploading to store
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }

}

gradle.taskGraph.whenReady {
    if (allTasks.any { it.name.contains("Release", ignoreCase = true) } && !file("keystore.properties").isFile) {
        error("keystore.properties is required for release builds")
    }
}
