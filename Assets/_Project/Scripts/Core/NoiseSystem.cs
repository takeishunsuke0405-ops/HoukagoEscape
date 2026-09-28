using System;
using UnityEngine;

/// <summary>
/// ドアの開け閉めなど、一瞬だけ鳴る物音を相沢に知らせる。
/// プレイヤーの足音は PlayerController.CurrentNoiseRadius を相沢が直接見ている。
/// </summary>
public static class NoiseSystem
{
    /// <summary>物音の位置と、届く範囲 (m)</summary>
    public static event Action<Vector3, float> NoiseMade;

    public static void Emit(Vector3 position, float radius) => NoiseMade?.Invoke(position, radius);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => NoiseMade = null;
}
