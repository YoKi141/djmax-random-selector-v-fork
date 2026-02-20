#!/usr/bin/env python3
"""
DJMAX Random Selector V - AppData Generator Script

Purpose:
  1. Download the latest AllTrackList.json from V-Archive
  2. Detect new DLC categories not yet in appdata.json
  3. Report tracks with non-ASCII titles (Korean/Japanese) that may need
     an entry in the englishTitles mapping for English-mode navigation
  4. Update appdata.json while preserving all existing manual configuration

Usage:
  python generate_appdata.py [options]

Options:
  --data-dir PATH   Path to DMRSV3_Data directory
  --no-download     Skip downloading; use existing AllTrackList.json
  --dry-run         Print report only; do not write any files

Maintenance workflow:
  1. Run this script  ->  AllTrackList.json is updated, new DLCs are added
  2. Manually add English titles for newly reported tracks
  3. Proofread/verify translations
  4. Add any new UI as needed
  5. Release
"""

import json
import urllib.request
import os
import sys
import io
import argparse
from datetime import datetime

# Force UTF-8 output on Windows so non-ASCII characters print correctly
if sys.stdout.encoding and sys.stdout.encoding.lower() not in ("utf-8", "utf8"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

# V-Archive track database URL
SONGS_URL = "https://v-archive.net/db/songs.json"

# Script location is <project_root>/scripts/generate_appdata.py
# Data dir defaults to <project_root>/DjmaxRandomSelectorV/DMRSV3_Data
_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_DATA_DIR = os.path.join(_SCRIPT_DIR, "..", "DjmaxRandomSelectorV", "DMRSV3_Data")


# ---------------------------------------------------------------------------
# Unicode helpers
# ---------------------------------------------------------------------------

def _has_korean(text: str) -> bool:
    for c in text:
        cp = ord(c)
        if (0xAC00 <= cp <= 0xD7A3  # Hangul syllables
                or 0x3131 <= cp <= 0x314E  # Hangul Jamo consonants
                or 0x3141 <= cp <= 0x3163):  # Hangul Jamo vowels
            return True
    return False


def _has_japanese(text: str) -> bool:
    for c in text:
        cp = ord(c)
        if (0x3040 <= cp <= 0x309F  # Hiragana
                or 0x30A0 <= cp <= 0x30FF  # Katakana
                or 0x4E00 <= cp <= 0x9FFF):  # CJK Unified Ideographs (Kanji)
            return True
    return False


def _is_non_ascii(text: str) -> bool:
    return any(ord(c) > 127 for c in text)


def _lang_tag(name: str) -> str:
    if _has_korean(name):
        return "KO"
    if _has_japanese(name):
        return "JA"
    return "OTHER"


# ---------------------------------------------------------------------------
# Core functions
# ---------------------------------------------------------------------------

def download_songs(dest_path: str) -> list:
    """Download songs.json from V-Archive and save to *dest_path*."""
    print(f"Downloading track list from {SONGS_URL} ...")
    try:
        req = urllib.request.Request(
            SONGS_URL,
            headers={"User-Agent": "djmax-random-selector-v/generate_appdata.py"}
        )
        with urllib.request.urlopen(req, timeout=30) as resp:
            content = resp.read().decode("utf-8")
    except Exception as exc:
        print(f"ERROR: Download failed: {exc}")
        sys.exit(1)

    tracks = json.loads(content)
    with open(dest_path, "w", encoding="utf-8") as fh:
        json.dump(tracks, fh, ensure_ascii=False, indent=4)
    print(f"Saved {len(tracks)} tracks to {dest_path}")
    return tracks


def load_json(path: str) -> object:
    # utf-8-sig strips BOM if present, falls back to plain utf-8
    with open(path, "r", encoding="utf-8-sig") as fh:
        return json.load(fh)


def save_json(data: object, path: str):
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=4)
    print(f"Saved {path}")


def analyze_tracks(tracks: list) -> dict:
    """
    Return a dict with:
      dlcs  : {dlcCode: {"dlc": fullName, "count": int}}
      non_ascii : [(id, name, langTag)]   -- tracks with non-ASCII names
    """
    dlcs: dict = {}
    non_ascii: list = []

    for track in tracks:
        tid = track.get("title")
        name = track.get("name", "")
        code = track.get("dlcCode", "")
        dlc_name = track.get("dlc", "")

        if code:
            entry = dlcs.setdefault(code, {"dlc": dlc_name, "count": 0})
            entry["count"] += 1

        if _is_non_ascii(name):
            non_ascii.append((tid, name, _lang_tag(name)))

    return {"dlcs": dlcs, "non_ascii": non_ascii}


def find_new_categories(appdata: dict, dlcs: dict) -> list:
    """
    Compare DLC codes from AllTrackList with existing categories.
    Return a list of new category dicts (already appended to appdata).
    """
    existing_ids = {cat["id"] for cat in appdata.get("categories", [])}
    added = []
    for code, info in sorted(dlcs.items()):
        if code not in existing_ids:
            new_cat = {
                "name": info["dlc"],  # use game's DLC name as placeholder
                "id": code,
                "steamId": None,
                "type": 0,  # default Regular; verify manually
            }
            appdata["categories"].append(new_cat)
            added.append(new_cat)
    return added


