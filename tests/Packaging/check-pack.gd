extends SceneTree

var expected: Dictionary = {}
var textures: Array[String] = []
var scenes: Array[String] = []

func _initialize():
    var args = OS.get_cmdline_user_args()
    if args.size() != 2:
        push_error("Usage: check-pack.gd -- <project directory> <PCK path>; run from an empty --path")
        quit(1)
        return
    if not collect_expected(args[0].path_join("ShoujoKagekiJunna"), "res://ShoujoKagekiJunna"):
        quit(1)
        return
    if not ProjectSettings.load_resource_pack(args[1]):
        push_error("Cannot mount PCK: " + args[1])
        quit(1)
        return
    var actual = collect_files("res://ShoujoKagekiJunna") + collect_files("res://.godot/imported")
    var unexpected: Array[String] = []
    var missing: Array[String] = []
    for path in actual:
        if not expected.has(path):
            unexpected.append(path)
    for path in expected:
        if not actual.has(path):
            missing.append(path)
    if not unexpected.is_empty() or not missing.is_empty():
        push_error("Package mismatch: %d unexpected, %d missing; unexpected examples: %s; missing: %s" % [unexpected.size(), missing.size(), unexpected.slice(0, 5), missing])
        quit(1)
        return
    for path in textures:
        var texture = load(path) as Texture2D
        if texture == null or texture.get_width() <= 0 or texture.get_height() <= 0:
            push_error("Cannot load packed texture: " + path)
            quit(1)
            return
    for path in scenes:
        var scene = load(path) as PackedScene
        if scene == null:
            push_error("Cannot load packed scene: " + path)
            quit(1)
            return
        var instance = scene.instantiate()
        instance.free()
    print("PASS: %d packed files, %d textures and %d scenes; no unrelated imports or duplicate source images" % [actual.size(), textures.size(), scenes.size()])
    quit(0)

func collect_expected(directory: String, resource_directory: String) -> bool:
    for file in DirAccess.get_files_at(directory):
        var source = directory.path_join(file)
        var resource = resource_directory.path_join(file)
        if FileAccess.file_exists(source + ".import"):
            continue
        expected[resource] = true
        if file.ends_with(".import"):
            var config = ConfigFile.new()
            if config.load(source) != OK:
                push_error("Cannot read import: " + source)
                return false
            for dependency in config.get_value("deps", "dest_files", PackedStringArray()):
                expected[dependency] = true
            textures.append(resource.trim_suffix(".import"))
        elif file.ends_with(".tscn"):
            scenes.append(resource)
    for child in DirAccess.get_directories_at(directory):
        if not collect_expected(directory.path_join(child), resource_directory.path_join(child)):
            return false
    return true

func collect_files(directory: String) -> Array[String]:
    var paths: Array[String] = []
    for file in DirAccess.get_files_at(directory):
        paths.append(directory.path_join(file))
    for child in DirAccess.get_directories_at(directory):
        paths.append_array(collect_files(directory.path_join(child)))
    return paths
