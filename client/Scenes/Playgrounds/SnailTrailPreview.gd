extends Node3D

const SEGMENT = preload("res://Scenes/FX/Skills/SnailTrail.tscn")
var _elapsed: float = 0.0
var _next_drop: float = 0.0
var _count: int = 0
var _segments: Array[Node3D] = []

func _process(delta: float) -> void:
    _elapsed += delta
    if _count < 8 and _elapsed >= _next_drop:
        var segment := SEGMENT.instantiate() as Node3D
        add_child(segment)
        segment.position = Vector3(-4.5 + _count * 1.3, 0.0, sin(_count * 0.55) * 0.7)
        segment.configure_segment(1.0, 4.5, float(_count) * 13.7)
        _segments.append(segment)
        _count += 1
        _next_drop += 0.4
    if _elapsed >= 8.0:
        for segment in _segments:
            segment.queue_free()
        _segments.clear()
        _elapsed = 0.0
        _next_drop = 0.0
        _count = 0
