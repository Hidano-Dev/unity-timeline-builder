# Requirements Document

## Project Description (Input)
1 つの CSV/TSV シートに複数の Timeline を記述し、1 回のビルドで複数の TimelineAsset / Prefab / Scene を同時生成できるようにする。現状は 1 シート = 1 TimelineAsset = 1 Prefab = 1 Scene の制約があり（.kiro/specs/timeline-scene-builder/research.md の設計判断）、複数 Timeline を作るには CSV を分割して複数回ビルドする必要がある。この制約を緩和し、CSV 内で Timeline 単位のグルーピング（例: timelineName カラム追加、または Scene 行ごとのセクション区切り）を導入する。影響範囲: BuildSheetParser の列定義・行検証（Scene 行 1 行制約の見直し）、TimelineBuilder のビルドパイプライン（複数アセット出力）、TimelineAssetFactory / PrefabFactory / SceneFactory、BuildRequest / BuildResult モデル（複数出力パス対応）、TimelineBuilderCli、column-definitions.md などのドキュメント。後方互換性: 既存の単一 Timeline CSV（timelineName 無し）はそのまま動作すること。

## Introduction
本仕様は、UPM パッケージ「Unity Timeline Builder」(`com.hidano.unity-timeline-builder`)の拡張として、1 つの構築情報 CSV/TSV に複数の Timeline の構築内容を記述し、1 回の構築処理で複数の TimelineAsset / Prefab / Scene を同時生成できるようにする機能の要件を定義する。既存仕様(timeline-prefab-builder / timeline-scene-builder)では 1 シート = 1 TimelineAsset = 1 Prefab = 1 Scene の制約があり、複数の Timeline を作るにはシートを分割して複数回構築する必要があった。本機能はシート内に Timeline 単位のグルーピングを導入してこの制約を緩和し、既存の単一 Timeline フォーマットとの後方互換性を維持する。

## Boundary Context

- **In scope**:
  - 1 つの構築情報 CSV/TSV 内で複数の Timeline を区別して記述できるグルーピング表記の定義とパース(Scene 行 1 行制約の Timeline グループ単位への見直しを含む)
  - 1 回の構築処理での複数 TimelineAsset / Prefab / Scene の同時生成
  - 構築結果(複数の出力アセットパス)の報告形式の拡張と、公開 API・CLI バッチ実行での複数出力対応
  - CSV テンプレート・列定義ドキュメントの更新
  - 既存の単一 Timeline フォーマット(グルーピング表記なし)との後方互換性の維持
- **Out of scope**:
  - 複数の構築情報ファイルの一括入力(1 回の構築処理の入力は従来どおり 1 ファイル)
  - Timeline グループ間の相互参照(あるグループで生成した TimelineAsset を別グループの Scene へ暗黙参照する等。既存 TimelineAsset の `Assets/` パスによる明示参照は既存仕様どおり)
  - 単一 Timeline 分の構築仕様自体の変更(パース規約、リソース解決、TimelineAsset / Prefab / Scene 生成、バインディング規約)
  - 既存 Scene への追記・マージ、AnimationTrack 以外のバインディング(いずれも既存仕様の Out of scope を踏襲)
- **Adjacent expectations**:
  - 各 Timeline グループ単体の構築挙動(パース規約、リソース解決、TimelineAsset / Prefab 生成、Scene 生成・バインディング)は既存仕様(timeline-prefab-builder / timeline-scene-builder)に従い、変更しない
  - Google スプレッドシートの File > Download エクスポート(1 ファイル 1 シート)互換、RFC 4180 準拠のパース規約は維持される

## Open Questions and Decisions (Dig)

