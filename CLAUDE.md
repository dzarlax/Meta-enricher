# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project structure

```
Meta enricher/
├── macOS/      ← Native macOS app (Swift/SwiftUI), primary version
├── Windows/    ← Future Windows version (not yet created)
```

---

## macOS app (`macOS/MetaEnricher/`)

Built with Swift + SwiftUI using `@Observable`. Targets macOS 26+. Open `MetaEnricher.xcodeproj` in Xcode to build and run — there is no CLI build command.

### Architecture

| Layer | Files | Purpose |
|-------|-------|---------|
| App entry | `App.swift` | Routes between `OnboardingView` and `ContentView` based on `hasCompletedOnboarding` |
| State | `Models/AppState.swift` | Single `@Observable` source of truth; holds sessions, photos, UI state, settings |
| Models | `Models/Photo.swift` | `Photo` and `PhotoSession` value types |
| Services | `Services/` | Stateless actors/classes: `PhotoScanner`, `OllamaService`, `ExifService`, `GeocodingService`, `ImportService`, `ThumbnailService` |
| Views | `Views/` | SwiftUI views; inject `AppState` via `@Environment` |

**Sandbox & file access**: The app is sandboxed (`com.apple.security.app-sandbox`). User-selected folders are persisted via security-scoped bookmarks (`AppState.saveBookmark` / `restoreBookmark`) — never plain paths.

**AI enrichment flow**: `OllamaService` resizes photo → base64 → POST to local Ollama → parse JSON → `ExifService` writes IPTC/XMP/legacy tags. Location priority: embedded GPS → reverse geocode (Nominatim) → AI guess.

**Onboarding**: Multi-step wizard in `OnboardingView`. Supports two library schemas — `metaEnricher` (fixed subfolder names) and `custom` (user-defined subfolder name).

### Photo library structure (metaEnricher schema)

```
CAMERA_ROOT/
  <year>/
    <date> [label]/    ← "session"
      Edited export/   ← edited JPEGs (what the UI browses)
      JPEG/
      RAW/
```
