extends SceneTree

func _initialize():
    var args = OS.get_cmdline_user_args()
    if args.size() < 2 or not ProjectSettings.load_resource_pack(args[0]):
        push_error("Cannot load built PCK")
        quit(1)
        return
    var manifest = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
    var checked = []
    for icon in manifest.icons:
        var texture = load("res://" + icon.file) as Texture2D
        if texture == null or texture.get_width() != 256 or texture.get_height() != 256:
            push_error("Invalid packed icon: " + icon.file)
            quit(1)
            return
        var source_path = args[1].get_base_dir().path_join("icons/" + icon.sourceKey + ".png")
        if FileAccess.get_sha256(source_path) != icon.sha256:
            push_error("Cleaned source hash differs: " + source_path)
            quit(1)
            return
        var expected = Image.load_from_file(source_path)
        # Match the existing texture importer setting process/fix_alpha_border.
        expected.fix_alpha_edges()
        var actual = visible_hash(texture.get_image())
        if actual != visible_hash(expected):
            push_error("Packed pixels differ from cleaned source: " + icon.file)
            quit(1)
            return
        checked.append({"key": icon.key, "size": [256, 256], "visibleRgbaSha256": actual})
    var report = {"passed": checked.size(), "source": "fresh-project-pck", "icons": checked}
    if args.size() > 2:
        var output = FileAccess.open(args[2], FileAccess.WRITE)
        output.store_string(JSON.stringify(report, "  ") + "\n")
        output.close()
    print("PASS: loaded ", checked.size(), " Power icons from the PCK; sizes and visible RGBA pixels match cleaned originals")
    quit(0)

func visible_hash(image: Image) -> String:
    image.convert(Image.FORMAT_RGBA8)
    var pixels = image.get_data()
    for i in range(0, pixels.size(), 4):
        if pixels[i + 3] == 0:
            pixels[i] = 0
            pixels[i + 1] = 0
            pixels[i + 2] = 0
    var hash = HashingContext.new()
    hash.start(HashingContext.HASH_SHA256)
    hash.update(pixels)
    return hash.finish().hex_encode()
