#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""doc-first 门禁（advisory）：触碰某业务模块代码前，若该模块有文档则提示先读。

PreToolUse(Edit|Write|Read)。仅当 Assets/Scripts/<模块>/ 存在对应
ai-docs/modules/<模块>/_knowledge-index.md 时给出建议；无模块文档则静默降级（不阻断）。
每会话每模块只提示一次。
"""
import sys, os, io, json, re

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
MODULES_DIR = os.path.join(PROJECT, "ai-docs", "modules")
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
    fp = (ti.get("file_path") or ti.get("filePath") or "").replace("\\", "/")
    if not fp:
        sys.exit(0)

    m = re.search(r"/Assets/Scripts/([^/]+)/", fp, re.IGNORECASE)
    if not m:
        sys.exit(0)
    module = m.group(1)

    index = os.path.join(MODULES_DIR, module.lower(), "_knowledge-index.md")
    if not os.path.isfile(index):
        # 模块暂无文档：优雅降级，不阻断也不提示
        sys.exit(0)

    session = str(ev.get("session_id") or "default")
    state_path = os.path.join(STATE_DIR, f"docfirst-{session}.json")
    try:
        with open(state_path, "r", encoding="utf-8") as f:
            seen = set(json.load(f))
    except Exception:
        seen = set()
    if module.lower() in seen:
        sys.exit(0)
    seen.add(module.lower())
    try:
        os.makedirs(STATE_DIR, exist_ok=True)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(sorted(seen), f)
    except Exception:
        pass

    rel = os.path.relpath(index, PROJECT).replace("\\", "/")
    msg = f"[doc-first] 即将接触模块「{module}」。建议先读 {rel} 与其三件套，再动代码。"
    out = {"hookSpecificOutput": {"hookEventName": "PreToolUse", "additionalContext": msg}}
    sys.stdout.write(json.dumps(out, ensure_ascii=False))
    sys.exit(0)


if __name__ == "__main__":
    main()
