# Research & Design Decisions

## Summary
- **Feature**: `timeline-prefab-builder`
- **Discovery Scope**: New Feature(グリーンフィールド / embedded UPM パッケージ新規作成)
- **Key Findings**:
  - Timeline 1.8 系の公式 API(`TimelineAsset.CreateTrack<T>` / `TrackAsset.CreateClip<T>` / `AudioPlayableAsset.clip { get; set; }` / `AnimationPlayableAsset.clip`)だけでトラック・クリップのプログラム構築が完結する。追加パッケージは不要
  - com.unity.timeline 1.8.13 は Unity 6 系(6000.x)向けにリリースされた検証済みバージョンであり、プロジェクトの manifest.json に既に導入済み
  - バッチ実行の exit code 制御は `EditorApplication.Exit(int)` の明示呼び出しが確実。`-quit` に依存せず、例外は握りつぶさずすべて失敗系 exit code に変換する構成が CI 実績のあるパターン
  - リポジトリにはまだ実装コードが一切なく(Assets 配下 .cs ゼロ、Packages はマニフェストのみ)、既存パターンへの追従制約はない。steering ディレクトリも未整備

## Research Log

### Timeline のプログラム構築 API(com.unity.timeline 1.8)
- **Context**: 要件 3(TimelineAsset の構築)を実現する API の確認
- **Sources Consulted**:
  - [Class TimelineAsset | Timeline 1.8](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEngine.Timeline.TimelineAsset.html)
  - [Class TrackAsset | Timeline 1.8](https://docs.unity.cn/Packages/com.unity.timeline@1.8/api/UnityEngine.Timeline.TrackAsset.html)
  - [Class AudioPlayableAsset | Timeline 1.8](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEngine.Timeline.AudioPlayableAsset.html)
  - [Programmatically Creating a Timeline - Amorphic Space](https://amorphic.space/journal/unity/programmatic-timeline/)
- **Findings**:
  - `TimelineAsset.CreateTrack<T>(TrackAsset parent, string name)` でトラック生成(AudioTrack / AnimationTrack とも対応)
  - `TrackAsset.CreateClip<T>()` で PlayableAsset 付き `TimelineClip` を生成。`TimelineClip.start` / `clipIn` / `duration` / `displayName` は public に設定可能
  - `AudioPlayableAsset.clip` は public getter/setter を持つことを公式 API リファレンスで確認。`AnimationPlayableAsset.clip` も同様に設定可能
  - TimelineAsset はサブアセット(トラック・PlayableAsset)を内包するため、`AssetDatabase.CreateAsset` 後に `AssetDatabase.SaveAssets` で永続化する必要がある
- **Implications**: トラック種別ごとの差分は「トラック生成」「クリップへのリソース割り当て」の 2 点に閉じるため、Strategy(ITrackBuilder)で抽象化できる。TimelineAsset の拡張子は `.playable` を使用する

### com.unity.timeline 1.8.13 と Unity 6000.0.36f1 の互換性
- **Context**: 要件 8.3 / 8.4(依存宣言と動作環境)
- **Sources Consulted**:
  - [Unity Manual: Timeline (Unity 6000)](https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.timeline.html)
  - [Timeline package version 1.8.x and 1.7.x - Unity Discussions](https://discussions.unity.com/t/timeline-package-version-1-8-x-and-1-7-x/927104)
  - [Changelog | Timeline 1.8](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/changelog/CHANGELOG.html)
- **Findings**:
  - 1.8.13 は Unity 6 系向けの検証済みリリース。破壊的 API 変更なし(1.8 系はバグフィックス中心)
  - 本プロジェクトの `UnityTimelineBuilder/Packages/manifest.json` に `com.unity.timeline: 1.8.13` が既に宣言済み
- **Implications**: package.json の依存は `"com.unity.timeline": "1.8.13"` で確定。asmdef は `Unity.Timeline` アセンブリを参照する

### バッチモード実行と exit code 制御
- **Context**: 要件 6(CLI バッチ実行)の exit code / ログ設計
- **Sources Consulted**:
  - [Unity Manual: Unity Editor command line arguments](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)
  - [Headless automation in Unity - partiallydisassembled](https://partiallydisassembled.net/posts/unity-headless.html)
  - [Unity batchmode does no longer return exit code - Unity Discussions](https://discussions.unity.com/t/unity-batchmode-does-no-longer-return-exit-code-that-could-be-captured-by-python/1698339)
- **Findings**:
  - `-executeMethod` の対象は public static メソッドで、Editor アセンブリに置く必要がある
  - 失敗の伝達は「例外送出(Unity が exit code 1 で終了)」または「`EditorApplication.Exit(非0)`」。exit code を細かく制御するには後者が確実
  - `-quit` と `EditorApplication.Exit` の併用は不要(Exit がプロセスを終了させる)。CLI エントリポイント内で全例外を捕捉して Exit に集約するのが CI 実績のあるパターン
  - コマンドライン引数は `System.Environment.GetCommandLineArgs()` で取得する(`-executeMethod` 実行ではメソッド引数は渡らない)
- **Implications**: CLI 層は「引数解析 → API 呼び出し → 結果を exit code に写像」だけを担い、テスト容易性のため Exit 呼び出しと処理本体(int を返す)を分離する

### FBX 内包 AnimationClip の解決
- **Context**: 要件 2.4 / 2.5(fbx 内包 AnimationClip の選択)
- **Sources Consulted**: Unity 公式 Scripting API(AssetDatabase.LoadAllAssetsAtPath / LoadAssetRepresentationsAtPath)
- **Findings**:
  - fbx のサブアセットは `AssetDatabase.LoadAllAssetsAtPath(path)` から `OfType<AnimationClip>()` で列挙できる
  - fbx には Editor 生成のプレビュー用クリップ(名前が `__preview__` で始まる)が含まれるため除外が必要
  - 複数クリップ内包時は名前一致で選択する。単一クリップならそのまま採用できる
- **Implications**: AnimationClip リゾルバは「.anim 単体」「fbx 内包(単一/複数)」の 3 分岐を持ち、複数時の選択キーは構築情報の Clip 名列を用いる(下記 Design Decisions 参照)

### 外部リソースのコピーとインポート
- **Context**: 要件 2.2(プロジェクト外リソースの取り込み)
- **Findings**(Unity 公式 API 仕様に基づく):
  - `File.Copy` で `Assets/` 配下へ複製後、`AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport)` で同期インポートすれば直後に `AssetDatabase.LoadAssetAtPath` で参照できる
  - wav / mp3 は Unity 6 の標準 AudioImporter がサポート。追加設定なしで AudioClip として解決可能
- **Implications**: インポート先ディレクトリは既定 `Assets/UnityTimelineBuilder/Imported/` とし、API 引数で上書き可能にする

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| パイプライン + Strategy レジストリ(採用) | Parse → Resolve → Build Timeline → Build Prefab の直列パイプライン。トラック種別・リソース種別を Strategy + Registry で抽象化 | 要件 2.8 / 3.6 の拡張性を最小コストで満たす。フェーズ境界 = エラー報告境界で診断しやすい | レジストリの静的状態管理(テスト時のリセット)に注意 | 小規模 Editor ツールに過剰でない範囲の抽象化 |
| 単一ビルダークラス(手続き型) | 1 クラスに全処理を実装 | 最短で実装可能 | トラック/リソース種別追加のたびに既存コードを修正(2.8 / 3.6 違反)。テスト分割不能 | 却下 |
| フル DI + Hexagonal | ポート/アダプタで全境界を抽象化 | 最大の柔軟性 | Editor 静的コンテキストと相性が悪く、public static API 要件(5.1)に対して過剰 | 却下 |

## Design Decisions

### Decision: CSV/TSV パーサーを自前実装(RFC 4180 準拠)とする
- **Context**: 要件 1.2(Google スプレッドシートエクスポートのクォート・改行・エスケープ対応)
- **Alternatives Considered**:
  1. 外部 CSV ライブラリ(CsvHelper 等)導入 — NuGet/UPM 依存が増え、UPM 配布が複雑化
  2. `string.Split` ベースの簡易実装 — フィールド内カンマ・改行で破綻
- **Selected Approach**: RFC 4180 準拠のステートマシン型リーダーを Parsing 層に自前実装。区切り文字(`,` / `\t`)はコンストラクタ引数で切り替え、拡張子で選択する
- **Rationale**: Google スプレッドシートのエクスポートは RFC 4180 準拠。仕様が小さく確定しており自前実装が依存ゼロで最も安全
- **Trade-offs**: パーサーの単体テストを厚めに書く必要がある(クォート・改行・BOM・CRLF)
- **Follow-up**: Google スプレッドシート実エクスポートファイルをテストフィクスチャとして採取する

### Decision: fbx 内複数 AnimationClip の選択キーは Clip 名列を使う
- **Context**: 要件 2.5「構築情報で指定された名前に一致する AnimationClip を選択」
- **Alternatives Considered**:
  1. resourcePath に `#サブアセット名` サフィックスを導入 — 列仕様が複雑化し、テンプレートの説明コストが増える
  2. Clip 名列をサブアセット選択キーに流用(採用)
- **Selected Approach**: fbx に複数の AnimationClip が内包される場合、構築情報の Clip 名列と一致する名前のサブアセットを選択する。単一内包時は名前照合なしで採用。`.anim` 単体はパス解決のみ
- **Rationale**: 要件の文言「構築情報で指定された名前」に最も素直で、列定義を増やさない。Timeline 上の表示名とアニメーション名が一致する運用は制作フローとしても自然
- **Trade-offs**: fbx 内クリップ名と異なる表示名を付けたい場合に対応できない(将来、専用列の追加で拡張可能)
- **Follow-up**: 複数内包 fbx で Clip 名不一致時のエラーメッセージに「fbx 内の候補クリップ名一覧」を含めると診断性が上がる

### Decision: 2 フェーズコミット型パイプライン(検証完了までアセットを書き込まない)
- **Context**: 要件 1.5 / 1.6 / 2.6 / 2.7 の「エラー時に構築処理を中断」と、中断時の中途半端なアセット残留の回避
- **Alternatives Considered**:
  1. 行単位に逐次構築(エラー行で中断) — 途中まで構築されたアセットが残る
  2. 全行のパース+リソース解決を先に完了させ、成功後にアセット生成(採用)
- **Selected Approach**: Phase A(パース → 全行バリデーション → 全リソース解決)がすべて成功した場合のみ Phase B(TimelineAsset 生成 → Prefab 生成 → 保存)に進む。Phase A は検出可能な全エラーを収集してからまとめて報告する
- **Rationale**: 失敗時にプロジェクトを汚さない。CI 実行で「1 回の実行で全エラーが判る」ことは修正サイクル短縮に直結する
- **Trade-offs**: 外部リソースのコピー・インポートは Phase A で発生するため、後続エラー時にインポート済みアセットは残る(ログで明示する)。Phase B 内の保存失敗は残留し得るが発生頻度は低い
- **Follow-up**: 実装時に AssetDatabase.StartAssetEditing/StopAssetEditing による一括インポートの最適化を検討(必須ではない)

### Decision: exit code は 0 / 1 / 2 の 3 値とする
- **Context**: 要件 6.3 / 6.4(exit code による結果判別)
- **Selected Approach**: 0 = 成功、1 = 構築失敗(パース・解決・生成エラー)、2 = CLI 引数不正(必須引数欠落等)。CLI 内で全例外を捕捉し `EditorApplication.Exit` に集約
- **Rationale**: CI 側で「データ不備(1)」と「呼び出し方の誤り(2)」を区別でき、原因切り分けが速い
- **Trade-offs**: これ以上の細分化(エラー種別ごとの code)はログで代替し、code 体系の肥大化を避ける

### Decision: CSV テンプレートと列定義ドキュメントは `Documentation~/` に同梱する
- **Context**: 要件 7(テンプレートとドキュメントの同梱)
- **Alternatives Considered**:
  1. `Samples~/` 経由の Import — Unity Editor 上での取り込み手順が増える。ドキュメント閲覧目的には過剰
  2. `Documentation~/` に直接配置(採用)
- **Selected Approach**: `Documentation~/timeline-template.csv` と `Documentation~/column-definitions.md` を配置。`Documentation~` は AssetDatabase のインポート対象外のため .meta 不要で、Google スプレッドシートへは File > Import でそのまま取り込める
- **Rationale**: UPM 規約準拠かつ Unity プロジェクトを汚さない。パッケージ利用者はエクスプローラ/リポジトリから直接参照できる
- **Trade-offs**: Unity Editor の Package Manager UI 上ではサンプルとして表示されない(README からリンクして補う)

## Risks & Mitigations
- **RFC 4180 エッジケース(BOM、CRLF 混在、末尾空行)の取りこぼし** — Google スプレッドシート実エクスポートをフィクスチャ化した単体テストで担保
- **バッチモードでの同期インポート失敗(mp3 デコーダ等の環境差)** — `ImportAsset` 後のロード結果を必ず null チェックし、失敗時は ResourceNotFound / ImportFailed エラーとして行番号付きで報告
- **レジストリの静的状態がテスト間で汚染される** — レジストリにリセット手段(テスト用 internal API)を設け、テストの SetUp で初期状態へ戻す
- **AnimationClip を Legacy 設定(`legacy: true`)の .anim で渡された場合に Timeline で再生不能** — 解決時に `AnimationClip.legacy` を検査し、警告ログを出す(構築自体は継続)
- **Exit code が CI 側で捕捉できない Unity バージョン固有の不具合報告あり(6000.2 系での報告)** — 本プロジェクトは 6000.0.36f1 固定。ログにも成功/失敗マーカーを出力し、exit code 以外でも判定可能にする

## References
- [Timeline 1.8 API Reference](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEngine.Timeline.TimelineAsset.html) — トラック/クリップ生成 API
- [AudioPlayableAsset (Timeline 1.8)](https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEngine.Timeline.AudioPlayableAsset.html) — clip setter の確認
- [Unity Editor command line arguments](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html) — -batchmode / -executeMethod / exit code 仕様
- [Headless automation in Unity](https://partiallydisassembled.net/posts/unity-headless.html) — CLI エントリポイント設計の実践パターン
- [RFC 4180](https://www.rfc-editor.org/rfc/rfc4180) — CSV フォーマット仕様
- [Unity Manual: Embedded packages](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-embed.html) — embedded package 配置規約
