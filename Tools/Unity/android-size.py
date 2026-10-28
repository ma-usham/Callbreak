"""Prints the sizes android-size.sh measured: the universal APK by part, the App Bundle, and what Play
sends one phone (bundletool's get-size for an ARM64 and a 32-bit device).
Usage: python Tools/Unity/android-size.py Builds/Size/<label>
"""
import csv
import os
import sys
import zipfile

MB = 1000 * 1000
MIB = 1024 * 1024


def part(name):
    if name.startswith('assets/bin/Data/'):
        return 'game data (assets/bin/Data)'
    if name.startswith('lib/arm64-v8a/'):
        return 'ARM64 native libraries'
    if name.startswith('lib/armeabi-v7a/'):
        return 'ARMv7 native libraries'
    if name.endswith('.dex'):
        return 'Java/Kotlin code (dex)'
    return 'everything else'


def main(folder):
    apk = os.path.join(folder, 'Callbreak.apk')
    aab = os.path.join(folder, 'Callbreak.aab')
    parts = {}
    with zipfile.ZipFile(apk) as z:
        for info in z.infolist():
            zipped, raw = parts.get(part(info.filename), (0, 0))
            parts[part(info.filename)] = (zipped + info.compress_size, raw + info.file_size)
    print(f'{"universal APK":34s} {os.path.getsize(apk) / MB:8.2f} MB')
    for name in sorted(parts, key=lambda n: -parts[n][0]):
        zipped, raw = parts[name]
        print(f'  {name:32s} {zipped / MIB:8.2f} MiB zipped {raw / MIB:8.2f} MiB raw')
    print(f'{"App Bundle (.aab)":34s} {os.path.getsize(aab) / MB:8.2f} MB')
    for abi in ('arm64-v8a', 'armeabi-v7a'):
        path = os.path.join(folder, f'download-{abi}.csv')
        try:
            with open(path, newline='') as f:
                rows = list(csv.reader(f))
            low, high = int(rows[1][0]), int(rows[1][1])
            print(f'{"Play download, " + abi + " phone":34s} {low / MB:8.2f} - {high / MB:.2f} MB')
        except (OSError, IndexError, ValueError):
            print(f'Play download, {abi}: not measured ({path})')


if __name__ == '__main__':
    main(sys.argv[1])
