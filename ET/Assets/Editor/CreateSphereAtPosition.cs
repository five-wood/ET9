using UnityEditor;
using UnityEngine;

public class CreateSphereAtPosition
{
    [MenuItem("Tools/创建球体 (124, 35, 134)")]
    public static void CreateSphere()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Sphere";
        sphere.transform.position = new Vector3(124f, 35f, 134f);

        // 在 Hierarchy 中选中并高亮显示
        Selection.activeGameObject = sphere;
        EditorGUIUtility.PingObject(sphere);

        Debug.Log($"球体已创建，坐标：{sphere.transform.position}");
    }
}
