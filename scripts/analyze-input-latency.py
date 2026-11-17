"""Summarize manual input captures; draw completion is not monitor presentation."""
import bisect
import argparse
import csv
import json
import math
import pathlib
from input_latency_report import write_report


def stats(values):
    values = sorted(values)
    if not values:
        return {"count": 0}
    return {"count": len(values), "median_ms": round(values[len(values) // 2], 3),
            "p95_ms": round(values[min(len(values) - 1, int(len(values) * .95))], 3),
            "max_ms": round(values[-1], 3)}


def analyze(path):
    with path.open(encoding="utf-8-sig") as stream:
        anchor = stream.readline().strip()
        rows = list(csv.DictReader(stream))
    draws = [int(r["usec"]) for r in rows if r["kind"] == "draw_end"]
    frames = [int(r["usec"]) for r in rows if r["kind"] == "frame_start"]
    sessions = [r for r in rows if r["kind"] == "session_end"]
    session_ends = [int(r["usec"]) for r in sessions]
    session_spans = [(end - float(r["duration_ms"]) * 1000, end)
                     for r, end in zip(sessions, session_ends)]
    rectangles = []
    input_times = []
    dispatch_to_draw = []
    slow_drags = []
    last_motion = None
    for row in rows:
        timestamp = int(row["usec"])
        if row["kind"] == "mouse_motion":
            last_motion = timestamp
            input_times.append(timestamp)
        elif row["kind"] == "rectangle" and last_motion is not None:
            rectangles.append((timestamp - last_motion) / 1000)
            index = bisect.bisect_left(draws, timestamp)
            if index < len(draws):
                end = draws[index]
                latency = (end - last_motion) / 1000
                dispatch_to_draw.append(latency)
                session_ms = 0
                for start, finish in session_spans[bisect.bisect_left(session_ends, last_motion):]:
                    if start > end:
                        break
                    session_ms += max(0, min(end, finish) - max(last_motion, start)) / 1000
                slow_drags.append({"input_seconds": last_motion / 1e6,
                                   "dispatch_to_draw_ms": round(latency, 3),
                                   "session_overlap_ms": round(session_ms, 3)})
    slowest = sorted(sessions, key=lambda r: float(r["duration_ms"]), reverse=True)[:10]
    return {"file": str(path), "anchor": anchor,
            "mouse_motion_events": len(input_times),
            "dispatch_to_rectangle": stats(rectangles),
            "drag_dispatch_to_next_draw_end": stats(dispatch_to_draw),
            "frame_start_intervals": stats([(b - a) / 1000 for a, b in zip(frames, frames[1:])]),
            "session_update": stats([float(r["duration_ms"]) for r in sessions]),
            "slowest_drag_updates": sorted(slow_drags, key=lambda r: r["dispatch_to_draw_ms"], reverse=True)[:10],
            "slowest_sessions": [{"end_seconds": int(r["usec"]) / 1e6,
                                  "duration_ms": float(r["duration_ms"]),
                                  "predicted_ticks_advanced": int(r["tick"]) - int(float(r["x"]))}
                                 for r in slowest],
            "limits": "Excludes pre-dispatch input waiting and monitor presentation; coalesced updates may share one draw."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=pathlib.Path)
    parser.add_argument("--target-fps", type=float, default=60)
    parser.add_argument("--feedback-budget-ms", type=float, default=1000 / 30)
    parser.add_argument("--warmup-seconds", type=float, default=5)
    args = parser.parse_args()
    if (not all(math.isfinite(value) for value in (args.target_fps, args.feedback_budget_ms, args.warmup_seconds))
            or args.target_fps <= 0 or args.feedback_budget_ms <= 0 or args.warmup_seconds < 0):
        parser.error("Budgets must be positive and warmup must be non-negative")
    folder = args.capture
    paths = sorted(folder.glob("*.latency.csv")) if folder.is_dir() else [folder]
    if not paths:
        parser.error("No latency captures found")
    result = [analyze(path) for path in paths]
    output = (folder if folder.is_dir() else folder.parent) / "input-latency-summary.json"
    output.write_text(json.dumps(result, indent=2) + "\n")
    report = output.with_name("responsiveness.html")
    summary = write_report(paths, report, 1000 / args.target_fps, args.feedback_budget_ms, args.warmup_seconds)
    for client in summary:
        print(client["client"], json.dumps({k: client[k] for k in ("frames", "feedback")}))
    print(f"Report: {report}")


if __name__ == "__main__":
    main()
