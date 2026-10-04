# DP の収支

周の数：10（1周あたりの平均で示す）

## 蛇口と排水口

| 区分 | 呼び出し元 | 1周あたり | 割合 |
|---|---|---|---|
| 出 | 配下の召喚<br><span style="color:#888">MinionRoster.TrySummon</span> | 68,653 | 57% |
| 入り | 撃破の報酬<br><span style="color:#888">AdventurerAI.TakeDamage</span> | 61,371 | 43% |
| 出 | 号令<br><span style="color:#888">CommandSystem.TryUse</span> | 50,570 | 42% |
| 入り | 逃げた冒険者の清算<br><span style="color:#888">AdventurerAI.GrantReturnReward</span> | 32,218 | 23% |
| 入り | 因縁の相手<br><span style="color:#888">Nemesis.OnSlain</span> | 24,739 | 17% |
| 入り | 時代の偉業<br><span style="color:#888">EraSystem.TickTurn</span> | 14,659 | 10% |
| 入り | 感情の刈り取り<br><span style="color:#888">AdventurerAI.ReapEmotion</span> | 6,019 | 4% |
| 入り | 地上の産出<br><span style="color:#888">SurfaceMap.CollectYields</span> | 2,123 | 1% |
| 入り | 施設の産出<br><span style="color:#888">DistrictCatalog.Collect</span> | 1,617 | 1% |
| 出 | 罠を置く<br><span style="color:#888">DungeonFeatureManager.TryPlaceTrap</span> | 1,050 | 1% |
| 出 | 施設を置く<br><span style="color:#888">DungeonFeatureManager.TryPlaceFeature</span> | 550 | 0% |
| 出 | 環境を置く<br><span style="color:#888">DungeonFeatureManager.TryPlaceHabitat</span> | 484 | 0% |
| 入り | IncidentSystem.Apply<br><span style="color:#888">IncidentSystem.Apply</span> | 360 | 0% |
| 出 | ProductionSystem.TryPurchase<br><span style="color:#888">ProductionSystem.TryPurchase</span> | 10 | 0% |

## ターンごと（全周の平均）

| T | 入り | 出 | 残高（中央値） | 貯まり具合 R |
|---|---|---|---|---|
| 1 | 628 | 625 | 244 | 0.4 |
| 2 | 1,335 | 1,295 | 295 | 0.4 |
| 3 | 1,579 | 1,448 | 340 | 0.4 |
| 4 | 1,775 | 1,512 | 862 | 0.9 |
| 5 | 2,166 | 2,256 | 428 | 0.4 |
| 6 | 2,428 | 2,414 | 519 | 0.4 |
| 7 | 2,445 | 2,308 | 751 | 0.5 |
| 8 | 1,850 | 2,139 | 345 | 0.2 |
| 9 | 3,353 | 2,850 | 794 | 0.5 |
| 10 | 2,874 | 2,922 | 852 | 0.5 |
| 11 | 2,858 | 2,754 | 881 | 0.4 |
| 12 | 3,222 | 3,331 | 715 | 0.3 |
| 13 | 3,772 | 3,141 | 1,642 | 0.7 |
| 14 | 4,702 | 4,127 | 1,867 | 0.7 |
| 15 | 5,273 | 4,741 | 2,388 | 0.8 |
| 16 | 5,687 | 5,389 | 3,085 | 0.9 |
| 17 | 8,225 | 6,395 | 5,013 | 1.3 |
| 18 | 6,746 | 7,162 | 4,078 | 0.9 |
| 19 | 7,972 | 7,520 | 6,169 | 1.3 |
| 20 | 10,367 | 7,440 | 6,794 | 1.2 |
| 21 | 7,676 | 9,368 | 4,429 | 0.8 |
| 22 | 10,006 | 7,964 | 8,026 | 1.3 |
| 23 | 13,759 | 9,644 | 12,188 | 1.7 |
| 24 | 9,433 | 10,569 | 7,177 | 0.9 |
| 25 | 18,616 | 11,208 | 18,057 | 2.0 |
| 26 | 20,479 | 12,272 | 26,257 | 2.5 |
| 27 | 21,104 | 12,608 | 32,109 | 2.7 |
| 28 | 16,398 | 12,608 | 45,843 | 3.7 |
| 29 | 17,095 | 12,920 | 39,488 | 3.0 |
