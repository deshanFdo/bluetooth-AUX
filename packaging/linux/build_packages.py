#!/usr/bin/env python3
"""Build AudioSwitch-linux-x64.tar.gz and audioswitch_1.3.0_amd64.deb on Windows/Linux."""

from __future__ import annotations

import hashlib
import io
import math
import os
import tarfile
import time
from pathlib import Path

VERSION = "1.3.0"
PACKAGE_NAME = "audioswitch"
ARCH = "amd64"

ROOT = Path(__file__).resolve().parents[2]
DIST_DIR = ROOT / "dist-linux"
BIN_PATH = DIST_DIR / "AudioSwitch"
ICON_PATH = ROOT / "src" / "AudioSwitch.Linux" / "Assets" / "AudioSwitch.ico"
INSTALL_SH = Path(__file__).resolve().parent / "install.sh"
UNINSTALL_SH = Path(__file__).resolve().parent / "uninstall.sh"

TAR_OUT = DIST_DIR / f"AudioSwitch-linux-x64.tar.gz"
DEB_OUT = DIST_DIR / f"{PACKAGE_NAME}_{VERSION}_{ARCH}.deb"


def _lf_bytes(path: Path) -> bytes:
    return path.read_bytes().replace(b"\r\n", b"\n")


def _add_tar_bytes(
    tf: tarfile.TarFile,
    arcname: str,
    data: bytes,
    mode: int,
    mtime: int,
) -> None:
    info = tarfile.TarInfo(name=arcname)
    info.size = len(data)
    info.mode = mode
    info.uid = 0
    info.gid = 0
    info.uname = "root"
    info.gname = "root"
    info.mtime = mtime
    tf.addfile(info, io.BytesIO(data))


def _add_tar_dir(tf: tarfile.TarFile, arcname: str, mtime: int) -> None:
    info = tarfile.TarInfo(name=arcname)
    info.type = tarfile.DIRTYPE
    info.mode = 0o755
    info.uid = 0
    info.gid = 0
    info.uname = "root"
    info.gname = "root"
    info.mtime = mtime
    tf.addfile(info)


def build_tar_gz(binary_bytes: bytes, icon_bytes: bytes, mtime: int) -> None:
    install_bytes = _lf_bytes(INSTALL_SH)
    uninstall_bytes = _lf_bytes(UNINSTALL_SH)
    readme_bytes = (
        f"WaveDock v{VERSION} (Linux x64)\n\n"
        "Quick User Install (no sudo):\n"
        "  chmod +x install.sh AudioSwitch\n"
        "  ./install.sh\n\n"
        "Uninstall:\n"
        "  ./uninstall.sh\n"
        "  # or ~/.local/share/AudioSwitch/AudioSwitch --uninstall\n"
    ).encode("utf-8")

    with tarfile.open(TAR_OUT, "w:gz", format=tarfile.GNU_FORMAT) as tf:
        _add_tar_bytes(tf, "AudioSwitch-linux-x64/AudioSwitch", binary_bytes, 0o755, mtime)
        _add_tar_bytes(tf, "AudioSwitch-linux-x64/install.sh", install_bytes, 0o755, mtime)
        _add_tar_bytes(tf, "AudioSwitch-linux-x64/uninstall.sh", uninstall_bytes, 0o755, mtime)
        _add_tar_bytes(tf, "AudioSwitch-linux-x64/AudioSwitch.ico", icon_bytes, 0o644, mtime)
        _add_tar_bytes(tf, "AudioSwitch-linux-x64/README.txt", readme_bytes, 0o644, mtime)


def _ar_member(name: str, data: bytes, mtime: int) -> bytes:
    header = (
        f"{name:<16}"
        f"{mtime:<12}"
        f"{0:<6}"
        f"{0:<6}"
        f"{100644:<8}"
        f"{len(data):<10}"
        "`\n"
    ).encode("ascii")
    assert len(header) == 60
    padding = b"\n" if len(data) % 2 == 1 else b""
    return header + data + padding


