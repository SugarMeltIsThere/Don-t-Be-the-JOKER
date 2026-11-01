using UnityEngine;

public static class ItemProp
{
    // 아이템 프리팹의 겉모습만 복제해 연출용 오브젝트로 만든다(꺼진 부모 아래서 복제해 스크립트가 깨어나기 전에 떼어낸다)
    public static Transform Create(GameObject prefab, Transform parent)
    {
        GameObject holder = new GameObject("PropHolder");
        holder.SetActive(false);
        GameObject go = Object.Instantiate(prefab, holder.transform);
        go.name = prefab.name + "Prop";
        foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (Collider collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        Object.Destroy(holder);
        return go.transform;
    }
}
