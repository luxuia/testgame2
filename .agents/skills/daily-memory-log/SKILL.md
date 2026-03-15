---
name: daily-memory-log
description: Record daily execution memory to a repository memory.md file. Use when the user asks to log today's completed work, handoff notes, progress summary, pending risks, follow-up cautions, or "remember this for later". Always append both "今日工作内容" and "后续注意点" to the current date section.
---

# Daily Memory Log

## Workflow

1. Summarize today's completed work into 2-8 concise bullets.
2. Summarize follow-up cautions into 1-6 concise bullets.
3. Run `scripts/update_memory.py` to append both sections into the daily `memory.md`.
4. Verify by showing the newest date section or the tail of `memory.md`.

Never finish this skill without writing to `memory.md`.

## Command

Run from repository root:

```bash
python .agents/skills/daily-memory-log/scripts/update_memory.py \
  --memory-file memory.md \
  --work "完成了A" \
  --work "完成了B" \
  --follow-up "注意点1" \
  --follow-up "注意点2"
```

## Rules

- Keep items factual and specific; avoid vague text like "优化了一些东西".
- Include both sections every time:
- `今日工作内容`
- `后续注意点`
- If there is no follow-up risk, write one item: `- 暂无，按当前方案继续验证`.
- Default target file is repository-root `memory.md` unless user specifies a different path.
- Append to today's date section (`YYYY-MM-DD`), do not overwrite older records.

## Script

- Use: `scripts/update_memory.py`
- Purpose: create/update daily section and append a timestamped entry.
