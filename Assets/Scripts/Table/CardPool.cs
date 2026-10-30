using System.Collections.Generic;
using UnityEngine;

public class CardPool
{
    private readonly Stack<CardView> _free;
    private readonly Transform _root;
    private readonly Material _material;
    private readonly CardSpriteDatabase _db;
    private readonly int _prewarm;

    public int CreatedCount { get; private set; }

    // 카드를 미리 만들어 쌓아둔다
    public CardPool(Transform root, Material material, CardSpriteDatabase db, int prewarm)
    {
        _root = root;
        _material = material;
        _db = db;
        _prewarm = prewarm;
        _free = new Stack<CardView>(prewarm);

        for (int i = 0; i < prewarm; i++)
            Release(Create());
    }

    // 쉬고 있는 카드를 꺼내고 없으면 새로 만든다
    public CardView Get()
    {
        CardView view = _free.Count > 0 ? _free.Pop() : Create();
        view.gameObject.SetActive(true);
        return view;
    }

    // 카드를 숨기고 다시 쌓아둔다
    public void Release(CardView view)
    {
        view.Stop();
        view.gameObject.SetActive(false);
        _free.Push(view);
    }

    // 카드 오브젝트를 하나 만들고 미리 만든 수를 넘으면 경고한다
    private CardView Create()
    {
        CreatedCount++;
        if (CreatedCount > _prewarm)
            Debug.LogWarning($"[CardPool] 미리 만든 {_prewarm}장을 넘어 {CreatedCount}장째 생성");
        return CardView.Create(_root, _material, _db);
    }
}
