plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.familia.carretajoy"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.familia.carretajoy"
        minSdk = 24
        targetSdk = 35
        versionCode = 8
        versionName = "0.5.3"
    }

    signingConfigs {
        // Chave de debug fixa no repositório: assim um APK novo instala por cima do antigo,
        // não importa se foi compilado no seu PC ou no GitHub Actions.
        getByName("debug") {
            storeFile = rootProject.file("keystore/debug.keystore")
            storePassword = "android"
            keyAlias = "androiddebugkey"
            keyPassword = "android"
        }
    }

    buildTypes {
        debug {
            signingConfig = signingConfigs.getByName("debug")
        }
        release {
            isMinifyEnabled = false
            signingConfig = signingConfigs.getByName("debug")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
    androidResources {
        // não comprimir os modelos 3D (carregam mais rápido)
        noCompress += listOf("glb")
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("androidx.webkit:webkit:1.12.1")
}
