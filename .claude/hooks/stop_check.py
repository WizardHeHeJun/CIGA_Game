#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Stop：任务结束前的 sanity check（advisory，不阻断）。

检查工作区改动里是否残留 [DEBUG] 临时埋点（应在收尾前清除）。
仅提示，不阻止停止，避免死循环。
"""
import sys, os, io, json, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))

try:
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass


def git(args):
    try:
        return subprocess.run(
            ["git"] + args, cwd=PROJECT, capture_output=True, text=True, timeout=20
        ).stdout
    except Exception:
        return ""


def main():
    try:
        json.load(sys.stdin)
    except Exception:
        pass

    diff = git(["diff", "--unified=0"])
    hits = []
    for line in diff.splitlines():
        if line.startswith("+") and not line.startswith("+++") and "[DEBUG]" in line:
            hits.append(line[1:].strip())

    if not hits:
        sys.exit(0)

    sample = "\n".join("    " + h for h in hits[:5])
    sys.stderr.write(
        f"[stop-check] 检测到 {len(hits)} 处新增 [DEBUG] 埋点，提交前请清除：\n{sample}\n"
    )
    sys.exit(0)  # advisory，不阻断


if __name__ == "__main__":
    main()
