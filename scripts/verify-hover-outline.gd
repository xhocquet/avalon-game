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
    for y in range(1,mask.get_height()-1):
        for x in range(1,mask.get_width()-1):
            var interior=true
            for yy in range(y-1,y+2):
                for xx in range(x-1,x+2):
                    if mask.get_pixel(xx,yy).b<0.99:
                        interior=false
            if not interior:
                continue
            for yy in range(y*2,y*2+2):
                for xx in range(x*2,x*2+2):
                    if xx>=on.get_width() or yy>=on.get_height():
                        continue
                    var a=on.get_pixel(xx,yy)
                    var b=off.get_pixel(xx,yy)
                    if max(abs(a.r-b.r),max(abs(a.g-b.g),abs(a.b-b.b)))>0.02:
                        count+=1
    return count

func mask_center(mask: Image) -> float:
    var total=0.0
    var count=0
    for y in mask.get_height():
        for x in mask.get_width():
            if mask.get_pixel(x,y).b>0.99:
                total+=x
                count+=1
    return total/max(count,1)

func run():
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
    var paths=["Heroes/PickleKnight","Heroes/SnailHead","Heroes/Skinwalker","Heroes/CrystalGiant","Heroes/AllHairWizard","Mobs/CaperCreep"]
    var last=null
    var first=null
    var renderer=null
    for path in paths:
        var view=load("res://Scenes/"+path+".tscn").instantiate()
        world.add_child(view)
        if first==null:
            first=view
        var animation=view.get_node_or_null("AnimationPlayer")
        if animation and animation.has_animation(view.IdleAnimationOverride):
            animation.play(view.IdleAnimationOverride)
            animation.seek(0.5,true)
            animation.pause()
        if last:
            last.visible=false
        view.SetHovered(true)
        await settle()
        var active=camera.get_node("HoverOutlineRenderer")
        check(renderer==null or renderer==active,"target switches must reuse the renderer")
        renderer=active
        var mask=renderer.get_node("SilhouetteMask")
        check(mask.size==Vector2i(320,240),"mask must be half resolution")
        var on=snapshot()
        var silhouette=mask.get_texture().get_image()
        view.SetHovered(false)
        await settle()
        var off=snapshot()
        check(changes(on,off)>50,path+": outline must be visible")
        check(interior_changes(on,off,silhouette)==0,path+": interior must remain unchanged")
        check(mask.render_target_update_mode==SubViewport.UPDATE_DISABLED,"idle mask must stop rendering")
        view.SetHovered(true)
        view.OnDeactivate()
        check(not view.IsHovered,"deactivation must clear hover")
        check(mask.render_target_update_mode==SubViewport.UPDATE_DISABLED,"deactivation must stop rendering")
        last=view
    first.visible=true
    first.position.x=-1.5
    last.position.x=1.5
    first.SetHovered(true)
    await RenderingServer.frame_post_draw
    var mask=renderer.get_node("SilhouetteMask")
    check(mask_center(mask.get_texture().get_image())<mask.size.x*0.5,"first target must render immediately")
    first.SetHovered(false)
    last.SetHovered(true)
    await RenderingServer.frame_post_draw
    check(mask_center(mask.get_texture().get_image())>mask.size.x*0.5,"switch must not retain the previous silhouette")
    first.visible=false
    last.position.x=0
    var wall=MeshInstance3D.new()
    wall.mesh=BoxMesh.new()
    wall.mesh.size=Vector3(12,12,1)
    world.add_child(wall)
    wall.global_transform=camera.global_transform.translated_local(Vector3(0,0,-3))
    last.SetHovered(true)
    await settle()
    var occluded=snapshot()
    last.SetHovered(false)
    await settle()
    check(changes(occluded,snapshot())==0,"outline must not show through foreground geometry")
    wall.visible=false
    last.SetHovered(true)
    await settle()
    root.size=Vector2i(800,600)
    await settle()
    check(renderer.get_node("SilhouetteMask").size==Vector2i(400,300),"mask must follow viewport resizing")
    last.queue_free()
    await settle()
    check(renderer.get_node("SilhouetteMask").render_target_update_mode==SubViewport.UPDATE_DISABLED,"freed target must stop rendering")
    print("HOVER_MASK_CHECKS: failures=",failures)
    quit(1 if failures else 0)
