# MetaEnricher — App Description

**MetaEnricher** is a native macOS application designed for photographers who want to automate metadata enrichment of their photo libraries using local AI — without sending any data to the cloud.

---

## Overview

MetaEnricher bridges the gap between shooting photos and publishing them. After a session, photographers need to add titles, descriptions, keywords, and copyright information to dozens of images. MetaEnricher automates this using a locally running vision AI model (via Ollama), analyzing each image and generating rich, contextual metadata — entirely on your Mac.

---

## Key Features

### AI-Powered Metadata Enrichment
Connects to a locally hosted Ollama instance running a vision model (such as Qwen2.5-VL). For each photo, the app generates a title, caption, keywords, and suggested location — all processed locally, your photos never leave your machine.

### Session-Based Workflow
Organizes your library by shooting sessions discovered automatically by date. Each session shows its edited export folder, letting you enrich only the images you intend to publish.

### Metadata Writing
Writes EXIF, IPTC, and XMP tags directly to image files for compatibility with Lightroom, Capture One, Photo Mechanic, and publishing platforms. Keywords are deduplicated, creator and copyright fields are pre-filled from your preferences.

### Smart Location Handling
GPS coordinates are reverse-geocoded to city/country names via OpenStreetMap. If no GPS data exists, the AI guesses location from visual content. Session notes let you provide context — e.g. "Shot in Tuscany" — to improve accuracy.

### Bulk Enrichment & SD Card Import
Select one photo or many for batch processing. The built-in SD card importer scans for DCIM folders and organizes files into the correct library structure ready for enrichment.

### Privacy-First
No API keys, no uploads, no cloud. All AI inference runs locally via Ollama. The app works entirely offline after setup.

---

## Who It's For

Independent photographers and content creators who manage local photo archives and want a fast, private, AI-assisted way to prepare images for publication — without subscriptions or repetitive manual work.

---

*Requires macOS 26 or later. Requires Ollama running locally with a compatible vision model installed.*
