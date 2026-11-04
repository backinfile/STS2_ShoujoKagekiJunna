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
    add_directory(packer, "res://ShoujoKagekiJunna")
    add_directory(packer, "res://.godot/imported")
    error = packer.flush()
    print("Junna PCK: ", destination, " result=", error)
    quit(0 if error == OK else 1)

func add_directory(packer: PCKPacker, path: String):
    for file in DirAccess.get_files_at(path):
        var full = path.path_join(file)
        var error = packer.add_file(full, full)
        if error != OK:
            push_error("Cannot pack " + full)
            quit(1)
    for directory in DirAccess.get_directories_at(path):
        add_directory(packer, path.path_join(directory))
