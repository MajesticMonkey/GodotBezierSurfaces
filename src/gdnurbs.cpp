#include "gdnurbs.h"

#include <godot_cpp/core/class_db.hpp>

#include <godot_cpp/classes/array_mesh.hpp>
#include <godot_cpp/classes/editor_interface.hpp>
#include <godot_cpp/classes/editor_selection.hpp>
#include <godot_cpp/classes/engine.hpp>
#include <godot_cpp/classes/mesh.hpp>
#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/classes/resource_saver.hpp>
#include <godot_cpp/classes/scene_tree.hpp>
#include <godot_cpp/classes/sphere_mesh.hpp>

#include <godot_cpp/variant/packed_vector2_array.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/utility_functions.hpp>
#include <godot_cpp/variant/vector2i.hpp>
#include <godot_cpp/variant/vector2.hpp>
#include <godot_cpp/variant/vector3.hpp>
#include <godot_cpp/variant/vector4.hpp>

#include <cmath>
#include <Eigen/Dense>
#include <future>
#include <random>
#include <vector>


using namespace godot;

void NURB::_bind_methods(

)
{

}

NURB::NURB(

)
{
    set_process_internal(true);

    VPS = 32;
    CPMesh = memnew(SphereMesh);

    for(int i = 0; i < 4; i++) {
        CPNetwork[i] = Eigen::Matrix<float, 4, 4>::Zero();
    }

    std::random_device rd;
    std::mt19937 gen(rd());

    std::uniform_real_distribution<float> dis(0.0f, (float)VPS);

    for (int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            CPNetwork[0](i, j) = (i / 3.0f) * (float)VPS;
            CPNetwork[1](i, j) = 0;
            CPNetwork[2](i, j) = (j / 3.0f) * (float)VPS;
            CPNetwork[3](i, j) = 1.0f;
        }
    }
}

void NURB::_enter_tree (

)
{
    if (!Engine::get_singleton()->is_editor_hint()) {
        return;
    }

    EditorInterface *ei = EditorInterface::get_singleton();
    if (ei && ei->get_selection()) {
        ei->get_selection()->connect("selection_changed", callable_mp(this, &NURB::_selection_changed));
    }
}

void NURB::_exit_tree (

)
{
    if (!Engine::get_singleton()->is_editor_hint()) {
        return;
    }

    EditorInterface *ei = EditorInterface::get_singleton();
    if (ei && ei->get_selection()) {
        this->disconnect("selection_changed", callable_mp(this, &NURB::_selection_changed));
    }
}

void NURB::_selection_changed (

)
{
    EditorInterface *ei = EditorInterface::get_singleton();
    if (!ei) return;

    TypedArray<Node> selection = ei->get_selection()->get_selected_nodes();
    Selected = false;
    if (selection.size() > 0) {
        for (int i = 0; i < selection.size(); i++) {
            if (Object::cast_to<Node>(selection[i])->get_instance_id() == get_instance_id()) {
                EnableChildren();
                Selected = true;
            }
            for (int j = 0; j < 16; j++) {
                if (Object::cast_to<Node>(selection[i])->get_instance_id() == CPStorage[j]->get_instance_id()) {
                    EnableChildren();
                    Selected = true;
                }
            }
        }
        if (!Selected) {
            DisableChildren();
        }
    }
    else
    {
        DisableChildren();
    }
}

bool NURB::_validate_property(
    godot::PropertyInfo &p_property
) const
{
    if (p_property.name == godot::StringName("mesh")) {
        p_property.usage = godot::PROPERTY_USAGE_EDITOR;

        return true;
    }

    return false;
}

void NURB::_ready (

)
{
    ReloadSurface();
    
    set_mesh(MeshShape);

    CreateChildren();
}

