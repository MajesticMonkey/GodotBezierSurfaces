#ifndef GDNURBS_H
#define GDNURBS_H

#include <vector>

#include <godot_cpp/core/math.hpp>

#include <godot_cpp/classes/material.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>
#include <godot_cpp/classes/array_mesh.hpp>
#include <godot_cpp/classes/mesh.hpp>
#include <godot_cpp/classes/sphere_mesh.hpp>
#include <godot_cpp/classes/standard_material3d.hpp>

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
    class AbstractNURB : public MeshInstance3D {
        

        private:
            
            
        
        protected:
            
            Ref<godot::ArrayMesh> MeshShape;
            Ref<godot::SphereMesh> CPMesh;
            int VPS; // Verticies Per Side
            bool Selected = false;
            bool ChildrenEnabled = false;
            godot::String Prefix = "ControlPoint_";

            static void _bind_abstract_methods();

        public:
            bool AutoUpdate = true;

            
            
    };

    class NURB : public AbstractNURB {
        GDCLASS(NURB, AbstractNURB);

        private:
            Ref<godot::Material> SurfaceMat;
            godot::Ref<godot::StandardMaterial3D> CNMat = memnew(StandardMaterial3D);
            godot::PackedVector4Array SceneSaveNetwork;
            std::array<Eigen::Matrix<float, 4, 4>, 4> CPNetwork; // X, Y, Z, Weight
            std::array<godot::MeshInstance3D*, 16> CPStorage;
            inline static const Eigen::Matrix<float, 4, 4> B{
                {1, -3, 3, -1},
                {0, 3, -6, 3},
                {0, 0, 3, -3},
                {0, 0, 0, 1}
            };
            inline static const Eigen::Matrix<float, 4, 4> DB{
                {-3, 6, -3, 0},
                {3, -12, 9, 0},
                {0, 6, -9, 0},
                {0, 0, 3, 0}
            };
            
            void EnableChildren(
                
            );

            void DisableChildren(
                
            );

            void CreateChildren(

            );

            godot::MeshInstance3D* CreateControlPoint(
                Vector2i UV
            );

            void ReloadSurface(

            );

            template <typename T>
            std::conditional_t<std::is_same_v<T, Vector3>, godot::PackedVector3Array, godot::PackedVector2Array>
            WindTriangles(
                std::vector<T> verticies,
                int VPS
            );

            struct MeshData {
                std::vector<Vector2> uvs;
                std::vector<Vector3> positions;
                std::vector<Vector3> normals;
            };

            MeshData IterateOverParametricPoints(
                const std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CN,
                const Eigen::Matrix<float, 4, 4, 0, 4, 4> B,
                const Eigen::Matrix<float, 4, 4, 0, 4, 4> DB,
                const int VertexDensity
            );

            Vector4 ComputeParametricPoint(
                float u,
                float v,
                std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CNW,
                Eigen::Matrix<float, 4, 4, 0, 4, 4> NB,
                Eigen::Matrix<float, 4, 4, 0, 4, 4> MB,
                int VertexDensity
            );
            
            
    
        protected:
            static void _bind_methods();
            
            bool _validate_property(
                godot::PropertyInfo &p_property
            ) const;

        public:
            void _enter_tree() override;
            void _exit_tree() override;
            void _ready() override;
            void _process(double delta) override;

            void _selection_changed();

            void UpdateSceneSaveNetwork(std::array<Eigen::Matrix<float, 4, 4>, 4> CN);
            void SetSceneSaveNetwork(const PackedVector4Array &Network);
            godot::PackedVector4Array GetSceneSaveNetwork() const;

            void SetSurfaceMat(const Ref<Material> &M);
            Ref<Material> GetSurfaceMat() const;

            NURB();
            ~NURB();
        
    };

    class DynamicNURB : public AbstractNURB {
        GDCLASS(DynamicNURB, AbstractNURB);

        private:
            Vector2i CNSize;
            std::vector<std::array<float, 4>> CPNetwork[4]; // X, Y, Z, Weight
            std::vector<float> MB;
            std::vector<float> NB;

        protected:
            static void _bind_methods();

        public:
            DynamicNURB();
            ~DynamicNURB();
    };
}

#endif