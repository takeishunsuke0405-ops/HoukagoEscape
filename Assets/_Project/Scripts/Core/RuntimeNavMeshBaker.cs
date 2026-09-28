using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// ゲーム開始時に、相沢が歩ける範囲（ナビメッシュ）を作る。
/// 相沢の AI より先に動くように、実行順を早くしている。
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(NavMeshSurface))]
public class RuntimeNavMeshBaker : MonoBehaviour
{
    void Awake() => GetComponent<NavMeshSurface>().BuildNavMesh();
}
