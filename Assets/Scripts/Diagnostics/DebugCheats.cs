#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugCheats : MonoBehaviour
{
    private const int StackedAces = 4;

    // 개발 빌드와 에디터에서 테스트 키 처리기를 하나 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject(nameof(DebugCheats));
        DontDestroyOnLoad(go);
        go.AddComponent<DebugCheats>();
    }

    // 호스트에서 기능 키로 아이템 지급과 에이스 쌓기를 실행한다
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        GameManager gm = GameManager.Instance;
        if (keyboard == null || gm == null) return;
        if (NetworkGameManager.Instance != null && !NetworkGameManager.Instance.IsHost) return;

        if (keyboard.f1Key.wasPressedThisFrame) gm.DebugGiveItemToBoth(ItemType.Magnifier);
        if (keyboard.f2Key.wasPressedThisFrame) gm.DebugGiveItemToBoth(ItemType.Flash);
        if (keyboard.f3Key.wasPressedThisFrame) gm.DebugGiveItemToBoth(ItemType.Bribe);
        if (keyboard.f4Key.wasPressedThisFrame) gm.DebugStackAces(StackedAces);
    }
}
#endif
