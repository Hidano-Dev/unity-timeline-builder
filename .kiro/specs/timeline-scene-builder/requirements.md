# Requirements Document

## Project Description (Input)
CSVフォーマットを拡張し、CSVからPlayableDirectorへのBindが完了したSceneファイルを生成できるようにする機能。既存のCSV→TimelineAsset/Prefab生成機能を土台に、CSVでScene名・配置するTimelineAsset・SceneにインスタンスするPrefabファイル・AnimationTrackにBindするAnimatorを持つGameObject名を指定できるようにし、生成したSceneにPlayableDirector付きGameObjectを配置し、TimelineAssetの割り当てとAnimationTrackへのBindまで自動で行う。

## Introduction
本仕様は、UPM パッケージ「Unity Timeline Builder」(`com.hidano.unity-timeline-builder`)の拡張機能として、構築情報 CSV/TSV から PlayableDirector へのバインド設定が完了した Scene ファイルを自動生成する機能の要件を定義する。既存の CSV → TimelineAsset / Prefab 構築機能(timeline-prefab-builder 仕様)を土台に、CSV フォーマットを拡張して Scene 名・PlayableDirector に割り当てる TimelineAsset・Scene にインスタンス化する Prefab・AnimationTrack にバインドする Animator を持つ GameObject 名を指定できるようにし、Scene 生成から AnimationTrack のバインディング設定までを自動化する。

## Boundary Context

- **In scope**:
  - Scene 構築情報(Scene 名、割り当てる TimelineAsset、インスタンス化する Prefab、AnimationTrack のバインド対象 GameObject 名)を指定できる CSV/TSV フォーマットの拡張とパース
  - Scene ファイル(.unity)の生成と、PlayableDirector を持つ GameObject の配置・TimelineAsset の割り当て
  - 指定された Prefab の Scene へのインスタンス配置
  - AnimationTrack への Animator バインディングの自動設定と Scene への保存
  - 公開 API・CLI バッチ実行・CSV テンプレート・列定義ドキュメントの拡張
  - 既存の TimelineAsset / Prefab 構築機能との後方互換性の維持
- **Out of scope**:
  - AnimationTrack 以外のトラック(AudioTrack 等)のバインディング設定(将来拡張)
  - プロジェクト外の Prefab ファイルのコピー・インポート(依存アセットを含むため将来検討。Prefab はプロジェクト内アセットパスでの指定のみ)
  - 既存 Scene への追記・マージ(本機能は新規 Scene の生成のみを対象とする)
  - ランタイム(再生時)での Scene 構築
  - JSON/XML など CSV/TSV 以外の構築情報フォーマット
- **Adjacent expectations**:
  - CSV/TSV のパース規約(Google スプレッドシートエクスポート互換、RFC 4180)、リソース解決、TimelineAsset / Prefab 構築の挙動は既存仕様(timeline-prefab-builder)に従い、変更しない
  - 既存フォーマットの CSV を入力した場合の成果物・挙動は従来どおり維持される

## Requirements

### Requirement 1: 構築情報フォーマットの拡張
**Objective:** As a コンテンツ制作者, I want 構築情報 CSV に Scene の構築内容(Scene 名、割り当てる TimelineAsset、配置する Prefab、バインド対象の GameObject 名)を記述したい, so that スプレッドシート上のデータだけで再生可能な Scene の構成まで指定できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 構築情報 CSV/TSV で Scene 名、PlayableDirector に割り当てる TimelineAsset、Scene にインスタンス化する Prefab、および AnimationTrack にバインドする Animator を持つ GameObject 名を指定できるフォーマットを定義する
2. The Unity Timeline Builder shall Scene 構築情報を既存の Track・クリップ構築情報と同一の構築情報ファイル内で区別して解釈する
3. The Unity Timeline Builder shall AnimationTrack とバインド対象 GameObject 名の対応を Track 名によって指定できるようにする
4. The Unity Timeline Builder shall 拡張後のフォーマットを Google スプレッドシートの File > Download でエクスポートされた CSV/TSV として既存のパース規約どおりにパースする
5. When Scene 構築情報を含まない既存フォーマットの構築情報ファイルが入力されたとき, the Unity Timeline Builder shall 既存の TimelineAsset / Prefab 構築を従来どおり実行する
6. If Scene 構築情報に必須項目の欠落または解釈できない値が含まれるとき, then the Unity Timeline Builder shall 行番号と原因を特定できるエラーを報告し、構築処理を中断する

### Requirement 2: Scene ファイルの生成と PlayableDirector の配置
**Objective:** As a コンテンツ制作者, I want CSV で指定した内容どおりに PlayableDirector 付き GameObject が配置された Scene ファイルを自動生成したい, so that Unity Editor 上での Scene 作成と PlayableDirector 設定の手作業をなくせる

#### Acceptance Criteria
1. When Scene 構築情報を含む構築処理が実行されたとき, the Unity Timeline Builder shall 構築情報で指定された Scene 名の Scene ファイル(.unity)を生成し、指定された出力先にアセットとして保存する
2. The Unity Timeline Builder shall 生成した Scene に PlayableDirector コンポーネントを持つ GameObject を配置する
3. The Unity Timeline Builder shall 配置した PlayableDirector の playableAsset に、構築情報で指定された TimelineAsset(同一の構築処理で生成した TimelineAsset を含む)を割り当てる
4. If 構築情報で指定された TimelineAsset が解決できないとき, then the Unity Timeline Builder shall 対象の指定内容を特定できるエラーを報告し、構築処理を中断する
5. If 出力先パスに既存の Scene ファイルが存在するとき, then the Unity Timeline Builder shall 既存ファイルを上書きし、上書きした旨をログに出力する

