#ifndef GDCP_H
#define GDCP_H

#include <vector>

#include <godot_cpp/core/math.hpp>

#include <godot_cpp/classes/collision_shape3d.hpp>
#include <godot_cpp/classes/material.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>
#include <godot_cpp/classes/array_mesh.hpp>
#include <godot_cpp/classes/mesh.hpp>
#include <godot_cpp/classes/sphere_mesh.hpp>
#include <godot_cpp/classes/standard_material3d.hpp>
#include <godot_cpp/classes/static_body3d.hpp>

#include <godot_cpp/variant/array.hpp>
#include <godot_cpp/variant/packed_vector2_array.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/packed_vector4_array.hpp>
#include <godot_cpp/variant/vector2i.hpp>
#include <godot_cpp/variant/vector2.hpp>
#include <godot_cpp/variant/vector3.hpp>


#include <vector>
#include <Eigen/Dense>





namespace godot {
    class ControlPoint : public MeshInstance3D {
        GDCLASS(ControlPoint, MeshInstance3D);

        private:
            float Weight = 1.0f;

        protected:
            static void _bind_methods();

        public:
            void SetWeight(const float &W);
            float GetWeight() const;
    };
}

#endif