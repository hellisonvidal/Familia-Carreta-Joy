package com.familia.carretajoy

import android.annotation.SuppressLint
import android.content.Intent
import android.graphics.Color
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.InputDevice
import android.view.KeyEvent
import android.view.MotionEvent
import android.view.View
import android.view.WindowManager
import android.webkit.JavascriptInterface
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.FileProvider
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.webkit.WebViewAssetLoader
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import kotlin.concurrent.thread

/**
 * O jogo é feito em three.js (WebGL) e fica dentro do APK em assets/www.
 * O WebViewAssetLoader serve esses arquivos por https://appassets.androidplatform.net,
 * o que permite usar módulos JavaScript e carregar os modelos 3D sem internet.
 *
 * Também: repassa os botões/analógicos do controle (PlayStation/Xbox) para o jogo e
 * oferece o atualizador (verifica a última versão no GitHub, baixa e instala o APK).
 */
class MainActivity : AppCompatActivity() {

    private lateinit var webView: WebView
    private var pendingApk: File? = null

    companion object {
        const val RELEASES = "https://api.github.com/repos/hellisonvidal/Familia-Carreta-Joy/releases/latest"
    }

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        WindowCompat.setDecorFitsSystemWindows(window, false)

        val assetLoader = WebViewAssetLoader.Builder()
            .addPathHandler("/assets/", WebViewAssetLoader.AssetsPathHandler(this))
            .build()

