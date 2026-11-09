import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description="Build and launch Junna.")
    parser.add_argument("--skip-build", action="store_true", help="Launch the existing package.")
    parser.add_argument("--core-package", help="Core distribution directory.")
    parser.add_argument("--godot", help="MegaDot console executable.")
    args = parser.parse_args()

    candidates = [
        shutil.which("pwsh.exe"),
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "PowerShell/7/pwsh.exe",
        Path.home() / ".cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe",
    ]
    powershell = next((Path(path) for path in candidates if path and Path(path).is_file()), None)
    if powershell is None:
        parser.error("PowerShell 7 is required. Install it or add pwsh.exe to PATH.")

    root = Path(__file__).resolve().parent
    command = [str(powershell), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root / "play.ps1")]
    if args.skip_build:
        command.append("-SkipBuild")
    if args.core_package:
        command.extend(["-CorePackage", args.core_package])
    if args.godot:
        command.extend(["-Godot", args.godot])
    return subprocess.run(command, cwd=root).returncode


if __name__ == "__main__":
    sys.exit(main())
