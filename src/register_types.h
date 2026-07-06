#ifndef NURBS_REGISTER_TYPES_H
#define NURBS_REGISTER_TYPES_H

#include <godot_cpp/core/class_db.hpp>

using namespace godot;

void initialize_nurbs_module(ModuleInitializationLevel p_level);
void uninitialize_nurbs_module(ModuleInitializationLevel p_level);

#endif // NURBS_REGISTER_TYPES_H