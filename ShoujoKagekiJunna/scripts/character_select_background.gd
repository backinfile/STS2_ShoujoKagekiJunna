extends Control

func _ready():
	get_viewport().size_changed.connect(_fit_to_viewport)
	_fit_to_viewport()

func _process(_delta):
	_fit_to_viewport()

func _fit_to_viewport():
	var parent_canvas=get_parent() as CanvasItem
	if parent_canvas:
		var inverse=parent_canvas.get_global_transform().affine_inverse()
		position=inverse.origin
		rotation=inverse.get_rotation()
		scale=inverse.get_scale()
	size=get_viewport_rect().size
