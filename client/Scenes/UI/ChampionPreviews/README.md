# Champion previews

Open a champion's `.tscn` to edit its `Camera3D`, lights, and `ChampionPivot` placement. These settings apply when that champion is selected in the lobby.

The preview widget swaps the entire stage. Keep `Camera3D` and `ChampionPivot` as direct children of the stage, with the champion model under `ChampionPivot`.

Models instance `Scenes/Heroes/Presentation/`, which gameplay also uses. Adjust the model instance's transform here for preview placement; edit the shared presentation scene only for changes that should also affect gameplay.

The model's `AnimationPlayer.autoplay` selects the default idle. To override it for a preview, enable Editable Children on the model instance and set autoplay. The widget's `InitialAnimation` or `ShowFaction(factionId, animationName)` can select a different animation at runtime.

Camera transforms and zoom are preserved by default. `AutoFrame` on the widget opts into automatic camera placement.
