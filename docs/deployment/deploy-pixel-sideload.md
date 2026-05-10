# Deploying LeanAI to a Google Pixel 10 (Sideload — No Play Store)

**App ID:** `com.leanai.app`  
**Min Android version:** 5.0 (API 21)  
**Target:** Android API 36

---

## Prerequisites

Install these on your development machine before starting.

| Tool | How to get |
|---|---|
| .NET 10 SDK | `winget install Microsoft.DotNet.SDK.10` or from dotnet.microsoft.com |
| .NET MAUI workload | `dotnet workload install maui-android` |
| Android SDK (API 36) | Via Android Studio SDK Manager or `dotnet build -t:InstallAndroidDependencies` |
| ADB (Android Debug Bridge) | Bundled with Android SDK — confirm with `adb version` |
| USB cable (data-capable) | USB-C for Pixel 10 |

> **Check your environment:**
> ```
> dotnet --version          # must be 10.x.x
> adb version               # must print a version, not "not found"
> ```

---

## Step 1 — Enable Developer Options on the Pixel 10

1. Open **Settings → About phone**.
2. Tap **Build number** seven times in quick succession.
3. Enter your PIN/pattern if prompted.
4. You will see "You are now a developer!" — Developer Options is now unlocked.

---

## Step 2 — Enable USB Debugging

1. Go to **Settings → System → Developer options**.
2. Toggle **USB debugging** to **ON**.
3. Optionally enable **Stay awake** to keep the screen on while plugged in.

---

## Step 3 — Connect the Phone and Trust the Computer

1. Plug the Pixel 10 into your development machine via USB.
2. On the phone, pull down the notification shade and tap the USB connection notification. Select **File Transfer (MTP)** — ADB works over any USB mode, but this avoids confusion.
3. A dialog appears: **"Allow USB debugging?"** — tap **Allow**. Check **Always allow from this computer** to avoid repeating this step.
4. On your machine, verify the device is visible:
   ```
   adb devices
   ```
   Expected output:
   ```
   List of devices attached
   <serial_number>    device
   ```
   If it shows `unauthorized`, unlock the phone and accept the dialog again.

---

## Step 4 — Build the Release APK

Run this from the repository root (`C:\Code\LeanAI`):

```
dotnet publish src/LeanAI.Maui/LeanAI.Maui.csproj ^
  -f net10.0-android ^
  -c Release ^
  -p:AndroidPackageFormat=apk ^
  -p:AndroidKeyStore=false
```

> **`AndroidKeyStore=false`** generates a debug-signed APK, which is sufficient for personal sideloading. See [Step 4b](#step-4b--optional-sign-with-a-release-keystore) if you want a properly signed release build.

The APK will be written to:
```
src\LeanAI.Maui\bin\Release\net10.0-android\com.leanai.app-Signed.apk
```

---

## Step 4b — Optional: Sign with a Release Keystore

Skip this if you are only deploying to your own device for personal use.

1. Generate a keystore (one-time):
   ```
   keytool -genkeypair -v ^
     -keystore leanai-release.keystore ^
     -alias leanai ^
     -keyalg RSA -keysize 2048 -validity 10000
   ```
   Store `leanai-release.keystore` somewhere safe and **never commit it to git**.

2. Publish with signing:
   ```
   dotnet publish src/LeanAI.Maui/LeanAI.Maui.csproj ^
     -f net10.0-android ^
     -c Release ^
     -p:AndroidPackageFormat=apk ^
     -p:AndroidKeyStore=true ^
     -p:AndroidSigningKeyStore=<path-to-leanai-release.keystore> ^
     -p:AndroidSigningKeyAlias=leanai ^
     -p:AndroidSigningKeyPass=<key-password> ^
     -p:AndroidSigningStorePass=<store-password>
   ```

---

## Step 5 — Allow Installation from Unknown Sources on the Pixel 10

Android blocks APKs not from the Play Store by default.

1. Go to **Settings → Apps → Special app access → Install unknown apps**.
2. Select the app you will use to open the APK. If you are installing directly via ADB (Step 6), you can skip this step entirely — ADB bypasses this restriction.

---

## Step 6 — Install the APK via ADB

From the repository root, with the phone connected:

```
adb install -r "src\LeanAI.Maui\bin\Release\net10.0-android\com.leanai.app-Signed.apk"
```

| Flag | Meaning |
|---|---|
| `-r` | Replace / upgrade an existing installation |

Expected output:
```
Performing Streamed Install
Success
```

If you see `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, the existing install was signed with a different key. Uninstall first:
```
adb uninstall com.leanai.app
adb install "src\LeanAI.Maui\bin\Release\net10.0-android\com.leanai.app-Signed.apk"
```

---

## Step 7 — Launch the App

Option A — tap the **LeanAI** icon in the app drawer on the phone.

Option B — launch from the terminal (useful for confirming install without touching the phone):
```
adb shell am start -n com.leanai.app/com.leanai.app.MainActivity
```

---

## Step 8 — View Logs (Optional)

To stream the app's debug output to your terminal:

```
adb logcat -s "DOTNET" "MAUI" "mono-rt"
```

Or filter to only LeanAI output:
```
adb logcat | findstr "leanai"
```

---

## Updating the App

Repeat Steps 4 and 6 (`-r` flag handles the upgrade in place). The SQLite database at `AppDataDirectory/leanai.db` is preserved across upgrades because it lives in the app's internal storage.

---

## Uninstalling

```
adb uninstall com.leanai.app
```

Or via **Settings → Apps → LeanAI → Uninstall** on the phone.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `adb: command not found` | ADB not on PATH | Add `C:\Program Files (x86)\Android\android-sdk\platform-tools` to your PATH (see note below table) |
| `adb devices` shows `unauthorized` | Debugging dialog not accepted | Unlock phone, accept the dialog |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | Signing key mismatch | `adb uninstall com.leanai.app` then reinstall |
| `INSTALL_PARSE_FAILED_NO_CERTIFICATES` | APK not signed | Re-run publish — ensure `AndroidKeyStore=false` is set |
| App crashes on launch | Runtime error | Check `adb logcat -s DOTNET` for the exception |
| Build fails: workload not found | MAUI workload missing | `dotnet workload install maui-android` |
| Build fails: Android SDK API 36 not found | SDK not installed | Open Android Studio SDK Manager → install **Android SDK Platform 36** |

> **Adding ADB to PATH (permanent, user-level — no admin required):**
> ```powershell
> [Environment]::SetEnvironmentVariable(
>     "PATH",
>     $env:PATH + ";C:\Program Files (x86)\Android\android-sdk\platform-tools",
>     "User"
> )
> ```
> Close and reopen PowerShell after running this, then verify with `adb version`.
