extends SceneTree

func _initialize():
    var packer := PCKPacker.new()
    assert(packer.pck_start("res://icons.pck") == OK)
    var rows := []
    for key in ["green_gem", "burning_gem", "green_gem_outline", "burning_gem_outline"]:
        var path: String = "res://ShoujoKagekiJunna/images/relics/" + key + ".png"
        var texture = load(path)
        assert(texture is Texture2D)
        assert(texture.get_size() == Vector2(128, 128))
        var image: Image = texture.get_image()
        assert(image.get_pixel(0, 0).a == 0)
        assert(packer.add_file(path + ".import", path + ".import") == OK)
        var config := ConfigFile.new()
        assert(config.load(path + ".import") == OK)
        for dependency in config.get_value("deps", "dest_files", PackedStringArray()):
            assert(packer.add_file(dependency, dependency) == OK)
        rows.append({"path": path, "width": texture.get_width(), "height": texture.get_height(), "transparentCorner": true})
    assert(packer.flush() == OK)
    assert(ProjectSettings.load_resource_pack("res://icons.pck"))
    var report := FileAccess.open("res://validation.json", FileAccess.WRITE)
    report.store_string(JSON.stringify({"godot": Engine.get_version_info().string, "importedTextures": rows, "pckRoundTrip": true}, "  "))
    print("Validated four imported relic textures and PCK round-trip")
    quit()
