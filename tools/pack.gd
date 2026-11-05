extends SceneTree

func _initialize():
    var args = OS.get_cmdline_user_args()
    var destination = args[0] if args.size() else "res://dist/ShoujoKagekiJunna.pck"
    var packer = PCKPacker.new()
    var error = packer.pck_start(destination)
    if error != OK:
        push_error("Cannot create pack: " + str(error))
        quit(1)
        return
    error = add_directory(packer, "res://ShoujoKagekiJunna")
    if error != OK:
        quit(1)
        return
    error = packer.flush()
    print("Junna PCK: ", destination, " result=", error)
    quit(0 if error == OK else 1)

func add_directory(packer: PCKPacker, path: String) -> Error:
    for file in DirAccess.get_files_at(path):
        var full = path.path_join(file)
        # Imported resources load through their .import remap, without the source image.
        if FileAccess.file_exists(full + ".import"):
            continue
        var error = packer.add_file(full, full)
        if error != OK:
            push_error("Cannot pack " + full)
            return error
        if file.ends_with(".import"):
            var config = ConfigFile.new()
            error = config.load(full)
            if error != OK:
                push_error("Cannot read import " + full)
                return error
            for dependency in config.get_value("deps", "dest_files", PackedStringArray()):
                error = packer.add_file(dependency, dependency)
                if error != OK:
                    push_error("Cannot pack imported resource " + dependency)
                    return error
    for directory in DirAccess.get_directories_at(path):
        var error = add_directory(packer, path.path_join(directory))
        if error != OK:
            return error
    return OK
