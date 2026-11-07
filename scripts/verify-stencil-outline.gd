extends SceneTree

var failures=0

func _initialize():
    call_deferred("run")

func check(condition: bool, message: String):
    if not condition:
        failures+=1
        push_error(message)

func settle():
    for i in 5:
        await process_frame
    await RenderingServer.frame_post_draw

func snapshot() -> Image:
    var result=root.get_texture().get_image()
    result.convert(Image.FORMAT_RGBA8)
    return result

func changes(a: Image, b: Image) -> int:
    var aa=a.get_data()
    var bb=b.get_data()
    var result=0
    for i in range(0, aa.size(), 4):
        if abs(int(aa[i])-int(bb[i]))>3 or abs(int(aa[i+1])-int(bb[i+1]))>3 or abs(int(aa[i+2])-int(bb[i+2]))>3:
            result+=1
    return result

func interior_changes(on: Image, off: Image, mask: Image) -> int:
    var count=0
    for y in mask.get_height():
        for x in mask.get_width():
            var nearest=mask.get_pixel(x,y)
            if fmod(nearest.r,float(mask.get_width()))!=x or nearest.g!=y:
                continue
            var a=on.get_pixel(x,y)
            var b=off.get_pixel(x,y)
            if max(abs(a.r-b.r),max(abs(a.g-b.g),abs(a.b-b.b)))>0.02:
                count+=1
    return count

func outline_center(frame: Image, baseline: Image) -> float:
    var total=0
    var count=0
    for y in frame.get_height():
        for x in frame.get_width():
            var color=frame.get_pixel(x,y)
            var before=baseline.get_pixel(x,y)
            if max(abs(color.r-before.r),max(abs(color.g-before.g),abs(color.b-before.b)))>0.05:
                total+=x
                count+=1
    return float(total)/max(count,1)

func materials(view: Node) -> Array:
    var result=[]
    for mesh in view.find_children("*","MeshInstance3D",true,false):
        result.append(mesh.material_override)
        for surface in mesh.mesh.get_surface_count():
            result.append(mesh.get_surface_override_material(surface))
    return result

