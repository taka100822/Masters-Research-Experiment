"""実験ログの点検（README 6.8）

その日の実験が終わったら、ログのフォルダを読んで、欠け・食い違いがないかを一覧にする。
ログは読むだけで、何も書き換えない。標準ライブラリだけで動く。

    python tools/check_logs.py [ログのフォルダ]

フォルダを省くと、ドキュメントの ExperimentLogsBackup（ログの控え。README 6.2）を見る。
"""

import csv
import os
import re
import sys
import unicodedata
from collections import defaultdict
from pathlib import Path

# ファイル名：{ID}_{条件}_S{番号}_{日付}_{時刻}.csv（README 6.2）
LOG_NAME = re.compile(r"^(?P<id>.+)_(?P<cond>[AB])_S(?P<set>\d+)_(?P<stamp>\d{8}_\d{6})\.csv$")

# 条件Bで送信のうちこれ以上がAIのエラーなら知らせる
AI_ERROR_RATIO = 0.2


def trailing_number(pid):
    m = re.search(r"(\d+)$", pid)
    return (pid[: m.start()], int(m.group(1)), len(m.group(1))) if m else (pid, 0, 0)


# グループ＝(番号 − 1) を4で割った余り ＋ 1。ExperimentConfig.GroupOf と同じ（README 2.5）
def group_of(pid):
    n = trailing_number(pid)[1]
    return (n - 1) % 4 + 1 if n > 0 else 0


# グループ1：A・S1→B・S2、2：A・S2→B・S1、3：B・S1→A・S2、4：B・S2→A・S1（README 1.3）
def expected(group, rnd):
    first_b = group >= 3
    is_b = (not first_b) if rnd == 2 else first_b
    first_set = 1 if group % 2 == 1 else 2
    return ("B" if is_b else "A", first_set if rnd == 1 else 3 - first_set)


def read_detail(detail):
    out = {}
    for part in (detail or "").split(";"):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def pad(text, width):
    """全角を2文字分と数えて右を空白で埋める（表の列をそろえる）"""
    used = sum(2 if unicodedata.east_asian_width(c) in "FWA" else 1 for c in text)
    return text + " " * max(0, width - used)


def compact_ids(ids):
    """P001, P002, P003, P005 → P001〜P003, P005"""
    keyed = sorted(set(ids), key=lambda p: (trailing_number(p)[0], trailing_number(p)[1]))
    runs = []
    for pid in keyed:
        prefix, n, _ = trailing_number(pid)
        if runs and runs[-1][0] == prefix and runs[-1][2] + 1 == n:
            runs[-1][3] = pid
            runs[-1][2] = n
        else:
            runs.append([prefix, n, n, pid, pid])
    return ", ".join(r[4] if r[4] == r[3] else r[4] + "〜" + r[3] for r in runs)


class Session:
    def __init__(self, path, m):
        self.path = path
        self.name = path.name
        self.pid = m.group("id")
        self.cond = m.group("cond")
        self.set = int(m.group("set"))
        self.stamp = m.group("stamp")
        self.session_id = None
        self.round = None
        self.build = None
        self.end_reason = None
        self.cols = set()   # 行の condition・set 列の値
        self.inputs = 0
        self.ai_errors = 0
        self.ai_wait_ms = 0
        self.prompts = {}   # プロンプトの場所 → ハッシュ値
        self.models = set()
        self.read()

    def read(self):
        with open(self.path, encoding="utf-8-sig", newline="") as f:
            for row in csv.DictReader(f):
                ev = row.get("event_type")
                d = read_detail(row.get("detail"))
                if self.session_id is None:
                    self.session_id = row.get("session_id")
                self.cols.add((row.get("condition"), row.get("set")))
                if ev == "session_start":
                    self.round = int(d["round"]) if d.get("round", "").isdigit() else None
                    self.build = d.get("build", "unknown")
                elif ev == "session_end":
                    self.end_reason = d.get("reason")
                elif ev == "free_input_submit":
                    self.inputs += 1
                elif ev == "ai_response":
                    if row.get("is_error") == "1":
                        self.ai_errors += 1
                    if d.get("latency_ms", "").isdigit():
                        self.ai_wait_ms += int(d["latency_ms"])
                    if "hash" in d:
                        self.prompts[d.get("prompt")] = d["hash"]
                    if "resp_model" in d:
                        self.models.add(d["resp_model"] + (" / fp=" + d["fp"] if "fp" in d else ""))

    @property
    def done(self):
        return self.end_reason == "time_up"


