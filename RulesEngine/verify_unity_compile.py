#!/usr/bin/env python3
"""Compile shared sources with the installed Unity compiler and its .NET Standard refs.

This writes only into a temporary directory. It is compiler evidence, not an Editor
import, player build, or Play Mode test. Pass --editor for another installation.
"""
import argparse
from pathlib import Path
import subprocess
import tempfile


def main():
    repo = Path(__file__).resolve().parent.parent
    version = (repo / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()[0].split(": ", 1)[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, default=Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app"))
    args = parser.parse_args()
    scripting = args.editor / "Contents/Resources/Scripting"
    compiler = sorted((scripting / "DotNetSdk/sdk").glob("*/Roslyn/bincore/csc.dll"))
    refs = sorted((scripting / "NetStandard/ref/2.1.0").glob("*.dll"))
    runtime = scripting / "NetCoreRuntime/dotnet"
    sources = sorted((repo / "Assets/Scripts/RulesEngine").rglob("*.cs"))
    if not compiler or not refs or not runtime.exists() or not sources:
        parser.error("Expected Unity compiler, runtime, .NET Standard 2.1 references, and shared sources")
    with tempfile.TemporaryDirectory(prefix="train-rules-unity-compile-") as scratch:
        output = Path(scratch) / "TrainDominoesRulesEngine.dll"
        response = Path(scratch) / "compile.rsp"
        lines = ["-target:library", "-langversion:9.0", "-nostdlib+", "-deterministic+", "-warnaserror+",
                 f'-out:"{output}"']
        lines += [f'-r:"{ref}"' for ref in refs]
        lines += [f'"{source}"' for source in sources]
        response.write_text("\n".join(lines) + "\n")
        result = subprocess.run([str(runtime), str(compiler[-1]), "@" + str(response)], check=False)
        if result.returncode:
            return result.returncode
        print(f"Unity {version}: compiled {len(sources)} core source files with C# 9 and only .NET Standard 2.1 references.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