        webView = WebView(this).apply {
            setBackgroundColor(Color.BLACK)
            settings.javaScriptEnabled = true
            settings.domStorageEnabled = true
            settings.mediaPlaybackRequiresUserGesture = false
            settings.allowFileAccess = false
            settings.allowContentAccess = false
            overScrollMode = View.OVER_SCROLL_NEVER
            isFocusable = true
            isFocusableInTouchMode = true
            webViewClient = object : WebViewClient() {
                override fun shouldInterceptRequest(
                    view: WebView,
                    request: WebResourceRequest
                ): WebResourceResponse? = assetLoader.shouldInterceptRequest(request.url)
            }
            addJavascriptInterface(Bridge(), "CarretaApp")
        }
        setContentView(webView)
        hideSystemBars()

        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                webView.evaluateJavascript("window.onAndroidBack ? window.onAndroidBack() : false") { handled ->
                    if (handled != "true") finish()
                }
            }
        })

        if (savedInstanceState == null) {
            webView.loadUrl("https://appassets.androidplatform.net/assets/www/index.html")
        } else {
            webView.restoreState(savedInstanceState)
        }
    }

    private fun js(code: String) = runOnUiThread { webView.evaluateJavascript(code, null) }

    // ---------------- ponte com o JavaScript ----------------
    inner class Bridge {
        @JavascriptInterface
        fun version(): String = try {
            packageManager.getPackageInfo(packageName, 0).versionName ?: "?"
        } catch (e: Exception) { "?" }

        @JavascriptInterface
        fun checkUpdate() {
            thread {
                try {
                    val con = URL(RELEASES).openConnection() as HttpURLConnection
                    con.setRequestProperty("Accept", "application/vnd.github+json")
                    con.setRequestProperty("User-Agent", "CarretaJoy-App")
                    con.connectTimeout = 15000; con.readTimeout = 15000
                    val body = con.inputStream.bufferedReader().readText()
                    js("window.onUpdateInfo(" + JSONObject.quote(body) + ")")
                } catch (e: Exception) {
                    js("window.onUpdateInfo(" + JSONObject.quote(JSONObject().put("erro", e.message ?: "sem internet").toString()) + ")")
                }
            }
        }

        @JavascriptInterface
        fun downloadAndInstall(url: String) {
            thread {
                try {
                    val dir = File(getExternalFilesDir(null) ?: cacheDir, "updates").apply { mkdirs() }
                    val file = File(dir, "CarretaJoy.apk")
                    val con = URL(url).openConnection() as HttpURLConnection
                    con.setRequestProperty("User-Agent", "CarretaJoy-App")
                    con.instanceFollowRedirects = true
                    val total = con.contentLength.toLong()
                    con.inputStream.use { input ->
                        file.outputStream().use { out ->
                            val buf = ByteArray(64 * 1024); var done = 0L; var last = -1
                            while (true) {
                                val n = input.read(buf); if (n < 0) break
                                out.write(buf, 0, n); done += n
                                if (total > 0) { val pct = (done * 100 / total).toInt(); if (pct != last) { last = pct; js("window.onUpdateProgress($pct)") } }
                            }
                        }
                    }
                    js("window.onUpdateProgress(100)")
                    runOnUiThread { install(file) }
                } catch (e: Exception) {
                    js("window.onUpdateError(" + JSONObject.quote("Falha ao baixar: " + (e.message ?: "")) + ")")
                }
            }
        }
    }

    private fun install(file: File) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && !packageManager.canRequestPackageInstalls()) {
            // o Android pede permissão para instalar apps deste aplicativo
            pendingApk = file
            js("window.onUpdateError('Permita \"Instalar apps desconhecidos\" para a Carreta Joy e volte para o jogo.')")
            startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:$packageName")))
            return
        }
        val uri = FileProvider.getUriForFile(this, "$packageName.files", file)
        val intent = Intent(Intent.ACTION_VIEW)
            .setDataAndType(uri, "application/vnd.android.package-archive")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        startActivity(intent)
    }

    // ---------------- controle de videogame ----------------
    private fun isPad(source: Int) =
        (source and InputDevice.SOURCE_GAMEPAD) == InputDevice.SOURCE_GAMEPAD ||
        (source and InputDevice.SOURCE_JOYSTICK) == InputDevice.SOURCE_JOYSTICK

    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        val code = event.keyCode
        val padKey = KeyEvent.isGamepadButton(code) ||
            (isPad(event.source) && code in listOf(KeyEvent.KEYCODE_DPAD_UP, KeyEvent.KEYCODE_DPAD_DOWN, KeyEvent.KEYCODE_DPAD_LEFT, KeyEvent.KEYCODE_DPAD_RIGHT, KeyEvent.KEYCODE_DPAD_CENTER))
        if (padKey) {
            if (event.repeatCount == 0) js("window.onPadKey && window.onPadKey($code, ${event.action == KeyEvent.ACTION_DOWN})")
            return true
        }
        return super.dispatchKeyEvent(event)
    }

    override fun dispatchGenericMotionEvent(ev: MotionEvent): Boolean {
        if (isPad(ev.source) && ev.action == MotionEvent.ACTION_MOVE) {
            val lt = maxOf(ev.getAxisValue(MotionEvent.AXIS_LTRIGGER), ev.getAxisValue(MotionEvent.AXIS_BRAKE))
            val rt = maxOf(ev.getAxisValue(MotionEvent.AXIS_RTRIGGER), ev.getAxisValue(MotionEvent.AXIS_GAS))
            js("window.onPadAxes && window.onPadAxes(" +
                "${ev.getAxisValue(MotionEvent.AXIS_X)},${ev.getAxisValue(MotionEvent.AXIS_Y)}," +
                "${ev.getAxisValue(MotionEvent.AXIS_Z)},${ev.getAxisValue(MotionEvent.AXIS_RZ)}," +
                "$lt,$rt,${ev.getAxisValue(MotionEvent.AXIS_HAT_X)},${ev.getAxisValue(MotionEvent.AXIS_HAT_Y)})")
            return true
        }
        return super.dispatchGenericMotionEvent(ev)
    }

    // ---------------- ciclo de vida ----------------
    private fun hideSystemBars() {
        val controller = WindowInsetsControllerCompat(window, window.decorView)
        controller.hide(WindowInsetsCompat.Type.systemBars())
        controller.systemBarsBehavior =
            WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) hideSystemBars()
    }

    override fun onPause() {
        webView.evaluateJavascript("window.onAppPause && window.onAppPause()", null)
        webView.onPause()
        super.onPause()
    }

    override fun onResume() {
        super.onResume()
        webView.onResume()
        webView.evaluateJavascript("window.onAppResume && window.onAppResume()", null)
        // voltou da tela de permissão: tenta instalar de novo
        pendingApk?.let { f ->
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O || packageManager.canRequestPackageInstalls()) { pendingApk = null; install(f) }
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        webView.saveState(outState)
    }

    override fun onDestroy() {
        webView.destroy()
        super.onDestroy()
    }
}
