#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""编译回退：Unity 不可用 / AiBridge 没起时，用 dotnet 对 Unity 生成的 .sln 做语法级编译粗检。

注意：这只是粗检——Unity 的 .csproj 由 IDE 集成生成，可能缺失或与 Unity 实际编译有差异。
权威编译结果以 Unity（bridge.py compile）为准。Unity 没生成 .sln 时本脚本无能为力。
"""
import os, sys, glob, subprocess

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


def main():
    slns = glob.glob(os.path.join(PROJECT, "*.sln"))
    if not slns:
        print("未找到 .sln（Unity 尚未生成 IDE 工程）。请用 bridge.py compile，或在 Unity 中"
              "Edit/Preferences/External Tools 生成 .csproj 后重试。", file=sys.stderr)
        sys.exit(3)
    sln = slns[0]
    print(f"dotnet build {os.path.basename(sln)} （语法级粗检，权威结果以 Unity 为准）……")
    try:
        r = subprocess.run(["dotnet", "build", sln, "-nologo", "-clp:ErrorsOnly"],
                           cwd=PROJECT, capture_output=True, text=True, timeout=300)
        sys.stdout.write(r.stdout)
        sys.stderr.write(r.stderr)
        sys.exit(r.returncode)
    except FileNotFoundError:
        print("未找到 dotnet。", file=sys.stderr)
        sys.exit(3)
    except subprocess.TimeoutExpired:
        print("dotnet build 超时。", file=sys.stderr)
        sys.exit(3)


if __name__ == "__main__":
    main()
