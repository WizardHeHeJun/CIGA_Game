#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AI 验证桥 CLI 客户端：通过本地 HTTP 驱动 Unity Editor（端口 17900）。

用法：
  python bridge.py health
  python bridge.py compile            # 触发编译并等结果，打印 error/warning
  python bridge.py console [--level error|warn|all] [--limit N]
  python bridge.py play | stop
  python bridge.py screenshot [--path X.png]
  python bridge.py test [--mode edit|play]
  python bridge.py hierarchy
  python bridge.py status

需 Unity 打开本工程，且 AiBridge 已启动（Window/AI Bridge，默认自动启动）。
"""
import sys, json, time, argparse, urllib.request, urllib.error

BASE = "http://127.0.0.1:17900"


def call(path, method="GET", timeout=20):
    url = BASE + path
    req = urllib.request.Request(url, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            body = r.read().decode("utf-8", "replace")
            try:
                return json.loads(body), None
            except Exception:
                return body, None
    except urllib.error.URLError as e:
        return None, f"连接失败：{e}. 确认 Unity 打开本工程且 AiBridge 已启动（Window/AI Bridge）。"
    except Exception as e:
        return None, str(e)


def cmd_health(_):
    d, err = call("/health")
    if err: fail(err)
    print(json.dumps(d, ensure_ascii=False, indent=2))


def cmd_status(_):
    d, err = call("/status")
    if err: fail(err)
    print(json.dumps(d, ensure_ascii=False, indent=2))


def cmd_compile(_):
    d, err = call("/compile", method="POST")
    if err: fail(err)
    print("已请求编译，等待结果……")
    # 轮询直到 !isCompiling 且拿到结果（编译会触发域重载，连接可能短暂中断，容错重试）
    deadline = time.time() + 120
    last = None
    settled = 0
    while time.time() < deadline:
        time.sleep(1.0)
        d, err = call("/status", timeout=5)
        if err or d is None:
            continue  # 域重载中，稍后重试
        if d.get("isCompiling"):
            settled = 0
            continue
        last = d.get("compile")
        settled += 1
        if settled >= 2:  # 连续两次非编译态，认为稳定
            break
    if not last:
        print("未拿到编译结果（可能无脚本变更，或 Unity 未刷新）。可在 Unity 里手动 Refresh 后重试。")
        return
    ec = last.get("errorCount", 0)
    wc = last.get("warningCount", 0)
    print(f"编译完成：{ec} error, {wc} warning（{last.get('time','')}）")
    for e in last.get("errors", []):
        print("  🔴 " + e)
    for w in last.get("warnings", [])[:20]:
        print("  🟡 " + w)
    if ec:
        sys.exit(1)


def cmd_console(args):
    d, err = call(f"/console?level={args.level}&limit={args.limit}")
    if err: fail(err)
    logs = d.get("logs", []) if isinstance(d, dict) else []
    if not logs:
        print("（无匹配日志）")
        return
    for l in logs:
        print(f"[{l.get('time','')}] {l.get('type','')}: {l.get('message','')}")


def cmd_play(_):
    d, err = call("/play", method="POST")
    if err: fail(err)
    print("进入 PlayMode" if isinstance(d, dict) and d.get("playing") else json.dumps(d, ensure_ascii=False))


def cmd_stop(_):
    d, err = call("/stop", method="POST")
    if err: fail(err)
    print("退出 PlayMode")


def cmd_screenshot(args):
    p = f"/screenshot"
    if args.path:
        p += "?path=" + urllib.request.quote(args.path)
    d, err = call(p)
    if err: fail(err)
    print(json.dumps(d, ensure_ascii=False, indent=2))


def cmd_test(args):
    d, err = call(f"/tests?mode={args.mode}", method="POST")
    if err: fail(err)
    print(f"已启动 {args.mode} 测试，等待结果……")
    deadline = time.time() + 180
    while time.time() < deadline:
        time.sleep(1.5)
        d, err = call("/tests/result", timeout=5)
        if err or not isinstance(d, dict):
            continue
        if d.get("status") == "running":
            continue
        if d.get("status") == "done":
            print(f"测试完成：{d.get('passed',0)} 通过 / {d.get('failed',0)} 失败 / {d.get('skipped',0)} 跳过")
            for f in d.get("failures", []):
                print("  ❌ " + f.get("name", "") + " — " + f.get("message", ""))
            if d.get("failed", 0):
                sys.exit(1)
            return
        if d.get("status") == "error":
            fail("测试运行出错：" + d.get("message", ""))
    print("测试超时未返回结果。")


def cmd_hierarchy(_):
    d, err = call("/hierarchy")
    if err: fail(err)
    print(json.dumps(d, ensure_ascii=False, indent=2))


def fail(msg):
    print("错误：" + msg, file=sys.stderr)
    sys.exit(2)


def main():
    p = argparse.ArgumentParser(prog="bridge.py")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("health").set_defaults(fn=cmd_health)
    sub.add_parser("status").set_defaults(fn=cmd_status)
    sub.add_parser("compile").set_defaults(fn=cmd_compile)
    c = sub.add_parser("console"); c.add_argument("--level", default="error"); c.add_argument("--limit", type=int, default=50); c.set_defaults(fn=cmd_console)
    sub.add_parser("play").set_defaults(fn=cmd_play)
    sub.add_parser("stop").set_defaults(fn=cmd_stop)
    s = sub.add_parser("screenshot"); s.add_argument("--path", default=None); s.set_defaults(fn=cmd_screenshot)
    t = sub.add_parser("test"); t.add_argument("--mode", default="edit"); t.set_defaults(fn=cmd_test)
    sub.add_parser("hierarchy").set_defaults(fn=cmd_hierarchy)
    args = p.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
