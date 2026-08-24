using UnityEngine;
using System.Collections.Generic;

// 카드(Rank+Suit) 조합마다 스프라이트를 매핑해두는 데이터 에셋.
// 프로젝트 창에서 우클릭 → Create → DontBeTheJoker → Card Sprite Database로 생성 후
// 52장 스프라이트 + 카드 뒷면(숨김) 스프라이트를 인스펙터에서 채워넣으면 됨.
[CreateAssetMenu(fileName = "CardSpriteDatabase", menuName = "DontBeTheJoker/Card Sprite Database")]
public class CardSpriteDatabase : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Rank rank;
        public Suit suit;
        public Sprite sprite;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();
    [SerializeField] private Sprite _cardBackSprite;
    [SerializeField] private Sprite _jokerSprite; // 현재 게임 로직상 조커는 손패에 안 들어가서 표시에 직접 안 쓰이지만, 나중에 연출용으로 미리 담아둠

    // 최초 조회 시 한 번만 만들어두는 캐시 (매번 리스트를 순회하지 않도록)
    private Dictionary<(Rank, Suit), Sprite> _lookup;

    private void BuildLookupIfNeeded()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<(Rank, Suit), Sprite>();
        foreach (Entry entry in _entries)
            _lookup[(entry.rank, entry.suit)] = entry.sprite;
    }

    public Sprite GetSprite(Card card)
    {
        BuildLookupIfNeeded();

        if (_lookup.TryGetValue((card.Rank, card.Suit), out Sprite sprite) && sprite != null)
            return sprite;

        Debug.LogWarning($"[CardSpriteDatabase] {card}에 해당하는 스프라이트가 등록되지 않음");
        return _cardBackSprite; // 안전한 폴백
    }

    public Sprite CardBackSprite => _cardBackSprite;
    public Sprite JokerSprite => _jokerSprite;
}