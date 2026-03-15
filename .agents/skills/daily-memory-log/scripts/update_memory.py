#!/usr/bin/env python3
import argparse
import datetime as dt
import re
from pathlib import Path


DATE_SECTION_PATTERN = re.compile(r"^##\s+(\d{4}-\d{2}-\d{2})\s*$", re.MULTILINE)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Append a timestamped daily work entry to memory.md."
    )
    parser.add_argument(
        "--memory-file",
        default="memory.md",
        help="Target memory file path. Default: memory.md",
    )
    parser.add_argument(
        "--date",
        default="",
        help="Date section in YYYY-MM-DD. Default: today",
    )
    parser.add_argument(
        "--time",
        default="",
        help="Entry time in HH:MM. Default: now",
    )
    parser.add_argument(
        "--work",
        action="append",
        default=[],
        help="Work bullet. Repeatable.",
    )
    parser.add_argument(
        "--follow-up",
        action="append",
        default=[],
        help="Follow-up caution bullet. Repeatable.",
    )
    return parser.parse_args()


def normalize_items(items):
    normalized = []
    for item in items:
        text = item.strip()
        if text:
            normalized.append(text)
    return normalized


def build_entry(time_text, work_items, follow_up_items):
    work_lines = "\n".join(f"- {item}" for item in work_items)
    follow_lines = "\n".join(f"- {item}" for item in follow_up_items)
    return (
        f"### {time_text}\n\n"
        "#### 今日工作内容\n"
        f"{work_lines}\n\n"
        "#### 后续注意点\n"
        f"{follow_lines}\n"
    )


def ensure_file_has_header(text):
    if text.strip():
        return text
    return "# Memory\n"


def insert_into_date_section(text, date_text, entry):
    header = f"## {date_text}"
    section_match = re.search(rf"^##\s+{re.escape(date_text)}\s*$", text, re.MULTILINE)

    if not section_match:
        result = text.rstrip() + "\n\n" + header + "\n\n" + entry
        return result.rstrip() + "\n"

    insert_from = section_match.end()
    next_header_match = DATE_SECTION_PATTERN.search(text, insert_from)
    if next_header_match:
        insert_pos = next_header_match.start()
        before = text[:insert_pos].rstrip()
        after = text[insert_pos:]
        result = before + "\n\n" + entry.rstrip() + "\n\n" + after.lstrip("\n")
    else:
        result = text.rstrip() + "\n\n" + entry

    return result.rstrip() + "\n"


def main():
    args = parse_args()

    now = dt.datetime.now()
    date_text = args.date.strip() or now.strftime("%Y-%m-%d")
    time_text = args.time.strip() or now.strftime("%H:%M")

    work_items = normalize_items(args.work)
    if not work_items:
        raise SystemExit("At least one --work item is required.")

    follow_up_items = normalize_items(args.follow_up)
    if not follow_up_items:
        follow_up_items = ["暂无，按当前方案继续验证"]

    memory_path = Path(args.memory_file).resolve()
    memory_path.parent.mkdir(parents=True, exist_ok=True)

    original = ""
    if memory_path.exists():
        original = memory_path.read_text(encoding="utf-8")

    seeded = ensure_file_has_header(original)
    entry = build_entry(time_text, work_items, follow_up_items)
    updated = insert_into_date_section(seeded, date_text, entry)

    memory_path.write_text(updated, encoding="utf-8")
    print(f"[OK] Updated memory file: {memory_path}")
    print(f"[OK] Date section: {date_text}")
    print(f"[OK] Entry time: {time_text}")


if __name__ == "__main__":
    main()
