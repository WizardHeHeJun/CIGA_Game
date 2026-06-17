#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""C# lint hook（数据驱动）。

PostToolUse(Edit|Write) 触发。规则定义在 rules.json，本引擎只读取执行。
设计原则：成功静默（无违规无输出，exit 0）；失败冗余（输出 行号+代码+修复+引用，exit 2 让模型修）。

四层过滤（line 规则）：file_context（文件级前置）→ pattern（行级主匹配）
→ exclude_patterns（排除误报）→ confirm_patterns（二次确认）。
file 规则：file_context 命中且 file_context_absent 未命中 → 报一次。
"""
import sys, os, io, json, re

HERE = os.path.dirname(os.path.abspath(__file__))
RULES_PATH = os.path.join(HERE, "rules.json")

# Windows 控制台中文输出兜底
try:
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass


def read_event():
    try:
        return json.load(sys.stdin)
    except Exception:
        return {}


def load_rules():
    try:
        with open(RULES_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        # 规则文件坏了不应阻断开发，仅提示
        sys.stderr.write(f"[csharp-lint] 规则加载失败: {e}\n")
        return []


def compile_list(patterns):
    out = []
    for p in patterns or []:
        try:
            out.append(re.compile(p))
        except re.error:
            pass
    return out


def check(content, rules):
    lines = content.splitlines()
    violations = []  # (severity, line_no, rule_id, message, fix, ref)

    for r in rules:
        rid = r.get("id", "?")
        sev = r.get("severity", "warn")
        rule_name = r.get("rule", "")
        fix = r.get("fix", "")
        ref = r.get("ref", "")
        tpl = r.get("violation_tpl", "{line}")

        fc = r.get("file_context")
        if fc and not re.search(fc, content):
            continue

        if r.get("scope") == "file":
            fca = r.get("file_context_absent")
            if fca and re.search(fca, content):
                continue
            msg = tpl.replace("{line}", "")
            violations.append((sev, 1, rid, msg, fix, ref))
            continue

        # line 规则
        pat = r.get("pattern")
        if not pat:
            continue
        try:
            main = re.compile(pat)
        except re.error:
            continue
        excludes = compile_list(r.get("exclude_patterns"))
        confirms = compile_list(r.get("confirm_patterns"))

        for i, line in enumerate(lines, 1):
            if not main.search(line):
                continue
            if any(e.search(line) for e in excludes):
                continue
            if confirms and not any(c.search(line) for c in confirms):
                continue
            msg = tpl.replace("{line}", line.strip())
            violations.append((sev, i, rid, msg, fix, ref))

    return violations


def main():
    ev = read_event()
    tool_input = ev.get("tool_input", {}) or {}
    fp = tool_input.get("file_path") or tool_input.get("filePath") or ""
    if not fp or not fp.lower().endswith(".cs"):
        sys.exit(0)
    if not os.path.isfile(fp):
        sys.exit(0)

    try:
        with open(fp, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()
    except Exception:
        sys.exit(0)

    rules = load_rules()
    if not rules:
        sys.exit(0)

    violations = check(content, rules)
    if not violations:
        sys.exit(0)  # 成功静默

    # 失败冗余
    sev_order = {"block": 0, "warn": 1, "info": 2}
    violations.sort(key=lambda v: (sev_order.get(v[0], 9), v[1]))
    name = os.path.basename(fp)
    out = [f"[csharp-lint] {name} 有 {len(violations)} 处需处理："]
    for sev, ln, rid, msg, fix, ref in violations:
        tag = {"block": "🔴", "warn": "🟡", "info": "⚪"}.get(sev, "•")
        out.append(f"  {tag} L{ln} [{rid}] {msg}")
        if fix:
            out.append(f"     → 修复: {fix}")
        if ref:
            out.append(f"     ref: {ref}")
    sys.stderr.write("\n".join(out) + "\n")
    # exit 2：把 stderr 反馈给模型去修
    sys.exit(2)


if __name__ == "__main__":
    main()
