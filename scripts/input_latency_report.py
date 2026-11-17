"""Build a standalone responsiveness timeline from application timing samples."""
import bisect
import csv
import json
import math
from pathlib import Path


def percentile(values, fraction):
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)] if ordered else None


def distribution(values, budget):
    misses = sum(value > budget for value in values)
    longest = current = 0
    for value in values:
        current = current + 1 if value > budget else 0
        longest = max(longest, current)
    return {"count": len(values), "p50_ms": percentile(values, .5),
            "p95_ms": percentile(values, .95), "p99_ms": percentile(values, .99),
            "max_ms": max(values, default=None), "over_budget": misses,
            "over_budget_percent": 100 * misses / len(values) if values else None,
            "over_twice_budget": sum(value > 2 * budget for value in values),
            "longest_over_budget_streak": longest}


def read_capture(path):
    with path.open(encoding="utf-8-sig") as stream:
        anchor = stream.readline().strip()
        rows = list(csv.DictReader(stream))
    for row in rows:
        row["time"] = int(row["usec"]) / 1000
    if any(a["time"] > b["time"] for a, b in zip(rows, rows[1:])):
        raise ValueError(f"Non-monotonic timestamps in {path}")

    starts = [r for r in rows if r["kind"] == "frame_start"]
    draws = [r["time"] for r in rows if r["kind"] == "draw_end"]
    draw_spans = []
    session_spans = []
    draw_start = None
    inputs = []
    motion = None
    for row in rows:
        kind, time = row["kind"], row["time"]
        if kind == "draw_start":
            draw_start = time
        elif kind == "draw_end" and draw_start is not None:
            draw_spans.append((draw_start, time))
            draw_start = None
        elif kind == "session_end":
            session_spans.append((time - float(row["duration_ms"]), time,
                                  int(row["tick"]) - int(float(row["x"]))))
        elif kind == "mouse_motion":
            motion = time
        elif kind in ("mouse_up", "mouse_down", "rectangle_hidden"):
            motion = None
        elif kind == "rectangle" and motion is not None:
            index = bisect.bisect_left(draws, time)
            if index < len(draws):
                inputs.append([motion / 1000, draws[index] - motion, time - motion])
            motion = None

    def overlap(spans, ends, start, end):
        total = ticks = 0
        index = bisect.bisect_right(ends, start)
        while index < len(spans) and spans[index][0] < end:
            span = spans[index]
            total += max(0, min(end, span[1]) - max(start, span[0]))
            if len(span) > 2:
                ticks += span[2]
            index += 1
        return total, ticks

    session_ends = [s[1] for s in session_spans]
    draw_ends = [s[1] for s in draw_spans]
    frames = []
    for row, next_row in zip(starts, starts[1:]):
        start, end = row["time"], next_row["time"]
        session, ticks = overlap(session_spans, session_ends, start, end)
        draw, _ = overlap(draw_spans, draw_ends, start, end)
        frames.append([start / 1000, int(row["frame"]), end - start, session, draw,
                       max(0, end - start - session - draw), ticks])
    return {"name": path.name.removesuffix(".latency.csv"), "anchor": anchor,
            "frames": frames, "inputs": inputs,
            "markers": [r["time"] / 1000 for r in rows if r["kind"] == "marker"]}


def summarize(capture, frame_budget, feedback_budget, warmup):
    frames = [f for f in capture["frames"] if f[0] >= warmup]
    inputs = [i for i in capture["inputs"] if i[0] >= warmup]
    windows = []
    end = frames[-1][0] if frames else warmup
    for second in range(math.ceil(warmup + 10), math.floor(end) + 1):
        recent_frames = [f[2] for f in frames if second - 10 <= f[0] < second]
        recent_inputs = [i[1] for i in inputs if second - 10 <= i[0] < second]
        windows.append({"end_seconds": second, "frames": distribution(recent_frames, frame_budget),
                        "feedback": distribution(recent_inputs, feedback_budget)})
    return {"client": capture["name"], "warmup_seconds": warmup,
            "frame_budget_ms": frame_budget, "feedback_budget_ms": feedback_budget,
            "frames": distribution([f[2] for f in frames], frame_budget),
            "feedback": distribution([i[1] for i in inputs], feedback_budget),
            "rolling_10_seconds": windows}


def write_report(paths, output, frame_budget=1000 / 60, feedback_budget=1000 / 30, warmup=5):
    captures = [read_capture(path) for path in paths]
    summary = [summarize(c, frame_budget, feedback_budget, warmup) for c in captures]
    payload = {"captures": captures, "frameBudget": frame_budget,
               "feedbackBudget": feedback_budget, "warmup": warmup}
    template = Path(__file__).with_name("input-latency-report.html").read_text(encoding="utf-8")
    encoded = json.dumps(payload, separators=(",", ":")).replace("<", "\\u003c")
    output.write_text(template.replace("/*CAPTURE_DATA*/null", encoded), encoding="utf-8")
    output.with_suffix(".json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return summary