void NURB::_process (
    double delta
)
{
    if (Selected && AutoUpdate)
    {
        bool changed = false;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                MeshInstance3D* ControlPoint = CPStorage[i * 4 + j];
                if (
                    CPNetwork[0](i, j) != ControlPoint->get_position().x ||
                    CPNetwork[1](i, j) != ControlPoint->get_position().y ||
                    CPNetwork[2](i, j) != ControlPoint->get_position().z
                ) // Replace 0 with the actual value you want to check
                {
                    changed = true;
                    CPNetwork[0](i, j) = ControlPoint->get_position().x;
                    CPNetwork[1](i, j) = ControlPoint->get_position().y;
                    CPNetwork[2](i, j) = ControlPoint->get_position().z;
                }
            }
        }
        if (changed)
        {
            ReloadSurface();

            set_mesh(MeshShape);
        }
    }  
}

NURB::~NURB(

)
{

}

void NURB::EnableChildren(

)
{
    if (!ChildrenEnabled) {
        for(int i = 0; i < 16; i++){
            add_child(CPStorage[i]);
            CPStorage[i]->set_owner(get_tree()->get_edited_scene_root());
        }
        ChildrenEnabled = true;
    }
}

void NURB::DisableChildren(

)
{
    if (ChildrenEnabled) {
        for(int i = 0; i < 16; i++){
            remove_child(CPStorage[i]);
        }
        ChildrenEnabled = false;
    }
}

void NURB::CreateChildren(

)
{
    godot::TypedArray<Node> children = get_children();
    if (children.size() > 0) {
        for (int i = 0; i < children.size(); i++) {
            Node3D *node = Object::cast_to<Node3D>(children[i]);
            if (node != nullptr && node->get_name().begins_with(Prefix)) {
                int u = godot::String(node->get_name())[Prefix.length()];
                int v = godot::String(node->get_name())[Prefix.length() + 2];
                CPNetwork[0](u, v) = node->get_position().x;
                CPNetwork[1](u, v) = node->get_position().y;
                CPNetwork[2](u, v) = node->get_position().z;
            }
        }
        
    }
    else
    {
        for(int i = 0; i < 16; i++){
            CPStorage[i] = CreateControlPoint(Vector2i(floor(i / 4), i % 4));
        }
    }
}

godot::MeshInstance3D* NURB::CreateControlPoint(
    Vector2i UV
)
{
    godot::MeshInstance3D* ControlPoint = memnew(MeshInstance3D);

    ControlPoint->set_mesh(CPMesh);

    ControlPoint->set_name(Prefix + String::num(UV.x, 0) + "_" + String::num(UV.y, 0));

    ControlPoint->set_position(Vector3(CPNetwork[0](UV.x, UV.y), CPNetwork[1](UV.x, UV.y), CPNetwork[2](UV.x, UV.y)));

    return ControlPoint;
}

void NURB::ReloadSurface(

)
{
    godot::UtilityFunctions::print("Reloading Surface");
    MeshData meshdata = std::async(std::launch::async, &NURB::IterateOverParametricPoints, this, CPNetwork, B, DB, VPS).get();

    MeshShape = memnew(ArrayMesh);

    godot::Array surfacearray;

    surfacearray.resize(Mesh::ARRAY_MAX);
    

    surfacearray[Mesh::ARRAY_TEX_UV] = godot::Variant(WindTriangles<Vector2>(meshdata.uvs, VPS)); // UVs
    surfacearray[Mesh::ARRAY_VERTEX] = godot::Variant(WindTriangles<Vector3>(meshdata.positions, VPS)); // Transforms
    surfacearray[Mesh::ARRAY_NORMAL] = godot::Variant(WindTriangles<Vector3>(meshdata.normals, VPS)); // Normals

    MeshShape->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, surfacearray);
}

template <typename T>
std::conditional_t<std::is_same_v<T, Vector3>, godot::PackedVector3Array, godot::PackedVector2Array>
NURB::WindTriangles(
    std::vector<T> verticies,
    int VPS
)
{
    using PackType = std::conditional_t<
        std::is_same_v<T, Vector3>, godot::PackedVector3Array,
        godot::PackedVector2Array
    >;

    PackType PackRAT; // Packed Reordered Array of Triangles

    for (int i = 0; i < VPS - 1; i++) {
        for (int j = 0; j < VPS - 1; j++) {
            PackRAT.append(verticies[i * VPS + j]);
            PackRAT.append(verticies[(i + 1) * VPS + j]);
            PackRAT.append(verticies[(i + 1) * VPS + (j + 1)]);

            PackRAT.append(verticies[(i + 1) * VPS + (j + 1)]);
            PackRAT.append(verticies[i * VPS + (j + 1)]);
            PackRAT.append(verticies[i * VPS + j]);
        }
    }

    return PackRAT;
}

