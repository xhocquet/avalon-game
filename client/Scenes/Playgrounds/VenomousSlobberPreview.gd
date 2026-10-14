extends Node3D

var _elapsed: float = 2.5

func _process(delta: float) -> void:
    _elapsed += delta
    if _elapsed >= 2.5:
        $VenomousSlobber.play()
        _elapsed = 0.0
