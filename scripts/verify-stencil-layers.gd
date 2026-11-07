extends "verify-stencil-outline.gd"

func refs(view: Node) -> Array:
    var result=[]
    for mesh in view.find_children("*","MeshInstance3D",true,false):
        if mesh.get_parent().name=="SelectionIndicator":
            continue
        for surface in mesh.mesh.get_surface_count():
            var mat=mesh.get_active_material(surface)
            if mat is BaseMaterial3D and mat.stencil_mode==BaseMaterial3D.STENCIL_MODE_CUSTOM:
                result.append(mat.stencil_reference)
    return result

func signal_strength(frame: Image, baseline: Image, left: bool) -> Vector3:
    var result=Vector3.ZERO
    for y in frame.get_height():
        for x in range(0 if left else frame.get_width()/2,frame.get_width()/2 if left else frame.get_width()):
            var a=frame.get_pixel(x,y)
            var b=baseline.get_pixel(x,y)
            result+=Vector3(max(a.r-b.r,0),max(a.g-b.g,0),max(a.b-b.b,0))
    return result

func run():
    var world=Node3D.new()
    root.add_child(world)
    var environment=WorldEnvironment.new()
    environment.environment=Environment.new()
    environment.environment.background_mode=Environment.BG_COLOR
    environment.environment.background_color=Color(0.03,0.03,0.03)
    environment.environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
    environment.environment.ambient_light_color=Color.WHITE
    world.add_child(environment)
    var camera=Camera3D.new()
    camera.cull_mask=5
    world.add_child(camera)
    camera.position=Vector3(0,5,8)
    camera.look_at(Vector3(0,0.5,0))
    var units=[]
    for team in [1,2]:
        var group=Node3D.new()
        group.name="Team"+str(team)
        world.add_child(group)
        var unit=load("res://Scenes/Mobs/CaperCreep.tscn").instantiate()
        group.add_child(unit)
        unit.position.x=-1.5 if team==1 else 1.5
        units.append(unit)
    var ally=units[0]
    var enemy=units[1]
    var originals=materials(ally)
    await settle()
    var off=snapshot()
    ally.SetSelected(true)
    enemy.SetSelected(true)
    await settle()
    var selected=snapshot()
    var blue=signal_strength(selected,off,true)
    var red=signal_strength(selected,off,false)
    check(blue.z>blue.x*1.5,"allied selection must be blue")
    check(red.x>red.z*1.5,"enemy selection must be red")
    check(refs(ally).has(5) and refs(enemy).has(6),"both teams must remain selected simultaneously")
    ally.SetHovered(true)
    await settle()
    var hovered=snapshot()
    var brighter=signal_strength(hovered,off,true)
    check(brighter.length()>blue.length()*1.3,"hover over selection must be visibly brighter")
    check(refs(ally).has(13),"hovered selection must retain both state bits")
    hovered.save_png("user://stencil-layers-ally.png")
    ally.SetHovered(false)
    enemy.SetHovered(true)
    await settle()
    var enemy_hover=snapshot()
    var brighter_red=signal_strength(enemy_hover,off,false)
    check(brighter_red.x>brighter_red.z*1.5,"enemy hover must retain its team color")
    check(brighter_red.length()>red.length()*1.3,"enemy hover must brighten selection")
    check(refs(ally).has(5),"clearing hover must retain allied selection")
    enemy_hover.save_png("user://stencil-layers-enemy.png")
    ally.SetSelected(false)
    check(materials(ally)==originals,"deselection must restore original materials")
    enemy.SetSelected(false)
    check(refs(enemy).has(10),"deselection must retain enemy hover")
    enemy.SetHovered(false)
    await settle()
    var effect=camera.compositor.compositor_effects[-1]
    check(not effect.enabled,"empty layers must stop dispatching")
    ally.SetSelected(true)
    ally.OnDeactivate()
    check(not ally.IsSelected and not effect.enabled,"pool deactivation must clear selection")
    var group=ally.get_parent()
    var batch=[]
    ally.visible=false
    enemy.visible=false
    for index in 12:
        var unit=load("res://Scenes/Mobs/CaperCreep.tscn").instantiate()
        group.add_child(unit)
        unit.position=Vector3((index%4-1.5)*0.7,0,(index/4-1)*0.7)
        unit.SetSelected(true)
        batch.append(unit)
    batch[5].SetHovered(true)
    await settle()
    check(effect.selection_active and effect.hover_active,"selection and hover layers must both run for a group")
    check(effect._textures.size()==2,"both layers must share the same two work buffers")
    check(camera.compositor.compositor_effects.size()==1,"unit count must not add compositor effects")
    for unit in batch:
        check(refs(unit).has(13 if unit==batch[5] else 5),"every selected minion must carry its selection state")
    snapshot().save_png("user://stencil-layers-group.png")
    for unit in batch:
        unit.OnDeactivate()
    check(not effect.enabled,"clearing a group must stop both layers")
    world.queue_free()
    await settle()
    print("STENCIL_LAYER_CHECKS: failures=",failures)
    quit(1 if failures else 0)