def read_summary_ids(folder):
    """summary.csv と summary_pending_…csv に入っている session_id"""
    in_summary, in_pending = set(), defaultdict(set)
    for path in sorted(folder.glob("summary*.csv")):
        if path.name.startswith("summary_old_"):
            continue
        with open(path, encoding="utf-8-sig", newline="") as f:
            ids = {row.get("session_id") for row in csv.DictReader(f)}
        if path.name == "summary.csv":
            in_summary |= ids
        elif path.name.startswith("summary_pending_"):
            in_pending[path.name] |= ids
    return in_summary, in_pending


def documents_folder():
    """ゲームの Environment.SpecialFolder.MyDocuments と同じ場所（OneDriveへ移していてもそこを指す）"""
    if os.name == "nt":
        import ctypes
        buf = ctypes.create_unicode_buffer(260)
        if ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, buf) == 0:  # CSIDL_PERSONAL
            return Path(buf.value)
    return Path(os.path.expanduser("~")) / "Documents"


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    default = documents_folder() / "ExperimentLogsBackup"
    folder = Path(sys.argv[1]) if len(sys.argv) > 1 else default
    if not folder.is_dir():
        print(f"フォルダがありません：{folder}")
        return 1
    print(f"ログのフォルダ：{folder}\n")

    # 本番のログだけ。チュートリアルと番号0のID（動作確認用）は数えない
    sessions = []
    for path in sorted(folder.glob("*.csv")):
        m = LOG_NAME.match(path.name)
        if not m or group_of(m.group("id")) == 0:
            continue
        try:
            sessions.append(Session(path, m))
        except Exception as e:  # 壊れたファイルがあっても他は点検する
            print(f"読めないファイル：{path.name}（{e}）")

    warnings = []
    by_pid = defaultdict(list)
    for s in sessions:
        by_pid[s.pid.upper()].append(s)

    # 1. 参加者ごとの表
    print("■ 参加者ごと（✓＝time_upで終わった回。同じ回が複数あれば最後の完了を使う）")
    print(f"ID      G  {pad('1回目', 34)} 2回目")
    completed = defaultdict(list)
    chosen = []
    for key in sorted(by_pid, key=lambda k: (trailing_number(k)[0], trailing_number(k)[1])):
        group_sessions = sorted(by_pid[key], key=lambda s: s.stamp)
        pid = group_sessions[0].pid
        g = group_of(pid)
        cells = []
        rounds_done = 0
        for rnd in (1, 2):
            in_round = [s for s in group_sessions if s.round == rnd]
            done = [s for s in in_round if s.done]
            pick = done[-1] if done else (in_round[-1] if in_round else None)
            if pick is None:
                cells.append("—")
                warnings.append(f"{pid}：{rnd}回目のログがありません")
                continue
            if done:
                rounds_done += 1
                chosen.append(pick)
            else:
                warnings.append(f"{pid}：{rnd}回目にtime_upで終わったログがありません")
            mark = "✓" if pick.done else "×"
            ai = f" エラー{pick.ai_errors}/{pick.inputs} 待ち{pick.ai_wait_ms / 1000:.1f}秒" if pick.cond == "B" else ""
            cells.append(f"{mark} {pick.cond}・S{pick.set}{ai}")

            # 2. 条件・セットがグループの表と合っているか（ファイル名と行の列の両方）
            exp_cond, exp_set = expected(g, rnd)
            if (pick.cond, pick.set) != (exp_cond, exp_set):
                warnings.append(f"{pid}：{rnd}回目が{pick.cond}・S{pick.set}（グループ{g}なら{exp_cond}・S{exp_set}）")
            if pick.cols != {(pick.cond, str(pick.set))}:
                cols = "、".join(f"{c}・{s}" for c, s in sorted(pick.cols))
                warnings.append(f"{pid}：{rnd}回目のログの中のcondition・set列（{cols}）がファイル名（{pick.cond}・S{pick.set}）と違います")

            # 8. AIのエラーが多い回
            if pick.cond == "B" and pick.inputs and pick.ai_errors / pick.inputs >= AI_ERROR_RATIO:
                warnings.append(f"{pid}の{rnd}回目：AIのエラーが{pick.ai_errors}/{pick.inputs}回")
        if rounds_done == 2:
            completed[g].append(pid)
        print(f"{pid:<7} {g}  {pad(cells[0], 34)} {cells[1]}")

        # 3. time_up以外で終わった本番（やり直しの跡）
        for s in group_sessions:
            if not s.done:
                warnings.append(f"{pid}：{s.round or '?'}回目のログ（{s.name}）が{s.end_reason or '終わりの記録なし'}で終わっています")
            if s.round is None:
                warnings.append(f"{pid}：{s.name}に何回目か（round）がありません")

    # 2'. グループごとの完了人数
    print("\n■ グループごとの完了人数（2回ともtime_up）")
    print("  " + "  ".join(f"G{g}:{len(completed[g])}人" for g in (1, 2, 3, 4)))

    # 4. ビルド
    builds = defaultdict(list)
    for s in chosen:
        builds[s.build or "unknown"].append(s.pid)
    for b, pids in builds.items():
        if b == "editor" or b == "unknown" or b.endswith("-dirty"):
            warnings.append(f"本番として使えないビルド（{b}）で遊んだ回があります：{compact_ids(pids)}")
    if len(builds) > 1:
        warnings.append("ビルドが" + str(len(builds)) + "種類あります：" +
                        "、".join(f"{b}（{compact_ids(p)}）" for b, p in builds.items()))

    # 5. プロンプトのハッシュ値
    prompts = defaultdict(lambda: defaultdict(list))
    for s in chosen:
        for place, h in s.prompts.items():
            prompts[place][h].append(s.pid)
    for place in sorted(prompts, key=str):
        if len(prompts[place]) > 1:
            warnings.append(f"{place}のプロンプトが{len(prompts[place])}種類あります：" +
                            "、".join(f"{h}（{compact_ids(p)}）" for h, p in prompts[place].items()))

    # 6. AIが返したモデル名（README 6.3）
    models = defaultdict(list)
    for s in chosen:
        for name in s.models:
            models[name].append(s.pid)
    if len(models) > 1:
        warnings.append("AIの返したモデル名が" + str(len(models)) + "種類あります：" +
                        "、".join(f"{n}（{compact_ids(p)}）" for n, p in models.items()))

    # 7. summary.csv との対応
    in_summary, in_pending = read_summary_ids(folder)
    for s in sessions:
        if s.session_id and s.session_id not in in_summary:
            pending = [n for n, ids in in_pending.items() if s.session_id in ids]
            if pending:
                warnings.append(f"{s.pid}の{s.round or '?'}回目（{s.name}）の集計が{pending[0]}にだけあります。summary.csvへ写してください")
            else:
                warnings.append(f"summary.csvに{s.pid}の{s.round or '?'}回目（{s.name}）の行がありません")

    print("\n■ 注意")
    if warnings:
        for w in warnings:
            print("  ・" + w)
    else:
        print("  問題は見つかりませんでした")
    return 0


if __name__ == "__main__":
    sys.exit(main())
