#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Eval 确定性检查器：读一条用例 JSON，执行 deterministic_checks，输出通过/失败。

支持的 check.type：
  - grep   : 在 files(glob) 里按 pattern 正则匹配，expect = present | absent
  - compile: 经 unity-bridge 编译，expect = no_errors（需 Unity + AiBridge 在跑）

用法：python ai-docs/evals/check.py <case.json>
退出码：0 全过 / 1 有失败 / 2 用例错误
"""
import sys, os, re, json, glob, subprocess

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def run_grep(chk):
    pattern = chk.get("pattern", "")
    expect = chk.get("expect", "present")
    globs = chk.get("files", ["**/*.cs"])
    try:
        rx = re.compile(pattern)
    except re.error as e:
        return False, f"正则错误: {e}"
    found_in = []
    for g in globs:
        for path in glob.glob(os.path.join(PROJECT, g), recursive=True):
            if not os.path.isfile(path):
                continue
            try:
                with open(path, "r", encoding="utf-8", errors="replace") as f:
                    if rx.search(f.read()):
                        found_in.append(os.path.relpath(path, PROJECT))
            except Exception:
                pass
    present = len(found_in) > 0
    if expect == "present":
        return present, ("命中: " + ", ".join(found_in[:3]) if present else "未找到匹配")
    else:  # absent
        return (not present), ("应不存在但命中: " + ", ".join(found_in[:3]) if present else "确认不存在")


def run_compile(chk):
    bridge = os.path.join(PROJECT, ".claude", "skills", "unity-bridge", "scripts", "bridge.py")
    try:
        r = subprocess.run([sys.executable, bridge, "compile"], cwd=PROJECT,
                           capture_output=True, text=True, timeout=180)
        ok = r.returncode == 0
        return ok, (r.stdout.strip().splitlines()[-1] if r.stdout.strip() else "")
    except Exception as e:
        return False, f"编译检查失败（Unity/桥是否在跑？）: {e}"


def main():
    if len(sys.argv) < 2:
        print("用法: python check.py <case.json>", file=sys.stderr)
        sys.exit(2)
    try:
        with open(sys.argv[1], "r", encoding="utf-8") as f:
            case = json.load(f)
    except Exception as e:
        print(f"读用例失败: {e}", file=sys.stderr)
        sys.exit(2)

    print(f"== {case.get('id','?')} {case.get('name','')} ==")
    checks = case.get("deterministic_checks", [])
    all_ok = True
    for chk in checks:
        t = chk.get("type")
        if t == "grep":
            ok, detail = run_grep(chk)
        elif t == "compile":
            ok, detail = run_compile(chk)
        else:
            ok, detail = False, f"未知 check 类型: {t}"
        all_ok = all_ok and ok
        mark = "✅" if ok else "❌"
        desc = chk.get("pattern", t)
        print(f"  {mark} [{t}] {desc} (expect={chk.get('expect','')}) — {detail}")

    print("结果:", "PASS" if all_ok else "FAIL")
    sys.exit(0 if all_ok else 1)


if __name__ == "__main__":
    main()