NURB::MeshData NURB::IterateOverParametricPoints(
    const std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CN,
    const Eigen::Matrix<float, 4, 4, 0, 4, 4> B,
    const Eigen::Matrix<float, 4, 4, 0, 4, 4> DB,
    const int VertexDensity
)
{
    std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CNWeighted;
    for (int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            CNWeighted[0](i, j) = CN[0](i, j) * CN[3](i, j);
            CNWeighted[1](i, j) = CN[1](i, j) * CN[3](i, j);
            CNWeighted[2](i, j) = CN[2](i, j) * CN[3](i, j);
            CNWeighted[3](i, j) = CN[3](i, j);
        }
    }

    MeshData PositionsAndNormals;
    for (int u = 0; u < VertexDensity; u++) {
        float uF = (float)u / (VertexDensity - 1.0f);
        for (int v = 0; v < VertexDensity; v++) {
            float vF = (float)v / ((float)VertexDensity - 1.0f);

           
            Vector4 ForkliftT = ComputeParametricPoint(uF, vF, CNWeighted, B, B, VertexDensity);
            Vector4 ForkliftA = ComputeParametricPoint(uF, vF, CNWeighted, DB, B, VertexDensity);
            Vector4 ForkliftB = ComputeParametricPoint(uF, vF, CNWeighted, B, DB, VertexDensity);

            Vector3 TangentA = Vector3(ForkliftA.x - (ForkliftA.w * ForkliftT.x), ForkliftA.y - (ForkliftA.w * ForkliftT.y), ForkliftA.z - (ForkliftA.w * ForkliftT.z));
            Vector3 TangentB = Vector3(ForkliftB.x - (ForkliftB.w * ForkliftT.x), ForkliftB.y - (ForkliftB.w * ForkliftT.y), ForkliftB.z - (ForkliftB.w * ForkliftT.z));
            
            PositionsAndNormals.uvs.emplace_back(Vector2(uF, vF));
            PositionsAndNormals.positions.emplace_back(Vector3(ForkliftT.x / ForkliftT.w, ForkliftT.y / ForkliftT.w, ForkliftT.z / ForkliftT.w));
            PositionsAndNormals.normals.emplace_back((TangentA.cross(TangentB)).normalized());
        }
    }
    return PositionsAndNormals;
}

Vector4 NURB::ComputeParametricPoint(
    float u,
    float v,
    std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CNW,
    Eigen::Matrix<float, 4, 4, 0, 4, 4> NB,
    Eigen::Matrix<float, 4, 4, 0, 4, 4> MB,
    int VertexDensity
)
{
    Eigen::Vector4f pu;
    Eigen::Vector4f pv;

    pu << 1.0f, u, pow(u, 2.0f), pow(u, 3.0f);
    pv << 1.0f, v, pow(v, 2.0f), pow(v, 3.0f);
    
    Vector4 Point = Vector4(0.0f, 0.0f, 0.0f, 0.0f);

    Eigen::Matrix<float, 1, 4> pub = (MB * pu).transpose();
    Eigen::Matrix<float, 4, 1> bpv = (NB * pv);
/*
    Point.x = (pub * CNW[0] * bpv)(0, 0);
    Point.y = (pub * CNW[1] * bpv)(0, 0);
    Point.z = (pub * CNW[2] * bpv)(0, 0);
    Point.w = (pub * CNW[3] * bpv)(0, 0);
*/
    Point.x = pub.dot(CNW[0] * bpv);
    Point.y = pub.dot(CNW[1] * bpv);
    Point.z = pub.dot(CNW[2] * bpv);
    Point.w = pub.dot(CNW[3] * bpv);

    return Point;
}