extends SceneTree
var saw_loading=false
var frames=0
var failures=0
var last_percent=0.0
var captured=false
func check(ok,message):
    if not ok:
        failures+=1
        push_error(message)
func _initialize():
    call_deferred("run")
func run():
    var lobby=load("res://Scenes/Lobby.tscn").instantiate()
    root.add_child(lobby)
    current_scene=lobby
    await process_frame
    lobby.call("OnStartLocal")
    var start=Time.get_ticks_msec()
    var screen=null
    while Time.get_ticks_msec()-start<90000:
        await process_frame
        frames+=1
        screen=null
        for child in root.get_children():
            if child.get_script() and child.get_script().resource_path.ends_with("/LoadingScreen.cs"):
                screen=child
                saw_loading=true
        if screen and screen.get_node("%Dismiss").visible:
            check(false,"loading must not fail")
            quit(1)
            return
        if screen:
            var percent=screen.get_node("%ProgressBar").value
            check(percent>=last_percent,"loading progress must not go backwards")
            last_percent=percent
            if percent>=96 and percent<100 and not captured:
                await RenderingServer.frame_post_draw
                root.get_texture().get_image().save_png("user://outline-prewarm-loading.png")
                captured=true
        if saw_loading and not screen:
            break
    check(saw_loading,"loading screen must appear")
    check(not screen,"loading screen must finish")
    check(last_percent==100,"loading must reach 100 percent")
    check(captured,"loading must remain visible during outline preparation")
    var camera=root.get_camera_3d()
    if not camera:
        check(false,"game camera must be current")
        quit(1)
        return
    var renderer=camera.get_node_or_null("HoverOutlineRenderer")
    check(renderer!=null and renderer.IsPrewarmed,"camera must be prewarmed before loading closes")
    if renderer:
        var effect=camera.compositor.compositor_effects[-1]
        check(effect.sc_pipelines.size()==6 and not effect.rebuild_pipelines,"GPU pipelines must be built")
        var buffers=effect._textures.duplicate()
        var count=renderer.MaterialVariantCount
        var group=Node3D.new()
        group.name="Team2"
        root.add_child(group)
        var enemy=load("res://Scenes/Mobs/CaperCreep.tscn").instantiate()
        group.add_child(enemy)
        var cpu=Time.get_ticks_usec()
        enemy.SetHovered(true)
        print("PREWARM_FIRST_ENEMY cpu_ms=",(Time.get_ticks_usec()-cpu)/1000.0)
        await process_frame
        await RenderingServer.frame_post_draw
        check(renderer.MaterialVariantCount==count,"first enemy hover must reuse prepared materials")
        check(effect._textures==buffers,"first hover must reuse GPU buffers")
        check(not camera.has_node("OutlinePrewarm"),"previews must be removed")
        check(camera.get_node("HoverOutlineRenderer")==renderer,"first hover must reuse the renderer")
        enemy.SetHovered(false)
        enemy.SetSelected(true)
        enemy.SetHovered(true)
        await process_frame
        await RenderingServer.frame_post_draw
        check(renderer.MaterialVariantCount==count,"selection plus hover must reuse prepared materials")
        enemy.OnDeactivate()
        group.queue_free()
    print("OUTLINE_LOADING_CHECKS failures=",failures," loading_frames=",frames," elapsed_ms=",Time.get_ticks_msec()-start)
    current_scene.queue_free()
    for i in 4:
        await process_frame
    quit(1 if failures else 0)