### Requirement 3: Prefab の Scene へのインスタンス配置
**Objective:** As a コンテンツ制作者, I want CSV で指定した Prefab を生成した Scene に自動でインスタンス配置したい, so that バインド対象を含む演出用オブジェクトを手作業なしで Scene に揃えられる

#### Acceptance Criteria
1. When Scene 構築情報に Prefab が指定されているとき, the Unity Timeline Builder shall 指定された Prefab を Prefab インスタンスとして生成した Scene に配置する
2. The Unity Timeline Builder shall 複数の Prefab 指定を受け付け、指定されたすべての Prefab を Scene に配置する
3. The Unity Timeline Builder shall Prefab の指定をプロジェクト内のアセットパス(Assets/ 配下)として解決する
4. If 指定されたパスに Prefab アセットが存在しない、または Prefab として解決できないとき, then the Unity Timeline Builder shall 対象の行と参照パスを特定できるエラーを報告し、構築処理を中断する

### Requirement 4: AnimationTrack バインディングの自動設定
**Objective:** As a コンテンツ制作者, I want AnimationTrack へのバインドが完了した状態の Scene を得たい, so that 生成された Scene を開くだけで Timeline のアニメーションを再生・確認できる

#### Acceptance Criteria
1. When Scene 構築情報に AnimationTrack のバインド対象 GameObject 名が指定されているとき, the Unity Timeline Builder shall 生成した Scene 内(配置した Prefab インスタンスの階層を含む)から該当する名前の GameObject を特定し、その GameObject が持つ Animator を対象の AnimationTrack のバインディングとして PlayableDirector に設定する
2. The Unity Timeline Builder shall 設定したバインディングを保存後の Scene ファイルを再度開いた際にも保持されるように保存する
3. The Unity Timeline Builder shall バインド指定のない Track のバインディングを未設定のままにする
4. If 指定された名前の GameObject が Scene 内に存在しないとき, then the Unity Timeline Builder shall 対象の GameObject 名を特定できるエラーを報告し、構築処理を中断する
5. If 指定された名前に一致する GameObject が Scene 内に複数存在し、バインド対象を一意に特定できないとき, then the Unity Timeline Builder shall 重複した GameObject 名を特定できるエラーを報告し、構築処理を中断する
6. If 特定した GameObject が Animator コンポーネントを持たないとき, then the Unity Timeline Builder shall 対象の GameObject 名を特定できるエラーを報告し、構築処理を中断する
7. If バインド指定の Track 名に対応する AnimationTrack が TimelineAsset に存在しないとき, then the Unity Timeline Builder shall 対象の Track 名を特定できるエラーを報告し、構築処理を中断する

### Requirement 5: 公開 API の拡張
**Objective:** As a ツール開発者, I want Scene 構築を含む構築処理を public static メソッドとして呼び出したい, so that CLI・エディタ拡張・他ツールのいずれからも同一の Scene 構築処理を再利用できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 構築情報ファイルのパスと出力先を入力として、Scene 構築を含む構築処理を実行する public static メソッドを公開する
2. When 公開 API が呼び出され構築が完了したとき, the Unity Timeline Builder shall 構築の成功・失敗、失敗理由、および生成した Scene ファイルのアセットパスを呼び出し元が判別できる形式で返却する
3. If 公開 API に不正な引数(null、空文字、存在しないパス等)が渡されたとき, then the Unity Timeline Builder shall 例外送出またはエラー結果の返却により原因を特定できる形で失敗を通知する
4. The Unity Timeline Builder shall 既存の公開 API の呼び出し互換性(シグネチャおよび既存フォーマット入力時の挙動)を維持する

### Requirement 6: CLI バッチ実行の拡張
**Objective:** As a ビルドパイプライン管理者, I want Unity Editor の -batchmode / -executeMethod 経由で Scene 構築まで実行したい, so that CI や外部スクリプトから人手を介さずにバインド済み Scene を生成できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall Unity Editor の -batchmode / -executeMethod から Scene 構築を含む構築処理を実行できるエントリポイントを提供する
2. When CLI エントリポイントが実行されたとき, the Unity Timeline Builder shall コマンドライン引数から構築情報ファイルのパスと出力先の指定を読み取る
3. When 構築処理が正常に完了したとき, the Unity Timeline Builder shall 生成した Scene ファイルのアセットパスをログに出力し、exit code 0 で Unity Editor プロセスを終了させる
4. If 構築処理が失敗したとき, then the Unity Timeline Builder shall 失敗の原因(対象ファイル、行番号、エラー内容等)を特定できるメッセージをログに出力し、0 以外の exit code で Unity Editor プロセスを終了させる

### Requirement 7: テンプレートとドキュメントの更新
**Objective:** As a コンテンツ制作者, I want Scene 構築情報の記述方法をテンプレートとドキュメントで参照したい, so that 正しいデータ構造で Scene 構築情報を作成できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 同梱の CSV テンプレートに Scene 構築情報のヘッダーと記入例を示すサンプルデータを追加する
2. The Unity Timeline Builder shall 列定義ドキュメントに Scene 構築情報の各項目の名称・意味・データ型・必須/任意・記入例を追加する
3. The Unity Timeline Builder shall CSV テンプレートおよび列定義ドキュメントの内容を拡張後のパーサーの仕様と一致させる