def find_missing_translations(appdata: dict, non_ascii: list, title_key: str) -> list:
    """
    Return tracks whose names are non-ASCII but have no entry in the given titles dict.
    title_key: 'englishTitles' or 'japaneseTitles'
    """
    existing = {int(k) for k in appdata.get(title_key, {}).keys()}
    return [(tid, name, lang) for tid, name, lang in non_ascii if tid not in existing]


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Update AllTrackList.json and generate/patch appdata.json"
    )
    parser.add_argument(
        "--data-dir",
        default=DEFAULT_DATA_DIR,
        help="Path to DMRSV3_Data directory",
    )
    parser.add_argument(
        "--no-download",
        action="store_true",
        help="Skip downloading AllTrackList.json; use existing file",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print report only; do not write any files",
    )
    args = parser.parse_args()

    data_dir = os.path.abspath(args.data_dir)
    track_path = os.path.join(data_dir, "AllTrackList.json")
    appdata_path = os.path.join(data_dir, "appdata.json")

    print("=" * 60)
    print("DJMAX Random Selector V - AppData Generator")
    print(f"Timestamp : {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Data dir  : {data_dir}")
    if args.dry_run:
        print("[DRY RUN - no files will be written]")
    print("=" * 60)
    print()

    # 1. Download / load AllTrackList.json
    if args.no_download:
        if not os.path.exists(track_path):
            print(f"ERROR: {track_path} not found. Remove --no-download to fetch it.")
            sys.exit(1)
        tracks = load_json(track_path)
        print(f"Loaded {len(tracks)} tracks from existing file (--no-download).")
    else:
        tracks = download_songs(track_path)
    print()

    # 2. Load appdata.json
    if not os.path.exists(appdata_path):
        print(f"WARNING: {appdata_path} not found. Starting from a minimal template.")
        appdata = {
            "categoryType": ["Regular", "CollabMusicGame", "CollabVariety", "PLI"],
            "basicCategories": ["R", "RV", "P1", "P2", "GG"],
            "categories": [],
            "pliCategories": [],
            "linkDisc": [],
            "englishTitles": {},
            "japaneseTitles": {},
        }
    else:
        appdata = load_json(appdata_path)
        # Ensure japaneseTitles key exists for older appdata.json files
        appdata.setdefault("japaneseTitles", {})

    # 3. Analyze tracks
    print("Analyzing tracks ...")
    result = analyze_tracks(tracks)
    dlcs = result["dlcs"]
    non_ascii = result["non_ascii"]
    print(f"  Total tracks      : {len(tracks)}")
    print(f"  DLC codes found   : {len(dlcs)}")
    print(f"  Non-ASCII names   : {len(non_ascii)}")
    print()

    # 4. New categories
    print("Checking for new DLC categories ...")
    new_cats = find_new_categories(appdata, dlcs)
    if new_cats:
        for cat in new_cats:
            print(f"  + {cat['id']:8s}  name={cat['name']!r}  type=0  [verify steamId & type manually]")
    else:
        print("  No new categories.")
    print()

    # 5a. Missing English titles
    print("Checking for missing englishTitles entries ...")
    missing_en = find_missing_translations(appdata, non_ascii, "englishTitles")
    if missing_en:
        print(f"  {len(missing_en)} track(s) need an entry in englishTitles:")
        for tid, name, lang in sorted(missing_en, key=lambda x: x[0]):
            print(f"    [{lang}] ID {tid:4d}  {name}")
    else:
        print("  All non-ASCII tracks are covered in englishTitles.")
    print()

    # 5b. Missing Japanese titles
    print("Checking for missing japaneseTitles entries ...")
    missing_ja = find_missing_translations(appdata, non_ascii, "japaneseTitles")
    if missing_ja:
        print(f"  {len(missing_ja)} track(s) need an entry in japaneseTitles:")
        for tid, name, lang in sorted(missing_ja, key=lambda x: x[0]):
            print(f"    [{lang}] ID {tid:4d}  {name}")
    else:
        print("  All non-ASCII tracks are covered in japaneseTitles.")
    print()

    # 6. Save
    if not args.dry_run:
        save_json(appdata, appdata_path)
        print()

    # 7. Summary
    print("=" * 60)
    print("Summary")
    print(f"  New categories added        : {len(new_cats)}")
    print(f"  Missing englishTitles       : {len(missing_en)}")
    print(f"  Missing japaneseTitles      : {len(missing_ja)}")
    if missing_en:
        print()
        print("  ACTION REQUIRED: add the following to englishTitles in appdata.json")
        print('  (key = track ID as string, value = English title for navigation)')
        print()
        for tid, name, lang in sorted(missing_en, key=lambda x: x[0]):
            print(f'    "{tid}": "",  // [{lang}] {name}')
    if missing_ja:
        print()
        print("  ACTION REQUIRED: add the following to japaneseTitles in appdata.json")
        print('  (key = track ID as string, value = Japanese title for navigation)')
        print()
        for tid, name, lang in sorted(missing_ja, key=lambda x: x[0]):
            print(f'    "{tid}": "",  // [{lang}] {name}')
    print("=" * 60)


if __name__ == "__main__":
    main()
