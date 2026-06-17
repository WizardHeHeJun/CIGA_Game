#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PreCompact：压缩前保存工作进度，压缩后注入恢复。

把当前 git 改动摘要写入状态文件，并作为 additionalContext 传递，
让上下文压缩后仍知道"手上改了哪些文件"。
"""
import sys, os, io, json, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
STATE_DIR = os.path.join(PROJECT, ".claude", ".state")

try:
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass


def git(args):
    try:
        return subprocess.run(
            ["git"] + args, cwd=PROJECT, capture_output=True, text=True, timeout=20
        ).stdout.strip()
    except Exception:
        return ""


def main():
    try:
        json.load(sys.stdin)
    except Exception:
        pass

    status = git(["status", "--short"])
    stat = git(["diff", "--stat"])
    if not status and not stat:
        sys.exit(0)

    body = "[压缩前进度快照]\n"
    if status:
        body += "git status --short:\n" + status + "\n"
    if stat:
        body += "git diff --stat:\n" + stat + "\n"

    try:
        os.makedirs(STATE_DIR, exist_ok=True)
        with open(os.path.join(STATE_DIR, "precompact-snapshot.txt"), "w", encoding="utf-8") as f:
            f.write(body)
    except Exception:
        pass

    out = {"hookSpecificOutput": {"hookEventName": "PreCompact", "additionalContext": body}}
    sys.stdout.write(json.dumps(out, ensure_ascii=False))
    sys.exit(0)


if __name__ == "__main__":
    main()
