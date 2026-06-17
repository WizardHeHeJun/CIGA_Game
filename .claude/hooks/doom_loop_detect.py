#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Doom-loop 检测：同一文件被反复编辑时预警（非阻断）。

PostToolUse(Edit|Write)。达 5 次首警，之后每 +3 次再警。
反复改同一文件常意味着思路卡死——提示换策略（读文档/换方法/问用户）。
"""
import sys, os, io, json

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
STATE_DIR = os.path.join(PROJECT, ".claude", ".state")

try:
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass


def main():
    try:
        ev = json.load(sys.stdin)
    except Exception:
        sys.exit(0)
    ti = ev.get("tool_input", {}) or {}
    fp = ti.get("file_path") or ti.get("filePath")
    if not fp:
        sys.exit(0)
    session = str(ev.get("session_id") or "default")
    state_path = os.path.join(STATE_DIR, f"editcount-{session}.json")

    try:
        with open(state_path, "r", encoding="utf-8") as f:
            counts = json.load(f)
    except Exception:
        counts = {}

    key = fp.replace("\\", "/")
    counts[key] = int(counts.get(key, 0)) + 1
    n = counts[key]

    try:
        os.makedirs(STATE_DIR, exist_ok=True)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(counts, f)
    except Exception:
        pass

    warn = (n == 5) or (n > 5 and (n - 5) % 3 == 0)
    if not warn:
        sys.exit(0)

    name = os.path.basename(fp)
    msg = (
        f"[doom-loop] {name} 本会话已被编辑 {n} 次。"
        "若在反复试错，考虑换策略：先读相关模块文档 / 用验证桥确认真实报错 / "
        "换实现思路 / 必要时停下问用户。"
    )
    out = {"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": msg}}
    sys.stdout.write(json.dumps(out, ensure_ascii=False))
    sys.exit(0)


if __name__ == "__main__":
    main()