| ID | トピック | 決定 | 根拠 | リスク |
|----|----------|------|------|--------|
| D-1 | グルーピング表記方式 | `timeline` カラムを 8 列目として追加（ヘッダあり時は列順自由）。各行の `timeline` 値が帰属先 Timeline 名 | 列順・行順に依存せず、Google スプレッドシートでのフィルタ・並べ替えに壊れない。timeline カラム無し = 従来の単一グループとして自然に後方互換 | 中: 全行への記入が冗長（空欄時の扱いは D-4 で別途決定） |
| D-2 | 出力アセット名 | `timeline` カラムの Timeline 名をそのまま各グループのアセット名にする（例: `TitleTimeline.playable` / `TitleTimeline.prefab`） | CSV を見れば出力が分かる直感性。プレフィックス方式は参照しづらい | 低: AssetName 引数との競合規則が別途必要（D-5 予定） |
| D-3 | 行の並び順 | 同一 Timeline の行の連続は不要（インターリーブ許可）。グループの出力順はシート内での初出順で安定させる | スプレッドシートでの並べ替え・フィルタ運用に強い。カラム型（D-1）と整合 | 低: タイポによる意図しない別グループ化は検出できない（Timeline 名の妥当性検証でカバー） |
| D-4 | timeline セル空欄の扱い | timeline カラムが存在する場合、全行で値必須。空欄は行番号付きエラー | fill-down 継承は行順に意味を持たせ D-3 と矛盾。既定グループ帰属は書き忘れとの区別がつかない | 低: 記入は冗長だがスプレッドシート側のフィルダウンで対処可能 |
| D-5 | AssetName 引数との競合 | timeline カラムを含むシートに BuildRequest.AssetName / CLI -assetName が指定されたらエラーで中断 | Timeline 名が常にアセット名（D-2）という単純で一貫した規則を守り、暗黙の無視による意図しない出力を防ぐ | 低: 既存 CI が -assetName を渡したまま新フォーマットに切り替えると明示的に失敗する（挙動が黙って変わるより安全） |
| D-6 | ヘッダー無し CSV | timeline カラムはヘッダー行必須。ヘッダー無しは従来どおり既定 7 列順の単一 Timeline として解釈 | 既定列順の拡張は既存シートの誤解釈リスクを生む。ヘッダー無し運用は従来機能として完全温存 | 低 |
| D-7 | 生成フェーズ中の失敗時の部分生成物 | fail-fast で中断し、生成済み成果物はディスクに残す。どこまで成功したかをログで明示 | 再実行は同名上書き（既存仕様）なので冪等に復旧可能。ロールバックは上書き済みアセットを戻せず保証が中途半端 | 低: 失敗時にディスク上へ不完全な成果物セットが残る（ログで判別可能） |
| D-8 | 出力パス・Scene 名の衝突判定 | 大文字小文字を区別せずに衝突と判定する。ただし衝突してもエラーで中断せず、後出のグループの出力名に `(1)` などの連番サフィックスを付加して処理を継続し、警告をログに出力する | Windows/macOS のパス非区別による黙った上書き事故を防ぎつつ、ビルド全体は止めない | 中: サフィックス付き出力は CSV の記述と名前が一致しなくなるため、警告ログと BuildResult の返却パスで追跡できることが前提 |

## Requirements

### Requirement 1: 構築情報フォーマットの複数 Timeline 対応
**Objective:** As a コンテンツ制作者, I want 1 つの構築情報 CSV/TSV に複数の Timeline の構築内容をまとめて記述したい, so that Timeline ごとにシートを分割・管理する手間なく、1 つのスプレッドシートで演出全体を管理できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 1 つの構築情報 CSV/TSV 内で複数の Timeline を区別して記述できるグルーピング表記として `timeline` カラム(Timeline 名)を定義する (see D-1)
2. The Unity Timeline Builder shall 構築情報ファイル内の各行(Track・クリップ行および Scene 構築行)がどの Timeline に帰属するかを各行の `timeline` カラムの値から一意に決定し、同一 Timeline の行がシート内で連続していなくても帰属を決定する (see D-1, D-3)
3. The Unity Timeline Builder shall 同一の Timeline を示す行を同一の Timeline グループとして集約する
4. The Unity Timeline Builder shall 拡張後のフォーマットを Google スプレッドシートの File > Download でエクスポートされた CSV/TSV として既存のパース規約どおりにパースする
5. If `timeline` カラムが存在するシートで timeline セルが空欄の行があるとき、または Timeline の識別内容が不正であるとき, then the Unity Timeline Builder shall 行番号と原因を特定できるエラーを報告し、構築処理を中断する (see D-4)
6. The Unity Timeline Builder shall `timeline` カラムをヘッダー行がある場合にのみ認識し、ヘッダー無しシートは従来どおり既定 7 列順の単一 Timeline として解釈する (see D-6)

### Requirement 2: 複数 TimelineAsset / Prefab の同時生成
**Objective:** As a コンテンツ制作者, I want 1 回の構築処理でシートに記述したすべての Timeline の TimelineAsset と Prefab を生成したい, so that CSV を分割して構築処理を複数回実行する手間をなくせる

#### Acceptance Criteria
1. When 複数の Timeline グループを含む構築情報ファイルで構築処理が実行されたとき, the Unity Timeline Builder shall Timeline グループごとに TimelineAsset を生成し、1 回の構築処理ですべての TimelineAsset を出力する
2. The Unity Timeline Builder shall Timeline グループごとに、対応する PlayableDirector 付き Prefab を既存仕様と同じ規約で生成する
3. The Unity Timeline Builder shall 各 Timeline グループの出力アセット名を、`timeline` カラムに記述された Timeline 名をそのまま用いて決定する (see D-2)
4. The Unity Timeline Builder shall Track・クリップ行の同一 Track への集約(同じ種別・同じ Track 名の集約)を Timeline グループ内に限定し、グループを跨いで集約しない
5. If 複数の Timeline グループの出力アセットパスが互いに衝突するとき(大文字小文字を区別せず判定), then the Unity Timeline Builder shall 後出のグループの出力アセット名に連番サフィックス(例: `(1)`)を付加して衝突を回避し、リネームした出力を特定できる警告をログに出力して処理を継続する (see D-8)
6. If 出力先パスに既存のアセットが存在するとき, then the Unity Timeline Builder shall 既存仕様どおり上書きし、上書きした旨をログに出力する

