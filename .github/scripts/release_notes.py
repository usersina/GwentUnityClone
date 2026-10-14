#!/usr/bin/env python3
"""The notes of the release: the newest section of CHANGELOG.md, then the downloads (with the files' checksums).

release_notes.py CHANGELOG.md                      checks there is a section, prints it
release_notes.py CHANGELOG.md <assets dir> <repo>  the full notes, downloads and checksums included
"""
import hashlib
import os
import re
import sys

# The archives GwentBuild makes, in the order of the table, with what a player does on first launch
PLATFORMS = [
    ("Windows", "GwentLegacy-Windows.zip",
     "Unzip, run `GwentLegacy.exe`. Windows warns about an unknown publisher once: **More info > Run anyway**."),
    ("macOS (Intel and Apple Silicon)", "GwentLegacy-macOS.zip",
     "Unzip, open `GwentLegacy.app`. macOS blocks it once: **System Settings > Privacy & Security > Open Anyway**."),
    ("macOS (Intel and Apple Silicon)", "GwentLegacy-macOS.tar.gz",
     "Open the archive, then `GwentLegacy.app`. macOS blocks it once: **System Settings > Privacy & Security > Open Anyway**."),
    ("Linux", "GwentLegacy-Linux.tar.gz", "Extract, run `GwentLegacy.x86_64`."),
    ("Android (8 and later, 64-bit)", "GwentLegacy-Android.apk",
     "Open the file on the phone and allow installing apps from this source."),
    ("Web", "GwentLegacy-Web.zip", "Unzip and serve the folder from any web server (opening `index.html` as a file does not work)."),
]


def newest_section(changelog):
    with open(changelog, encoding="utf-8") as f:
        text = f.read()
    match = re.search(r"^## (.+?)\n(.*?)(?=^## |\Z)", text, re.S | re.M)
    if not match or not match.group(2).strip():
        sys.exit("CHANGELOG.md has no section to release")
    return match.group(1).strip(), match.group(2).strip()


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    title, body = newest_section(sys.argv[1])
    if len(sys.argv) < 4:
        print(f"## {title}\n\n{body}")
        return
    assets, repo = sys.argv[2], sys.argv[3]
    files = set(os.listdir(assets))
    lines = [f"*{title}*", "", body, "", "## Downloads", "",
             "Free and unsigned: your system asks once whether to trust it.", "",
             "| Platform | Download | First launch |", "| --- | --- | --- |"]
    sums = []
    for platform, name, how in PLATFORMS:
        if name not in files:
            continue
        size = os.path.getsize(os.path.join(assets, name)) / 1048576
        link = f"https://github.com/{repo}/releases/latest/download/{name}"
        lines.append(f"| {platform} | [{name}]({link}) ({size:.0f} MB) | {how} |")
        sums.append(f"{sha256(os.path.join(assets, name))}  {name}")
    lines += ["", "These links always point to the newest release.", "", "<details><summary>SHA-256</summary>", "",
              "```", *sums, "```", "", "</details>"]
    print("\n".join(lines))


if __name__ == "__main__":
    main()
