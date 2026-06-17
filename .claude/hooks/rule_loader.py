#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""规则按 glob 自动加载（知识层→策略层注入）。

PreToolUse(Edit|Write|Read) 触发：根据目标文件路径，匹配 .claude/rules/*.md 的
frontmatter（alwaysApply / globs），把命中的规则正文作为 additionalContext 注入。
每个会话内同一规则只注入一次（状态存 .claude/.state/）。

注：本 hook 只注入上下文（additionalContext），不改变权限决策——不会绕过用户的工具批准流程。
"""
import sys, os, io, json, re, glob

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))  # .claude/hooks -> .claude -> project
RULES_DIR = os.path.join(PROJECT, ".claude", "rules")
STATE_DIR = os.path.join(PROJECT, ".claude", ".state")

try:
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass


def read_event():
    try:
        return json.load(sys.stdin)
    except Exception:
        return {}


def parse_frontmatter(text):
    if not text.startswith("---"):
        return {}, text
    end = text.find("\n---", 3)
    if end == -1:
        return {}, text
    fm_raw = text[3:end]
    body = text[end + 4:].lstrip("\n")
    fm = {}
    for line in fm_raw.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        k, v = line.split(":", 1)
        fm[k.strip()] = v.strip()
    return fm, body


def parse_globs(v):
    if not v:
        return []
    try:
        arr = json.loads(v)
        if isinstance(arr, list):
            return [str(x) for x in arr]
    except Exception:
        pass
    return []


def glob_to_regex(pattern):
    p = pattern.replace("\\", "/").lower()
    out = []
    i = 0
    while i < len(p):
        c = p[i]
        if p[i:i + 3] == "**/":
            out.append("(?:.*/)?")
            i += 3
        elif p[i:i + 2] == "**":
            out.append(".*")
            i += 2
        elif c == "*":
            out.append("[^/]*")
            i += 1
        elif c == "?":
            out.append(".")
            i += 1
        else:
            out.append(re.escape(c))
            i += 1
    return re.compile(r"(?:^|/)" + "".join(out) + r"$")


def target_path(ev):
    ti = ev.get("tool_input", {}) or {}
    return ti.get("file_path") or ti.get("filePath") or ""


def load_injected(state_path):
    try:
        with open(state_path, "r", encoding="utf-8") as f:
            return set(json.load(f))
    except Exception:
        return set()


def save_injected(state_path, s):
    try:
        os.makedirs(STATE_DIR, exist_ok=True)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(sorted(s), f)
    except Exception:
        pass


def main():
    ev = read_event()
    session = str(ev.get("session_id") or "default")
    state_path = os.path.join(STATE_DIR, f"rules-{session}.json")

    fp = target_path(ev).replace("\\", "/").lower()

    rule_files = sorted(glob.glob(os.path.join(RULES_DIR, "*.md")))
    if not rule_files:
        sys.exit(0)

    injected = load_injected(state_path)
    to_emit = []  # (rule_id, body)

    for rf in rule_files:
        rid = os.path.splitext(os.path.basename(rf))[0]
        if rid in injected:
            continue
        try:
            with open(rf, "r", encoding="utf-8") as f:
                text = f.read()
        except Exception:
            continue
        fm, body = parse_frontmatter(text)
        always = str(fm.get("alwaysApply", "false")).lower() == "true"
        globs = parse_globs(fm.get("globs", ""))

        matched = always
        if not matched and fp and globs:
            for g in globs:
                if glob_to_regex(g).search(fp):
                    matched = True
                    break
        if matched:
            to_emit.append((rid, body.strip()))

    if not to_emit:
        sys.exit(0)

    for rid, _ in to_emit:
        injected.add(rid)
    save_injected(state_path, injected)

    parts = ["[规则自动加载] 以下项目规范适用于当前操作，请遵守：\n"]
    for rid, body in to_emit:
        parts.append(f"\n===== rule: {rid} =====\n{body}")
    context = "\n".join(parts)

    out = {
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "additionalContext": context,
        }
    }
    sys.stdout.write(json.dumps(out, ensure_ascii=False))
    sys.exit(0)


if __name__ == "__main__":
    main()