### Requirement 3: Timeline グループごとの Scene 構築
**Objective:** As a コンテンツ制作者, I want Timeline ごとに Scene 構築情報を記述し、1 回の構築処理で複数のバインド済み Scene を生成したい, so that 複数演出分の Scene 作成・バインド設定を 1 回の実行で完了できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall Scene 行を Timeline グループごとに高々 1 行受け付ける(従来の「1 シートにつき Scene 行 1 行まで」の制約を Timeline グループ単位に緩和する)
2. When ある Timeline グループに Scene 構築情報が含まれるとき, the Unity Timeline Builder shall そのグループの Scene ファイル(.unity)を既存の Scene 構築仕様どおりに生成する
3. The Unity Timeline Builder shall Scene 行の TimelineAsset 暗黙参照(参照先の未指定)を、同一 Timeline グループで生成した TimelineAsset に解決する
4. The Unity Timeline Builder shall ScenePrefab / SceneBind 行を、帰属する Timeline グループの Scene に対して適用する
5. If 同一 Timeline グループ内に Scene 行が 2 行以上存在するとき, then the Unity Timeline Builder shall 行番号を特定できるエラーを報告し、構築処理を中断する
6. If Scene 行を持たない Timeline グループに ScenePrefab または SceneBind 行が存在するとき, then the Unity Timeline Builder shall 行番号を特定できるエラーを報告し、構築処理を中断する
7. If 複数の Timeline グループで同一の Scene 出力先が指定され衝突するとき(大文字小文字を区別せず判定), then the Unity Timeline Builder shall 後出のグループの Scene 出力名に連番サフィックス(例: `(1)`)を付加して衝突を回避し、リネームした出力を特定できる警告をログに出力して処理を継続する (see D-8)

### Requirement 4: 構築結果の報告と整合性
**Objective:** As a ツール開発者, I want 複数 Timeline の構築結果と失敗原因を Timeline ごとに判別したい, so that 構築の成否確認と失敗時の原因特定を自動化できる

#### Acceptance Criteria
1. When 構築処理が完了したとき, the Unity Timeline Builder shall 生成したすべての TimelineAsset / Prefab / Scene のアセットパスを、どの Timeline の成果物かを判別できる形式で返却する
2. When 構築情報の検証でいずれかの Timeline グループにエラーが検出されたとき, the Unity Timeline Builder shall シート全体の検証を完了してから検出したすべてのエラーをまとめて報告し、アセットを生成せずに構築処理を中断する
3. If アセット生成中にエラーが発生したとき, then the Unity Timeline Builder shall 失敗した時点で構築処理を中断(fail-fast)し、失敗した Timeline グループと原因、およびどのグループまで生成が成功したかを特定できるエラーを報告して構築処理を失敗として通知する(生成済みの成果物はディスクに残す) (see D-7)
4. The Unity Timeline Builder shall エラー報告に行番号および対象 Timeline を特定できる情報を含める

### Requirement 5: 公開 API と CLI バッチ実行の拡張
**Objective:** As a ビルドパイプライン管理者, I want 公開 API と CLI から複数 Timeline の構築を 1 回で実行し、すべての出力を取得したい, so that CI や外部スクリプトから複数演出分の成果物を 1 回の Unity 起動で生成できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 公開 API の構築結果として、複数 Timeline 分の出力アセットパスを呼び出し元が判別できる形式で返却する
2. When CLI エントリポイントによる構築処理が正常に完了したとき, the Unity Timeline Builder shall 生成したすべての成果物のアセットパスをログに出力し、exit code 0 で Unity Editor プロセスを終了させる
3. If CLI エントリポイントによる構築処理が失敗したとき, then the Unity Timeline Builder shall 失敗の原因(対象ファイル、行番号、対象 Timeline、エラー内容等)を特定できるメッセージをログに出力し、0 以外の exit code で Unity Editor プロセスを終了させる
4. The Unity Timeline Builder shall 既存の公開 API および CLI の呼び出し互換性(シグネチャ・引数仕様)を維持する
5. If `timeline` カラムを含むシートに対して AssetName(API 引数 / CLI の -assetName)が指定されたとき, then the Unity Timeline Builder shall 競合を特定できるエラーを報告し、構築処理を中断する (see D-5)

