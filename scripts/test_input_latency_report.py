import csv
import tempfile
import unittest
from pathlib import Path

from input_latency_report import distribution, read_capture, read_render_capture, summarize


class ResponsivenessTests(unittest.TestCase):
    def test_tail_percentiles_and_miss_streaks(self):
        result = distribution([10, 30, 40, 10, 50], 20)
        self.assertEqual(result["p95_ms"], 50)
        self.assertEqual(result["p99_ms"], 50)
        self.assertEqual(result["over_budget"], 3)
        self.assertEqual(result["longest_over_budget_streak"], 2)
        self.assertEqual(result["over_twice_budget"], 1)
        self.assertIsNone(distribution([], 20)["p95_ms"])

    def test_clock_intervals_clipping_and_unfinished_feedback(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "client1.latency.csv"
            with path.open("w", newline="") as stream:
                stream.write("# utc_origin=test\n")
                writer = csv.writer(stream)
                writer.writerow(["usec", "frame", "kind", "x", "y", "width", "height", "tick", "duration_ms"])
                for time, frame, kind, duration in [
                    (0, 1, "frame_start", 0), (1000, 1, "mouse_motion", 0),
                    (1010, 1, "rectangle", 0), (1500, 1, "mouse_motion", 0),
                    (1510, 1, "rectangle", 0), (5000, 1, "session_end", 3),
                    (6000, 1, "draw_start", 0), (8000, 1, "draw_end", 0),
                    (10000, 2, "frame_start", 0), (12000, 2, "draw_start", 0),
                    (15000, 3, "frame_start", 0), (16000, 3, "draw_end", 0),
                    (17000, 3, "mouse_motion", 0), (17010, 3, "rectangle", 0),
                ]:
                    writer.writerow([time, frame, kind, 1, 0, 0, 0, 2, duration])
            result = read_capture(path)
        self.assertEqual(result["frames"][0][2:6], [10, 3, 2, 5])
        self.assertEqual(result["frames"][1][2:6], [5, 0, 3, 2])
        self.assertEqual(len(result["inputs"]), 2)
        self.assertAlmostEqual(result["inputs"][0][1], 7)
        self.assertAlmostEqual(result["inputs"][1][1], 6.5)

    def test_warmup_and_empty_drag_windows(self):
        capture = {"name": "test", "frames": [[0, 1, 200], [5, 2, 10], [16, 3, 30]],
                   "inputs": [[0, 200, .1]]}
        result = summarize(capture, 16.67, 33.33, 5)
        self.assertEqual(result["frames"]["count"], 2)
        self.assertEqual(result["frames"]["max_ms"], 30)
        self.assertEqual(result["feedback"]["count"], 0)
        self.assertIsNone(result["rolling_10_seconds"][0]["feedback"]["p95_ms"])

    def test_rendering_warmup_unavailable_gpu_and_global_setup_deduplication(self):
        capture = {"name": "test", "frames": [], "inputs": [], "rendering": [
            [0, 0, 0, "main", 100, 100, 100, 50, 100, 10],
            [5, 1, 1, "main", 1, None, .1, 5, 20, 3],
            [5, 1, 1, "minimap", .5, .7, .1, 2, 4, 1],
            [6, 2, 2, "main", 2, 4, .2, 6, 21, 4],
            [6, 2, 2, "minimap", .6, .8, .2, 3, 5, 2],
        ]}
        rendering = summarize(capture, 16.67, 33.33, 5)["rendering"]
        self.assertEqual(rendering["frame_setup_cpu"]["count"], 2)
        self.assertEqual(rendering["viewports"]["main"]["cpu"]["max_ms"], 2)
        self.assertEqual(rendering["viewports"]["main"]["gpu"]["count"], 1)
        self.assertEqual(rendering["viewports"]["minimap"]["peak_draw_calls"], 3)

    def test_render_csv_clock_validation_and_zero_measurements(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "client.render.csv"
            path.write_text('# utc_origin=test; alignment=latest_available\n'
                            'usec,observed_process_frame,observed_drawn_frame,viewport,cpu_ms,gpu_ms,setup_cpu_ms,draw_calls,primitives,objects\n'
                            '1000000,2,1,"minimap, viewport",1.5,0,.2,3,12,2\n')
            rows = read_render_capture(path, "# utc_origin=test; origin_usec=0")
            self.assertEqual(rows[0], [1, 2, 1, "minimap, viewport", 1.5, None, .2, 3, 12, 2])
            with self.assertRaises(ValueError):
                read_render_capture(path, "# utc_origin=another_capture")


if __name__ == "__main__":
    unittest.main()