def build_deb(binary_bytes: bytes, icon_bytes: bytes, mtime: int) -> None:
    launcher_script = (
        "#!/usr/bin/env bash\n"
        "exec /usr/lib/audioswitch/AudioSwitch \"$@\"\n"
    ).encode("utf-8")

    desktop_file = (
        "[Desktop Entry]\n"
        "Type=Application\n"
        "Name=WaveDock\n"
        "Comment=WaveDock Dynamic Island Audio Switcher, Split Router, Spotify/YT Music & Battery Monitor\n"
        "Exec=/usr/bin/audioswitch\n"
        "Icon=/usr/share/pixmaps/audioswitch.ico\n"
        "Terminal=false\n"
        "Categories=AudioVideo;Audio;Utility;\n"
        "Keywords=audio;bluetooth;aux;pipewire;pulseaudio;spotify;youtube;\n"
        "StartupNotify=false\n"
    ).encode("utf-8")

    data_buf = io.BytesIO()
    with tarfile.open(fileobj=data_buf, mode="w:gz", format=tarfile.GNU_FORMAT) as tf:
        for d in [
            "./",
            "./usr/",
            "./usr/bin/",
            "./usr/lib/",
            "./usr/lib/audioswitch/",
            "./usr/share/",
            "./usr/share/applications/",
            "./usr/share/pixmaps/",
        ]:
            _add_tar_dir(tf, d, mtime)
        _add_tar_bytes(tf, "./usr/lib/audioswitch/AudioSwitch", binary_bytes, 0o755, mtime)
        _add_tar_bytes(tf, "./usr/bin/audioswitch", launcher_script, 0o755, mtime)
        _add_tar_bytes(tf, "./usr/share/applications/audioswitch.desktop", desktop_file, 0o644, mtime)
        _add_tar_bytes(tf, "./usr/share/pixmaps/audioswitch.ico", icon_bytes, 0o644, mtime)
    data_tar_gz = data_buf.getvalue()

    total_unpacked_bytes = (
        len(binary_bytes) + len(launcher_script) + len(desktop_file) + len(icon_bytes)
    )
    installed_kb = max(1, math.ceil(total_unpacked_bytes / 1024))

    control_txt = (
        f"Package: {PACKAGE_NAME}\n"
        f"Version: {VERSION}\n"
        f"Section: sound\n"
        f"Priority: optional\n"
        f"Architecture: {ARCH}\n"
        f"Installed-Size: {installed_kb}\n"
        f"Maintainer: Deshan Fernando <deshan@users.noreply.github.com>\n"
        f"Homepage: https://audio-switch.vercel.app\n"
        f"Depends: libfontconfig1, libx11-6, libice6, libsm6, pulseaudio-utils, libglib2.0-bin\n"
        f"Recommends: pipewire-pulse, bluez, wmctrl\n"
        f"Description: WaveDock — Dynamic Island Audio Switcher & Split Router for Ubuntu/Debian\n"
        f" Top-of-screen Dynamic Island for PipeWire/PulseAudio switching, per-app\n"
        f" split routing, Spotify & YouTube Music MPRIS control, and connected\n"
        f" Bluetooth headset battery rings.\n"
    ).encode("utf-8")

    postinst_sh = (
        "#!/usr/bin/env bash\n"
        "set -e\n"
        "if command -v update-desktop-database >/dev/null 2>&1; then\n"
        "  update-desktop-database /usr/share/applications >/dev/null 2>&1 || true\n"
        "fi\n"
    ).encode("utf-8")

    prerm_sh = (
        "#!/usr/bin/env bash\n"
        "set -e\n"
        "pkill -f '/usr/lib/audioswitch/AudioSwitch' >/dev/null 2>&1 || true\n"
    ).encode("utf-8")

    postrm_sh = (
        "#!/usr/bin/env bash\n"
        "set -e\n"
        "if [ \"$1\" = \"purge\" ]; then\n"
        "  for home_dir in /home/* /root; do\n"
        "    if [ -d \"$home_dir\" ]; then\n"
        "      rm -rf \"$home_dir/.config/AudioSwitch\" \"$home_dir/.local/share/AudioSwitch\"\n"
        "      rm -f \"$home_dir/.config/autostart/audioswitch.desktop\" \"$home_dir/.local/share/applications/audioswitch.desktop\"\n"
        "    fi\n"
        "  done\n"
        "fi\n"
        "if command -v update-desktop-database >/dev/null 2>&1; then\n"
        "  update-desktop-database /usr/share/applications >/dev/null 2>&1 || true\n"
        "fi\n"
    ).encode("utf-8")

    control_buf = io.BytesIO()
    with tarfile.open(fileobj=control_buf, mode="w:gz", format=tarfile.GNU_FORMAT) as tf:
        _add_tar_dir(tf, "./", mtime)
        _add_tar_bytes(tf, "./control", control_txt, 0o644, mtime)
        _add_tar_bytes(tf, "./postinst", postinst_sh, 0o755, mtime)
        _add_tar_bytes(tf, "./prerm", prerm_sh, 0o755, mtime)
        _add_tar_bytes(tf, "./postrm", postrm_sh, 0o755, mtime)
    control_tar_gz = control_buf.getvalue()

    debian_binary = b"2.0\n"
    ar_bytes = (
        b"!<arch>\n"
        + _ar_member("debian-binary", debian_binary, mtime)
        + _ar_member("control.tar.gz", control_tar_gz, mtime)
        + _ar_member("data.tar.gz", data_tar_gz, mtime)
    )
    DEB_OUT.write_bytes(ar_bytes)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def main() -> None:
    if not BIN_PATH.exists():
        raise FileNotFoundError(f"Missing published Linux binary: {BIN_PATH}")

    binary_bytes = BIN_PATH.read_bytes()
    icon_bytes = ICON_PATH.read_bytes()
    mtime = int(time.time())

    build_tar_gz(binary_bytes, icon_bytes, mtime)
    build_deb(binary_bytes, icon_bytes, mtime)

    for artifact in (DEB_OUT, TAR_OUT):
        size_mb = artifact.stat().st_size / (1024 * 1024)
        print(f"{artifact.name}: {artifact.stat().st_size} bytes ({size_mb:.1f} MB)")
        print(f"  SHA-256: {sha256_file(artifact)}")


if __name__ == "__main__":
    main()
