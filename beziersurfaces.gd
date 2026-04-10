@tool
extends EditorPlugin

var BezIcon = load("res://addons/beziersurfaces/Textures/BezierSurfaceIcon.png")

var ControlPointScript = load("res://addons/beziersurfaces/Scripts/ControlPoint.cs")

var BezierSurfaceBuilderScript = load("res://addons/beziersurfaces/Scripts/BezierSurfaceBuilder.cs")

var ControlPointInspector

var LoadingBar


func _enter_tree() -> void:
	add_custom_type("ControlPoint", "MeshInstance3D", ControlPointScript, BezIcon)

	add_custom_type("BezierSurfaceBuilder", "Node3D", BezierSurfaceBuilderScript, BezIcon)

	LoadingBar = preload("res://addons/beziersurfaces/Scripts/builder_inspector_features.gd").new()
	add_inspector_plugin(LoadingBar)

	ControlPointInspector = preload("res://addons/beziersurfaces/Scripts/control_point_inspector_features.gd").new()
	add_inspector_plugin(ControlPointInspector)


func _exit_tree() -> void:
	remove_custom_type("BezierSurfaceBuilder")

	remove_custom_type("ControlPoint")

	remove_inspector_plugin(LoadingBar)

	remove_inspector_plugin(ControlPointInspector)
