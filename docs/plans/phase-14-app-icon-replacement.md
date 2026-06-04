# Phase 14: App Icon Replacement (IconKitchen Assets)

## Goal
Replace MAUI's resizetizer-generated launcher icon with pre-built iconkitchen PNG assets that include a proper `ic_launcher_monochrome.png` layer, resolving the white-square regression on Android 13+ themed icons (Pixel 10).

## Why the current approach fails
`<MauiIcon>` feeds a single PNG into MAUI's resizetizer, which generates:
- `leanai_icon.png` (legacy flat icon)
- `leanai_icon_foreground.png` (adaptive foreground)

It cannot produce a proper monochrome layer. The previous workaround (`leanai_icon_monochrome.xml` vector drawable) did not render correctly. IconKitchen generates all four required PNGs at all five densities — the cleanest fix is to bypass the resizetizer entirely and use those pre-built assets as native Android resources.

## Source assets
`z-com-ai/iconkitchen-output/android/res/` — already extracted from `z-com-ai/iconkitchen-output.zip`.

## Implementation Steps

### Step 1 — Remove `<MauiIcon>` from csproj
**File:** `src/LeanAI.Maui/LeanAI.Maui.csproj`

Remove:
```xml
<MauiIcon Include="Resources\AppIcon\leanai_icon.png" Color="#222222" />
```

This stops the resizetizer from generating `leanai_icon_*` resources. The icon will now come entirely from the Android Resources folders below.

---

### Step 2 — Copy pre-built icon PNGs into Android Resources
For each density — `mipmap-mdpi`, `mipmap-hdpi`, `mipmap-xhdpi`, `mipmap-xxhdpi`, `mipmap-xxxhdpi` — copy the four PNGs from `z-com-ai/iconkitchen-output/android/res/mipmap-{density}/` into `src/LeanAI.Maui/Platforms/Android/Resources/mipmap-{density}/`:

| Source file              | Purpose                                    |
|--------------------------|--------------------------------------------|
| `ic_launcher.png`        | Legacy flat icon (API < 26 fallback)       |
| `ic_launcher_foreground.png` | Adaptive icon foreground (colored)     |
| `ic_launcher_background.png` | Adaptive icon background image        |
| `ic_launcher_monochrome.png` | Themed-icon monochrome layer (API 33+) |

All 20 PNGs (4 variants × 5 densities) must be present.

---

### Step 3 — Copy adaptive icon XML
Copy `z-com-ai/iconkitchen-output/android/res/mipmap-anydpi-v26/ic_launcher.xml` to:
`src/LeanAI.Maui/Platforms/Android/Resources/mipmap-anydpi-v26/ic_launcher.xml`

This XML must contain all three adaptive layers:
```xml
<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
  <background android:drawable="@mipmap/ic_launcher_background"/>
  <foreground android:drawable="@mipmap/ic_launcher_foreground"/>
  <monochrome android:drawable="@mipmap/ic_launcher_monochrome"/>
</adaptive-icon>
```

Using `mipmap-anydpi-v26` (not `v33`) activates adaptive icons from Android 8.0+. The `<monochrome>` element is silently ignored by OS versions below API 33; it is used for themed icons on API 33+.

---

### Step 4 — Update AndroidManifest.xml
**File:** `src/LeanAI.Maui/Platforms/Android/AndroidManifest.xml`

Change `android:icon` and `android:roundIcon` to reference `ic_launcher`:
```xml
<application
    android:allowBackup="true"
    android:icon="@mipmap/ic_launcher"
    android:roundIcon="@mipmap/ic_launcher"
    android:supportsRtl="true">
</application>
```

No separate `_round` variant is needed — on API 26+ the adaptive icon system handles round clipping. On API 21–25 (legacy), `ic_launcher.png` provides the flat fallback for both `icon` and `roundIcon`.

---

### Step 5 — Remove obsolete old icon assets
Delete the following files (they are superseded by Step 2 and no longer referenced):

| File to delete | Reason |
|---|---|
| `src/LeanAI.Maui/Resources/AppIcon/leanai_icon.png` | MAUI resizetizer source — no longer used |
| `src/LeanAI.Maui/Resources/AppIcon/appicon.svg` | MAUI placeholder SVG |
| `src/LeanAI.Maui/Resources/AppIcon/appiconfg.svg` | MAUI placeholder foreground SVG |
| `src/LeanAI.Maui/Platforms/Android/Resources/mipmap-anydpi-v33/leanai_icon.xml` | Old adaptive XML (wrong name, wrong folder) |
| `src/LeanAI.Maui/Platforms/Android/Resources/drawable/leanai_icon_monochrome.xml` | Old vector monochrome (broken silhouette) |

After deleting, remove the now-empty `mipmap-anydpi-v33/` directory if the build system doesn't handle it automatically.

---

### Step 6 — Copy Play Store icon
Copy `z-com-ai/iconkitchen-output/android/play_store_512.png` to:
`src/LeanAI.Maui/Resources/AppIcon/play_store_512.png`

This is the 512×512 PNG required by Google Play. It is not a `<MauiIcon>` or `<MauiImage>` — it is an inert project file for reference/upload. No csproj entry is needed.

---

### Step 7 — Build & verify
1. Run `dotnet build src/LeanAI.Maui/LeanAI.Maui.csproj -c Release` → must be 0 errors.
2. Run `dotnet test` → must be 209 / 209 green.
3. Deploy Release APK to Pixel 10:
   - Home screen: full-color LeanAI icon visible.
   - Settings → Wallpaper & style → Themed icons ON: monochrome silhouette visible, correctly tinted (not a white square).

---

## Files Changed (summary)

| File | Action |
|---|---|
| `src/LeanAI.Maui/LeanAI.Maui.csproj` | Remove `<MauiIcon>` entry |
| `src/LeanAI.Maui/Platforms/Android/AndroidManifest.xml` | Update icon/roundIcon to `@mipmap/ic_launcher` |
| `Platforms/Android/Resources/mipmap-{5 densities}/ic_launcher*.png` (×4) | Add (20 files) |
| `Platforms/Android/Resources/mipmap-anydpi-v26/ic_launcher.xml` | Add |
| `src/LeanAI.Maui/Resources/AppIcon/play_store_512.png` | Add |
| `src/LeanAI.Maui/Resources/AppIcon/leanai_icon.png` | Delete |
| `src/LeanAI.Maui/Resources/AppIcon/appicon.svg` | Delete |
| `src/LeanAI.Maui/Resources/AppIcon/appiconfg.svg` | Delete |
| `Platforms/Android/Resources/mipmap-anydpi-v33/leanai_icon.xml` | Delete |
| `Platforms/Android/Resources/drawable/leanai_icon_monochrome.xml` | Delete |

## Definition of Done (DoD)

- [x] `<MauiIcon>` removed from csproj
- [x] 20 iconkitchen PNGs in `Platforms/Android/Resources/mipmap-*/`
- [x] `mipmap-anydpi-v26/ic_launcher.xml` present with foreground + background + monochrome
- [x] `AndroidManifest.xml` references `@mipmap/ic_launcher`
- [x] 5 obsolete files deleted
- [x] `play_store_512.png` present in `Resources/AppIcon/`
- [x] `dotnet build` → 0 errors
- [x] `dotnet test` → 209 / 209 green
- [x] On-device: full-color icon visible (home screen)
- [x] On-device: monochrome icon visible with themed icons enabled (not a white square)

## Status: ✅ COMPLETE (2026-06-04)