### Requirement 6: 後方互換性の維持
**Objective:** As a 既存利用者, I want グルーピング表記を含まない既存の構築情報ファイルをそのまま使い続けたい, so that 既存のシート・スクリプト・CI 設定を変更せずに本機能へ移行できる

#### Acceptance Criteria
1. When グルーピング表記を含まない既存フォーマットの構築情報ファイルが入力されたとき, the Unity Timeline Builder shall 従来どおり単一の TimelineAsset / Prefab、および Scene 行がある場合は単一の Scene を、従来と同一の命名・出力先・挙動で生成する
2. The Unity Timeline Builder shall 既存のパース規約(RFC 4180、ヘッダー行の有無の判定、ヘッダーレス時の固定列順フォールバック)を維持する
3. The Unity Timeline Builder shall 既存フォーマット入力時の構築結果の返却内容(成功・失敗、失敗理由、出力アセットパス)を従来の呼び出し元がそのまま解釈できる形式で維持する

### Requirement 7: テンプレートとドキュメントの更新
**Objective:** As a コンテンツ制作者, I want 複数 Timeline の記述方法をテンプレートとドキュメントで参照したい, so that 正しいデータ構造で複数 Timeline の構築情報を作成できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 同梱の CSV テンプレートに複数 Timeline を記述した記入例を追加する
2. The Unity Timeline Builder shall 列定義ドキュメントにグルーピング表記の名称・意味・データ型・必須/任意・記入例、および行の帰属規則を追加する
3. The Unity Timeline Builder shall CSV テンプレートおよび列定義ドキュメントの内容を拡張後のパーサーの仕様と一致させる

## Dig Summary

- **ラウンド数**: 3 / **質問数**: 8 / **決定数**: 8（D-1〜D-8）

### 主要な発見
1. **カラム型グルーピング（D-1）と行順自由（D-3）の組み合わせ**により、Google スプレッドシートでの並べ替え・フィルタ運用に耐えるフォーマットになる。この整合性を守るため、空欄セルの fill-down 継承は採用しない（D-4）。
2. **「timeline カラムがあれば Timeline 名が常にアセット名」という単一規則**（D-2 + D-5）により、AssetName 引数との競合は暗黙無視ではなく明示エラーとし、意図しない出力の黙った発生を防ぐ。
3. **衝突は大文字小文字非区別で検出するが中断しない**（D-8）: 連番サフィックスで自動リネームして継続する。エラー中断を採らなかった点は当初の AC から方針変更されており、警告ログと BuildResult での追跡可能性が前提条件となる。

### 決定一覧
「Open Questions and Decisions (Dig)」の表を参照（D-1〜D-8）。

### 残存リスク（設計フェーズへの申し送り）
- **Timeline 名の使用可能文字の検証仕様**: ファイル名に使えない文字（`/`, `:`, `*` 等）を含む Timeline 名の扱い（エラーかサニタイズか）は設計で決定する。
- **BuildResult の後方互換形式**: 既存の単一値プロパティ（TimelineAssetPath / PrefabPath / ScenePath）と複数出力コレクションの共存方法（Req 4.1 / Req 6.3 / Req 5.4 の同時充足）は設計で具体化する。
- **サフィックス付加時の Scene 内暗黙参照**: リネームされたグループの Scene 行暗黙参照（Req 3.3）がリネーム後の TimelineAsset を正しく指すことを設計・テストで保証する必要がある。

### validate-gap からの申し送り（設計フェーズで決定）
- **D-8 の判定範囲**: 衝突判定は「同一ビルド内の全グループ出力間」のみか、ディスク上の既存アセットも含むか（Req 2.6 の上書き仕様との整合から前者が自然）。同一グループ内の出力パス衝突（現行はエラー中断）の扱いも併せて明文化する。
- **生成順序と fail-fast の粒度**: 「グループごとに Timeline→Prefab→Scene」か「全 Timeline→全 Prefab→全 Scene」か。SceneFactory の NewScene(Single) 対策との親和性から前者が既存コードと整合的。
- **テンプレート更新方針**: 同梱 timeline-template.csv を複数 Timeline 形式に改変するか、別テンプレートファイルを追加するか（BundledTemplateE2ETests と既存ユーザーの出力への影響を考慮）。
- **アセット名確定タイミングの変更**: 現行はパース前にアセット名・出力パスを確定しているため、「パース → 命名・パス計画 → 検証 → 生成」への制御フロー再構成が必要（本機能最大の構造変更）。
- **非バッチ実行時の Scene 保存確認ダイアログ**: グループ数だけ呼ばれ得るため、集約するか許容するかを設計で決める。
