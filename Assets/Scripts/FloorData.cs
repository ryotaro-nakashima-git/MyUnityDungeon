using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1フロア分の迷宮データ。生成済みマップ・入口/ボス・色調・配置要素を保持する。
/// アクティブなフロアだけがグリッドに構築され、切替時に退避/復元される（DungeonFloorManager）。
/// </summary>
public class FloorData
{
    public DungeonGridSystem.TileType[,] map;
    public Vector2Int entrance;
    public Vector2Int boss;      // 入口から最遠＝最深部（最下層ではここに魔王）
    public Color tint;
    public bool isDeepest;       // 最下層＝魔王が実在するフロア
    public int size = 10;        // 🗺️ この階層の広さ(10〜50)。領域研究の横拡張で階層ごとに増やせる
    public List<DungeonFeatureManager.FeatureRecord> features = new List<DungeonFeatureManager.FeatureRecord>();
}
