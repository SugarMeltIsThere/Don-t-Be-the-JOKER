// 이 파일은 반드시 Assets/Editor/ 폴더(또는 그 하위) 안에 두세요.
// UnityEditor 네임스페이스를 쓰는 에디터 전용 스크립트라, Editor 폴더 밖에 있으면
// 빌드에 포함되려다 컴파일 에러가 납니다.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class CardSpriteSheetImporter
{
    private const int CellWidth = 192;
    private const int CellHeight = 256;

    // 사용법: Project 창에서 카드 스프라이트시트 텍스처 하나를 선택한 상태로
    // 상단 메뉴 DontBeTheJoker → Build Card Sprite Database From Selected Sheet 클릭
    [MenuItem("DontBeTheJoker/Build Card Sprite Database From Selected Sheet")]
    private static void BuildDatabase()
    {
        Texture2D texture = Selection.activeObject as Texture2D;
        if (texture == null)
        {
            EditorUtility.DisplayDialog("카드 스프라이트 DB 생성 실패",
                "먼저 Project 창에서 카드 스프라이트시트 텍스처를 선택하세요.", "확인");
            return;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError("[CardSpriteSheetImporter] TextureImporter를 찾을 수 없음");
            return;
        }

        int cols = texture.width / CellWidth;
        int rows = texture.height / CellHeight;

        if (cols != 13 || rows < 5)
        {
            bool proceed = EditorUtility.DisplayDialog("크기가 예상과 다름",
                $"192x256 기준으로 계산하면 {cols}열 x {rows}행이 나옵니다.\n" +
                "13열 x 5행(총 65칸, 그중 54칸 사용)이 아니라면 매핑이 어긋날 수 있어요.\n" +
                "그래도 계속할까요?", "계속", "취소");
            if (!proceed) return;
        }

        // 1단계: 텍스처를 Multiple 스프라이트 모드로 전환하고 그리드로 슬라이싱
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;

        List<SpriteMetaData> metas = new List<SpriteMetaData>();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Unity 스프라이트 좌표는 텍스처 좌하단이 원점이라,
                // "위에서 몇 번째 줄인지"를 아래에서부터 세는 y로 변환
                int yFromBottom = texture.height - (row + 1) * CellHeight;

                metas.Add(new SpriteMetaData
                {
                    name = $"card_{row}_{col}",
                    rect = new Rect(col * CellWidth, yFromBottom, CellWidth, CellHeight),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }

        importer.spritesheet = metas.ToArray();
        importer.SaveAndReimport();

        // SaveAndReimport 직후 바로 서브 에셋을 읽으면, 재임포트가 아직
        // 완전히 반영되기 전이라 예전(수동 슬라이싱) 결과가 읽힐 수 있음.
        // 강제 동기 재임포트로 확실히 끝낸 뒤에 읽도록 함
        AssetDatabase.ImportAsset(path,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh();

        // 2단계: 슬라이싱된 서브스프라이트를 다시 불러와 이름(card_행_열)으로 파싱
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        Dictionary<(int row, int col), Sprite> sprites = new Dictionary<(int, int), Sprite>();

        foreach (Object asset in allAssets)
        {
            if (asset is Sprite sprite && sprite.name.StartsWith("card_"))
            {
                string[] parts = sprite.name.Split('_');
                int row = int.Parse(parts[1]);
                int col = int.Parse(parts[2]);
                sprites[(row, col)] = sprite;
            }
        }

        // 그래도 하나도 못 찾았다면, 실제로 어떤 이름의 서브 에셋들이 있는지
        // 진단용으로 남겨서 원인 파악을 쉽게 함
        if (sprites.Count == 0)
        {
            List<string> foundNames = new List<string>();
            foreach (Object asset in allAssets)
            {
                if (asset is Sprite s) foundNames.Add(s.name);
            }
            Debug.LogWarning(
                $"[CardSpriteSheetImporter] card_행_열 이름의 조각을 하나도 못 찾음. " +
                $"실제로 발견된 서브 에셋 이름: {string.Join(", ", foundNames)}");
        }

        // 3단계: 0~3행 = 스페이드/하트/다이아몬드/클로버, 4행 0열 = 조커, 1열 = 뒷면으로 매핑
        CardSpriteDatabase database = ScriptableObject.CreateInstance<CardSpriteDatabase>();
        SerializedObject so = new SerializedObject(database);
        SerializedProperty entriesProp = so.FindProperty("_entries");
        SerializedProperty backProp = so.FindProperty("_cardBackSprite");
        SerializedProperty jokerProp = so.FindProperty("_jokerSprite");

        Suit[] suitOrder = { Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Clover };
        Rank[] rankOrder =
        {
            Rank.Ace, Rank.Two, Rank.Three, Rank.Four, Rank.Five, Rank.Six,
            Rank.Seven, Rank.Eight, Rank.Nine, Rank.Ten, Rank.Jack, Rank.Queen, Rank.King
        };

        int entryIndex = 0;
        int mappedCount = 0;

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 13; col++)
            {
                if (!sprites.TryGetValue((row, col), out Sprite sprite)) continue;

                entriesProp.InsertArrayElementAtIndex(entryIndex);
                SerializedProperty entry = entriesProp.GetArrayElementAtIndex(entryIndex);
                entry.FindPropertyRelative("suit").enumValueIndex = (int)suitOrder[row];
                entry.FindPropertyRelative("rank").enumValueIndex = (int)rankOrder[col];
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                entryIndex++;
                mappedCount++;
            }
        }

        if (sprites.TryGetValue((4, 0), out Sprite jokerSprite))
            jokerProp.objectReferenceValue = jokerSprite;

        if (sprites.TryGetValue((4, 1), out Sprite backSprite))
            backProp.objectReferenceValue = backSprite;

        so.ApplyModifiedProperties();

        string savePath = "Assets/CardSpriteDatabase.asset";

        // 이미 같은 경로에 에셋이 있으면(재실행 시) 삭제 후 새로 저장
        if (AssetDatabase.LoadAssetAtPath<CardSpriteDatabase>(savePath) != null)
            AssetDatabase.DeleteAsset(savePath);

        AssetDatabase.CreateAsset(database, savePath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = database;
        EditorUtility.DisplayDialog("완료",
            $"CardSpriteDatabase 생성 완료: {savePath}\n" +
            $"카드 {mappedCount}장 매핑됨 (조커: {(jokerSprite != null ? "O" : "X")}, 뒷면: {(backSprite != null ? "O" : "X")})",
            "확인");
    }
}