func run():
    if "--game" in OS.get_cmdline_user_args():
        await run_game()
        return
    var world=Node3D.new()
    root.add_child(world)
    var env=WorldEnvironment.new()
    env.environment=Environment.new()
    env.environment.background_mode=Environment.BG_COLOR
    env.environment.background_color=Color(0.08,0.09,0.1)
    env.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
    env.environment.ambient_light_color=Color.WHITE
    world.add_child(env)
    var camera=Camera3D.new()
    camera.cull_mask=5
    world.add_child(camera)
    camera.position=Vector3(0,5,7)
    camera.look_at(Vector3(0,1,0))
    var original_compositor=Compositor.new()
    var existing=load("res://Assets/FX/StencilOutline/HoverStencilOutline.tres").duplicate()
    existing.enabled=false
    original_compositor.compositor_effects=[existing]
    camera.compositor=original_compositor
    var paths=["Heroes/PickleKnight","Heroes/SnailHead","Heroes/Skinwalker","Heroes/CrystalGiant","Heroes/AllHairWizard","Objects/Turret2","Mobs/CaperCreep"]
    var last=null
    var first=null
    var renderer=null
    for path in paths:
        var view=load("res://Scenes/"+path+".tscn").instantiate()
        world.add_child(view)
        if path=="Objects/Turret2":
            view.OnInitialize()
            var ring=view.get_node("LoadingIndicator")
            ring.visible=true
            check(ring.is_in_group("hover_outline_exclude"),"turret cooldown ring must be excluded from the outline")
        if first==null:
            first=view
        var animation=view.get_node_or_null("AnimationPlayer")
        if animation and animation.has_animation(view.IdleAnimationOverride):
            animation.play(view.IdleAnimationOverride)
            animation.seek(0.5,true)
            animation.pause()
        if last:
            last.visible=false
        var originals=materials(view)
        view.SetHovered(true)
        await settle()
        var active=camera.get_node("HoverOutlineRenderer")
        check(renderer==null or renderer==active,"target switches must reuse the renderer")
        renderer=active
        check(not active.has_node("SilhouetteMask"),"stencil must not use a mask viewport")
        check(camera.compositor!=null,"camera must have a compositor")
        check(camera.compositor.compositor_effects.size()==2,"existing compositor effects must be preserved")
        check(camera.compositor.compositor_effects[0]==existing,"existing effects must retain their identity")
        var on=snapshot()
        var effect=camera.compositor.compositor_effects[-1]
        var nearest=effect.debug_textures[effect._passes&1].get_image()
        on.save_png("user://stencil-"+path.get_file()+".png")
        view.SetHovered(false)
        await settle()
        var off=snapshot()
        check(changes(on,off)>50,path+": outline must be visible")
        check(interior_changes(on,off,nearest)==0,path+": interior must remain unchanged")
        check(materials(view)==originals,path+": original materials must be restored")
        view.SetHovered(true)
        view.OnDeactivate()
        check(not view.IsHovered,"deactivation must clear hover")
        check(not camera.compositor.compositor_effects[-1].enabled,"deactivation must stop rendering")
        last=view
    first.visible=true
    first.position.x=-1.5
    last.position.x=1.5
    last.SetHovered(false)
    await settle()
    var baseline=snapshot()
    first.SetHovered(true)
    await settle()
    check(outline_center(snapshot(),baseline)<320,"first target must be outlined")
    first.SetHovered(false)
    last.SetHovered(true)
    await RenderingServer.frame_post_draw
    check(outline_center(snapshot(),baseline)>320,"switch must not retain the previous target")
    first.visible=false
    last.position.x=0
    last.SetHovered(true)
    await settle()
    var wall=MeshInstance3D.new()
    wall.mesh=BoxMesh.new()
    wall.mesh.size=Vector3(12,12,1)
    world.add_child(wall)
    wall.global_transform=camera.global_transform.translated_local(Vector3(0,0,-3))
    await settle()
    var occluded=snapshot()
    last.SetHovered(false)
    await settle()
    check(changes(occluded,snapshot())==0,"outline must not show through foreground geometry")
    wall.visible=false
    last.SetHovered(true)
    root.size=Vector2i(803,607)
    await settle()
    check(camera.compositor.compositor_effects[-1].resolution==Vector2i(803,607),"buffers must follow viewport resizing")
    var resized=snapshot()
    last.SetHovered(false)
    await settle()
    check(changes(resized,snapshot())>50,"outline must survive resizing")
    last.SetHovered(true)
    last.queue_free()
    await settle()
    check(not camera.compositor.compositor_effects[-1].enabled,"freed target must stop rendering")
    renderer.queue_free()
    await settle()
    check(camera.compositor==original_compositor,"removing the renderer must restore the camera compositor")
    original_compositor.compositor_effects=[]
    existing.release_resources()
    world.queue_free()
    await settle()
    print("STENCIL_CHECKS: failures=",failures)
    quit(1 if failures else 0)

func run_game():
    var game=load("res://Scenes/Singleplayer.tscn").instantiate()
    root.add_child(game)
    current_scene=game
    await create_timer(2.0).timeout
    var hero=null
    for unit in get_nodes_in_group("units"):
        if unit.get_script() and unit.get_script().resource_path.ends_with("/HeroEntity.cs"):
            hero=unit
            break
    if not hero:
        check(false,"singleplayer hero must spawn")
        quit(1)
        return
    var camera=game.get_node("Camera3D")
    camera.set_process(false)
    camera.position=hero.global_position+Vector3(0,3,4)
    camera.look_at(hero.global_position+Vector3.UP)
    hero.SetSelected(true)
    hero.SetHovered(true)
    await settle()
    check(camera.compositor.compositor_effects[-1].enabled,"live hero must enable stencil")
    snapshot().save_png("user://stencil-game.png")
    hero.SetHovered(false)
    await settle()
    check(camera.compositor.compositor_effects[-1].selection_active,"unhover must preserve selected hero outline")
    check(not camera.compositor.compositor_effects[-1].hover_active,"unhover must disable hover layer")
    snapshot().save_png("user://stencil-game-off.png")
    hero.SetHovered(true)
    hero.OnDeactivate()
    check(not camera.compositor.compositor_effects[-1].enabled,"deactivation must disable stencil")
    game.queue_free()
    await settle()
    print("STENCIL_GAME_CHECKS: failures=",failures)
    quit(1 if failures else 0)
