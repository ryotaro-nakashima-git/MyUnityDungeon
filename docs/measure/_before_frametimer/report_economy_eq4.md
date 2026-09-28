# DP の収支

周の数：10（1周あたりの平均で示す）

## 蛇口と排水口

| 区分 | 呼び出し元 | 1周あたり | 割合 |
|---|---|---|---|
| 出 | 配下の召喚<br><span style="color:#888">MinionRoster.TrySummon</span> | 48,066 | 52% |
| 入り | 撃破の報酬<br><span style="color:#888">AdventurerAI.TakeDamage</span> | 42,929 | 42% |
| 出 | 号令<br><span style="color:#888">CommandSystem.TryUse</span> | 42,205 | 46% |
| 入り | 逃げた冒険者の清算<br><span style="color:#888">AdventurerAI.GrantReturnReward</span> | 23,440 | 23% |
| 入り | 因縁の相手<br><span style="color:#888">Nemesis.OnSlain</span> | 18,321 | 18% |
| 入り | 時代の偉業<br><span style="color:#888">EraSystem.TickTurn</span> | 9,412 | 9% |
| 入り | 感情の刈り取り<br><span style="color:#888">AdventurerAI.ReapEmotion</span> | 4,973 | 5% |
| 入り | 地上の産出<br><span style="color:#888">SurfaceMap.CollectYields</span> | 1,570 | 2% |
| 入り | 施設の産出<br><span style="color:#888">DistrictCatalog.Collect</span> | 1,187 | 1% |
| 出 | 罠を置く<br><span style="color:#888">DungeonFeatureManager.TryPlaceTrap</span> | 1,050 | 1% |
| 出 | 施設を置く<br><span style="color:#888">DungeonFeatureManager.TryPlaceFeature</span> | 625 | 1% |
| 出 | 環境を置く<br><span style="color:#888">DungeonFeatureManager.TryPlaceHabitat</span> | 558 | 1% |
| 入り | IncidentSystem.Apply<br><span style="color:#888">IncidentSystem.Apply</span> | 480 | 0% |
| 出 | IncidentSystem.Apply<br><span style="color:#888">IncidentSystem.Apply</span> | 80 | 0% |
| 出 | ProductionSystem.TryPurchase<br><span style="color:#888">ProductionSystem.TryPurchase</span> | 11 | 0% |

## ターンごと（全周の平均）

| T | 入り | 出 | 残高（中央値） | 貯まり具合 R |
|---|---|---|---|---|
| 1 | 588 | 625 | 162 | 0.3 |
| 2 | 1,246 | 1,240 | 202 | 0.3 |
| 3 | 1,271 | 1,116 | 331 | 0.4 |
| 4 | 1,807 | 1,606 | 604 | 0.7 |
| 5 | 2,150 | 2,108 | 457 | 0.4 |
| 6 | 2,152 | 2,313 | 453 | 0.4 |
| 7 | 2,607 | 2,495 | 511 | 0.4 |
| 8 | 2,538 | 2,368 | 846 | 0.6 |
| 9 | 2,682 | 3,038 | 324 | 0.2 |
| 10 | 2,532 | 2,283 | 407 | 0.2 |
| 11 | 2,861 | 2,516 | 1,116 | 0.6 |
| 12 | 3,002 | 2,829 | 886 | 0.4 |
| 13 | 3,295 | 3,282 | 856 | 0.4 |
| 14 | 4,402 | 3,628 | 1,803 | 0.7 |
| 15 | 4,241 | 4,431 | 1,479 | 0.5 |
| 16 | 5,883 | 4,624 | 3,845 | 1.2 |
| 17 | 7,876 | 6,380 | 4,213 | 1.1 |
| 18 | 6,648 | 6,649 | 4,466 | 1.1 |
| 19 | 7,973 | 7,118 | 5,130 | 1.1 |
| 20 | 8,280 | 7,719 | 5,413 | 1.1 |
| 21 | 9,440 | 8,180 | 7,035 | 1.2 |
| 22 | 10,073 | 8,157 | 7,536 | 1.2 |
| 23 | 10,777 | 9,775 | 7,885 | 1.2 |
| 24 | 10,383 | 10,438 | 8,318 | 1.1 |
| 25 | 15,027 | 11,936 | 13,838 | 1.7 |
| 26 | 15,957 | 12,272 | 15,289 | 1.6 |
